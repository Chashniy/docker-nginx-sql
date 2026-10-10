using System.Text.Json;
using Backend.Data;
using Backend.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Backend.Outbox;

public class OutboxSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxSyncService> _logger;

    // Интервал между проходами
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    // Сколько записей за раз
    private const int BatchSize = 100;

    // Максимум попыток
    private const int MaxAttempts = 10;

    public OutboxSyncService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxSyncService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox sync loop");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        _logger.LogInformation("OutboxSyncService stopped");
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var sqlite = scope.ServiceProvider.GetRequiredService<SqliteContext>();
        var maria = scope.ServiceProvider.GetRequiredService<MariaDbContext>();

        // 1. Читаем pending-записи
        var pending = await ReadPendingAsync(sqlite, ct);
        if (pending.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Processing {Count} outbox records", pending.Count);

        foreach (var item in pending)
        {
            try
            {
                // 2. Парсим payload
                var payload = JsonSerializer.Deserialize<OutboxPayload>(item.Payload);
                if (payload == null)
                {
                    await MarkFailedAsync(sqlite, item.Id, "Invalid payload", ct);
                    continue;
                }

                // 3. Идемпотентная вставка в MariaDB
                var exists = await maria.TimeRecords.AnyAsync(r => r.Id == payload.Id, ct);
                if (!exists)
                {
                    var record = new TimeRecord
                    {
                        Id = payload.Id,
                        RecordedAt = payload.RecordedAt,
                        CreatedBy = payload.CreatedBy,
                        CreatedAt = payload.CreatedAt
                    };
                    maria.TimeRecords.Add(record);
                    await maria.SaveChangesAsync(ct);
                }

                // 4. Помечаем synced
                await MarkSyncedAsync(sqlite, item.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync outbox record {Id}", item.Id);
                await MarkFailedAsync(sqlite, item.Id, ex.Message, ct);
            }
        }
    }

    private static async Task<List<OutboxItem>> ReadPendingAsync(SqliteContext sqlite, CancellationToken ct)
    {
        var result = new List<OutboxItem>();
        var now = DateTime.UtcNow.ToString("O");

        using var connection = sqlite.CreateConnection();
        await connection.OpenAsync(ct);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, payload, attempts
            FROM outbox
            WHERE status = 'pending'
              AND (next_retry_at IS NULL OR next_retry_at <= $now)
            ORDER BY created_at
            LIMIT $limit;
        ";
        cmd.Parameters.AddWithValue("$now", now);
        cmd.Parameters.AddWithValue("$limit", BatchSize);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new OutboxItem(
                Id: reader.GetString(0),
                Payload: reader.GetString(1),
                Attempts: reader.GetInt32(2)
            ));
        }

        return result;
    }

    private static async Task MarkSyncedAsync(SqliteContext sqlite, string id, CancellationToken ct)
    {
        using var connection = sqlite.CreateConnection();
        await connection.OpenAsync(ct);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE outbox SET status = 'synced' WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task MarkFailedAsync(SqliteContext sqlite, string id, string error, CancellationToken ct)
    {
        using var connection = sqlite.CreateConnection();
        await connection.OpenAsync(ct);

        // Читаем attempts
        int attempts;
        using (var readCmd = connection.CreateCommand())
        {
            readCmd.CommandText = "SELECT attempts FROM outbox WHERE id = $id;";
            readCmd.Parameters.AddWithValue("$id", id);
            var result = await readCmd.ExecuteScalarAsync(ct);
            attempts = Convert.ToInt32(result);
        }

        attempts++;

        // Экспоненциальная задержка: 2^attempts секунд (ограничим 1 час)
        var delaySeconds = Math.Min(Math.Pow(2, attempts), 3600);
        var nextRetry = DateTime.UtcNow.AddSeconds(delaySeconds).ToString("O");

        // Если превысили лимит — помечаем failed, иначе остаётся pending
        var newStatus = attempts >= MaxAttempts ? "failed" : "pending";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE outbox
            SET attempts = $attempts,
                last_error = $error,
                next_retry_at = $nextRetry,
                status = $status
            WHERE id = $id;
        ";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$attempts", attempts);
        cmd.Parameters.AddWithValue("$error", error);
        cmd.Parameters.AddWithValue("$nextRetry", nextRetry);
        cmd.Parameters.AddWithValue("$status", newStatus);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

// === DTO ===
public record OutboxItem(string Id, string Payload, int Attempts);

public class OutboxPayload
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public Guid Id { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("recorded_at")]
    public DateTime RecordedAt { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("created_by")]
    public Guid? CreatedBy { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}