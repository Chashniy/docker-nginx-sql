using System.Text.Json;
using Backend.Data;

namespace Backend.Outbox;

public class OutboxRepository
{
    private readonly SqliteContext _sqlite;

    public OutboxRepository(SqliteContext sqlite)
    {
        _sqlite = sqlite;
    }

    public async Task<string> AddTimeRecordAsync(DateTime recordedAt, string? createdBy)
    {
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        var payload = JsonSerializer.Serialize(new
        {
            id,
            recorded_at = recordedAt,
            created_by = createdBy,
            created_at = now
        });

        using var connection = _sqlite.CreateConnection();
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO outbox (id, payload, status, created_at, next_retry_at)
            VALUES ($id, $payload, 'pending', $createdAt, $nextRetry);
        ";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$payload", payload);
        cmd.Parameters.AddWithValue("$createdAt", now.ToString("O"));
        cmd.Parameters.AddWithValue("$nextRetry", now.ToString("O"));

        await cmd.ExecuteNonQueryAsync();

        return id;
    }
}