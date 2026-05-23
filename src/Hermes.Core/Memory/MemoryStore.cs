using Microsoft.Data.Sqlite;
using System.Diagnostics;
using Hermes.Core.Telemetry;

namespace Hermes.Core.Memory;

/// <summary>
/// SQLite-backed implementation of <see cref="IMemoryService"/>.
///
/// Profile scoping is enforced at every query via a mandatory @profileId parameter.
/// There is no read or write path that omits the WHERE profileId = @profileId predicate.
/// </summary>
public sealed class MemoryStore : IMemoryService, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MemorySchema _schema;
    private bool _initialized;

    public MemoryStore(string connectionString, MemorySchema? schema = null)
    {
        _connection = new SqliteConnection(connectionString);
        _schema = schema ?? MemorySchema.Default;
    }

    /// <summary>Creates the Memory and UserProfiles tables if they do not exist.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        await _connection.OpenAsync(cancellationToken);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Memory (
                Id        TEXT NOT NULL PRIMARY KEY,
                ProfileId TEXT NOT NULL,
                Kind      TEXT NOT NULL DEFAULT 'memory',
                Content   TEXT NOT NULL DEFAULT '',
                Format    TEXT NOT NULL DEFAULT 'markdown',
                Version   INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_memory_profile ON Memory(ProfileId);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_memory_profile_kind ON Memory(ProfileId, Kind);

            CREATE TABLE IF NOT EXISTS UserProfiles (
                Id            TEXT NOT NULL PRIMARY KEY,
                ProfileId     TEXT NOT NULL UNIQUE,
                Data          TEXT NOT NULL DEFAULT '',
                SchemaVersion INTEGER NOT NULL DEFAULT 1,
                CreatedAt     TEXT NOT NULL,
                UpdatedAt     TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_userprofiles_profile ON UserProfiles(ProfileId);
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _initialized = true;
    }

    /// <inheritdoc/>
    public async Task<MemoryContext> LoadMemoryAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        using var span = TelemetryProvider.GetActivitySource().StartActivity("MemoryStore.LoadMemoryAsync");
        span?.SetTag("profile.id", profileId);
        span?.SetTag("operation", "load");
        
        ValidateProfileId(profileId);
        EnsureInitialized();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT Content, Format, Version, UpdatedAt
            FROM Memory
            WHERE ProfileId = @profileId AND Kind = @kind
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@profileId", profileId);
        cmd.Parameters.AddWithValue("@kind", MemoryKind.Memory);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return MemoryContext.Empty(profileId);

        return new MemoryContext(
            ProfileId: profileId,
            Content: reader.GetString(0),
            Format: reader.GetString(1),
            Version: reader.GetInt32(2),
            UpdatedAt: DateTime.Parse(reader.GetString(3)));
    }

    /// <inheritdoc/>
    public async Task UpdateMemoryAsync(
        string profileId,
        string content,
        CancellationToken cancellationToken = default)
    {
        using var span = TelemetryProvider.GetActivitySource().StartActivity("MemoryStore.UpdateMemoryAsync");
        span?.SetTag("profile.id", profileId);
        span?.SetTag("operation", "update");
        span?.SetTag("memory.size", content.Length);
        
        ValidateProfileId(profileId);
        ValidateContent(content);
        EnsureInitialized();

        var now = DateTime.UtcNow.ToString("O");

        using var cmd = _connection.CreateCommand();
        // Upsert: insert or update. Version increments on every write.
        cmd.CommandText = """
            INSERT INTO Memory (Id, ProfileId, Kind, Content, Format, Version, CreatedAt, UpdatedAt)
            VALUES (@id, @profileId, @kind, @content, 'markdown', 1, @now, @now)
            ON CONFLICT(ProfileId, Kind) DO UPDATE SET
                Content   = excluded.Content,
                Version   = Memory.Version + 1,
                UpdatedAt = excluded.UpdatedAt;
            """;
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("@profileId", profileId);
        cmd.Parameters.AddWithValue("@kind", MemoryKind.Memory);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.Parameters.AddWithValue("@now", now);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<UserProfileData> LoadUserProfileAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        using var span = TelemetryProvider.GetActivitySource().StartActivity("MemoryStore.LoadUserProfileAsync");
        span?.SetTag("profile.id", profileId);
        span?.SetTag("operation", "load");
        
        ValidateProfileId(profileId);
        EnsureInitialized();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT Data, SchemaVersion, UpdatedAt
            FROM UserProfiles
            WHERE ProfileId = @profileId
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@profileId", profileId);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return UserProfileData.Empty(profileId);

        return new UserProfileData(
            ProfileId: profileId,
            Data: reader.GetString(0),
            SchemaVersion: reader.GetInt32(1),
            UpdatedAt: DateTime.Parse(reader.GetString(2)));
    }

    /// <inheritdoc/>
    public async Task UpdateUserProfileAsync(
        string profileId,
        string data,
        CancellationToken cancellationToken = default)
    {
        using var span = TelemetryProvider.GetActivitySource().StartActivity("MemoryStore.UpdateUserProfileAsync");
        span?.SetTag("profile.id", profileId);
        span?.SetTag("operation", "update");
        span?.SetTag("memory.size", data.Length);
        
        ValidateProfileId(profileId);
        EnsureInitialized();

        var now = DateTime.UtcNow.ToString("O");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO UserProfiles (Id, ProfileId, Data, SchemaVersion, CreatedAt, UpdatedAt)
            VALUES (@id, @profileId, @data, 1, @now, @now)
            ON CONFLICT(ProfileId) DO UPDATE SET
                Data          = excluded.Data,
                SchemaVersion = UserProfiles.SchemaVersion + 1,
                UpdatedAt     = excluded.UpdatedAt;
            """;
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("@profileId", profileId);
        cmd.Parameters.AddWithValue("@data", data);
        cmd.Parameters.AddWithValue("@now", now);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<MemorySchema> GetMemorySchemaAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_schema);

    public void Dispose() => _connection.Dispose();

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException(
                "MemoryStore has not been initialized. Call InitializeAsync() first.");
    }

    private static void ValidateProfileId(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("profileId must not be null or empty.", nameof(profileId));
    }

    private void ValidateContent(string content)
    {
        if (!_schema.IsContentValid(content))
            throw new ArgumentException(
                $"Memory content exceeds maximum allowed size of {_schema.MaxContentBytes} bytes.",
                nameof(content));
    }
}
