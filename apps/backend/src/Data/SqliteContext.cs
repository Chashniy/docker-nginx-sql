using Microsoft.Data.Sqlite;

namespace Backend.Data;

public class SqliteContext
{
    private readonly string _connectionString;

    public SqliteContext(IConfiguration configuration)
    {
        // Путь к SQLite-файлу. Можно переопределить через appsettings.json.
        var dbPath = configuration["Sqlite:Path"] ?? "/app/data/app.db";
        _connectionString = $"Data Source={dbPath}";

        // Убедимся, что папка существует
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Initialize();
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Включаем WAL — позволяет читать и писать параллельно
        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
            pragma.ExecuteNonQuery();
        }

        // Таблица сессий
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS sessions (
                    id         TEXT PRIMARY KEY,
                    user_id    TEXT NOT NULL,
                    username   TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    expires_at TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_sessions_expires ON sessions(expires_at);
            ";
            cmd.ExecuteNonQuery();
        }

        // Таблица outbox
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS outbox (
                    id            TEXT PRIMARY KEY,
                    payload       TEXT NOT NULL,
                    status        TEXT NOT NULL DEFAULT 'pending',
                    attempts      INTEGER NOT NULL DEFAULT 0,
                    last_error    TEXT,
                    created_at    TEXT NOT NULL,
                    next_retry_at TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_outbox_status ON outbox(status, next_retry_at);
            ";
            cmd.ExecuteNonQuery();
        }
    }

    // === Sessions ===

    public string CreateSession(string userId, string username, TimeSpan ttl)
    {
        var sessionId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        var expiresAt = now + ttl;

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO sessions (id, user_id, username, created_at, expires_at)
            VALUES ($id, $userId, $username, $createdAt, $expiresAt);
        ";
        cmd.Parameters.AddWithValue("$id", sessionId);
        cmd.Parameters.AddWithValue("$userId", userId);
        cmd.Parameters.AddWithValue("$username", username);
        cmd.Parameters.AddWithValue("$createdAt", now.ToString("O"));
        cmd.Parameters.AddWithValue("$expiresAt", expiresAt.ToString("O"));
        cmd.ExecuteNonQuery();

        return sessionId;
    }

    public SessionData? GetSession(string sessionId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT user_id, username, expires_at
            FROM sessions
            WHERE id = $id;
        ";
        cmd.Parameters.AddWithValue("$id", sessionId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var expiresAt = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind);
        if (expiresAt < DateTime.UtcNow)
        {
            // Сессия просрочена — удаляем и возвращаем null
            DeleteSession(sessionId);
            return null;
        }

        return new SessionData(
            UserId: reader.GetString(0),
            Username: reader.GetString(1),
            ExpiresAt: expiresAt
        );
    }

    public void DeleteSession(string sessionId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM sessions WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", sessionId);
        cmd.ExecuteNonQuery();
    }

    public int CleanupExpiredSessions()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM sessions WHERE expires_at < $now;";
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        return cmd.ExecuteNonQuery();
    }
}

public record SessionData(string UserId, string Username, DateTime ExpiresAt);