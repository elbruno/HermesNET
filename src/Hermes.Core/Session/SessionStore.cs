using Microsoft.Data.Sqlite;

namespace Hermes.Core.Session;

/// <summary>
/// SQLite-backed session store. Accepts any ADO.NET connection string,
/// including ":memory:" for unit tests.
/// </summary>
public sealed class SessionStore : ISessionStore, IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _initialized;

    public SessionStore(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;

        await _connection.OpenAsync(cancellationToken);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Sessions (
                Id           TEXT    PRIMARY KEY,
                ProfileId    TEXT    NOT NULL,
                CreatedAt    TEXT    NOT NULL,
                UpdatedAt    TEXT    NOT NULL,
                LastMessage  TEXT,
                MessageCount INTEGER DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_sessions_profile ON Sessions(ProfileId);
            CREATE INDEX IF NOT EXISTS idx_sessions_created ON Sessions(CreatedAt);
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        _initialized = true;
    }

    public async Task<SessionEntity> CreateAsync(
        string profileId,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var entity = new SessionEntity
        {
            Id = Guid.NewGuid().ToString(),
            ProfileId = profileId,
            CreatedAt = DateTime.UtcNow.ToString("O"),
            UpdatedAt = DateTime.UtcNow.ToString("O"),
            LastMessage = message,
            MessageCount = message is null ? 0 : 1
        };

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Sessions (Id, ProfileId, CreatedAt, UpdatedAt, LastMessage, MessageCount)
            VALUES (@id, @profileId, @createdAt, @updatedAt, @lastMessage, @messageCount);
            """;
        cmd.Parameters.AddWithValue("@id", entity.Id);
        cmd.Parameters.AddWithValue("@profileId", entity.ProfileId);
        cmd.Parameters.AddWithValue("@createdAt", entity.CreatedAt);
        cmd.Parameters.AddWithValue("@updatedAt", entity.UpdatedAt);
        cmd.Parameters.AddWithValue("@lastMessage", entity.LastMessage ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@messageCount", entity.MessageCount);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return entity;
    }

    public async Task<SessionEntity?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ProfileId, CreatedAt, UpdatedAt, LastMessage, MessageCount
            FROM Sessions WHERE Id = @id;
            """;
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapRow(reader);
    }

    public async Task<SessionEntity> UpdateAsync(
        string id,
        string lastMessage,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var updatedAt = DateTime.UtcNow.ToString("O");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            UPDATE Sessions
            SET LastMessage  = @lastMessage,
                UpdatedAt    = @updatedAt,
                MessageCount = MessageCount + 1
            WHERE Id = @id;
            """;
        cmd.Parameters.AddWithValue("@lastMessage", lastMessage);
        cmd.Parameters.AddWithValue("@updatedAt", updatedAt);
        cmd.Parameters.AddWithValue("@id", id);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
            throw new KeyNotFoundException($"Session '{id}' not found.");

        var entity = await GetAsync(id, cancellationToken);
        return entity!;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Sessions WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
            throw new KeyNotFoundException($"Session '{id}' not found.");
    }

    public async Task<IReadOnlyList<SessionEntity>> ListRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ProfileId, CreatedAt, UpdatedAt, LastMessage, MessageCount
            FROM Sessions
            ORDER BY CreatedAt DESC
            LIMIT @limit;
            """;
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var results = new List<SessionEntity>();
        while (await reader.ReadAsync(cancellationToken))
            results.Add(MapRow(reader));

        return results.AsReadOnly();
    }

    public void Dispose() => _connection.Dispose();

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException(
                "SessionStore has not been initialized. Call InitializeAsync() first.");
    }

    private static SessionEntity MapRow(SqliteDataReader reader) =>
        new()
        {
            Id = reader.GetString(0),
            ProfileId = reader.GetString(1),
            CreatedAt = reader.GetString(2),
            UpdatedAt = reader.GetString(3),
            LastMessage = reader.IsDBNull(4) ? null : reader.GetString(4),
            MessageCount = reader.GetInt32(5)
        };
}
