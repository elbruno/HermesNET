using Microsoft.Data.Sqlite;
using System.Runtime.CompilerServices;

namespace Hermes.Core.Profiles;

/// <summary>
/// SQLite-backed session service scoped to profiles.
/// Session switching is atomic; cross-profile session access is enforced at the contract level.
/// </summary>
public sealed class SessionService : ISessionService, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IProfileService _profileService;
    private bool _initialized;

    public SessionService(string connectionString, IProfileService profileService)
    {
        _connection = new SqliteConnection(connectionString);
        _profileService = profileService;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        await _connection.OpenAsync(cancellationToken);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS ProfileSessions (
                Id           TEXT PRIMARY KEY,
                ProfileId    TEXT NOT NULL,
                Name         TEXT NOT NULL,
                CreatedAt    TEXT NOT NULL,
                LastAccessed TEXT NOT NULL,
                Metadata     TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_psessions_profile ON ProfileSessions(ProfileId);
            CREATE INDEX IF NOT EXISTS idx_psessions_accessed ON ProfileSessions(LastAccessed);
            CREATE TABLE IF NOT EXISTS AppState (
                Key   TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        _initialized = true;
    }

    public async Task<ProfileSession> CreateSessionAsync(
        string profileId,
        string name,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        // Verify the owning profile exists before creating a session.
        var profile = await _profileService.GetProfileAsync(profileId, cancellationToken)
            ?? throw new KeyNotFoundException($"Profile '{profileId}' not found.");

        var now = DateTimeOffset.UtcNow;
        var session = new ProfileSession
        {
            Id = Guid.NewGuid().ToString(),
            ProfileId = profileId,
            Name = name,
            CreatedAt = now,
            LastAccessed = now,
            Metadata = null
        };

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ProfileSessions (Id, ProfileId, Name, CreatedAt, LastAccessed, Metadata)
            VALUES (@id, @profileId, @name, @createdAt, @lastAccessed, @metadata);
            """;
        cmd.Parameters.AddWithValue("@id", session.Id);
        cmd.Parameters.AddWithValue("@profileId", session.ProfileId);
        cmd.Parameters.AddWithValue("@name", session.Name);
        cmd.Parameters.AddWithValue("@createdAt", session.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@lastAccessed", session.LastAccessed.ToString("O"));
        cmd.Parameters.AddWithValue("@metadata", session.Metadata ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return session;
    }

    public async Task<ProfileSession?> GetSessionAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ProfileId, Name, CreatedAt, LastAccessed, Metadata
            FROM ProfileSessions WHERE Id = @id;
            """;
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return MapRow(reader);
    }

    public async Task SaveSessionAsync(
        string id,
        string? metadata,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            UPDATE ProfileSessions
            SET Metadata = @metadata, LastAccessed = @lastAccessed
            WHERE Id = @id;
            """;
        cmd.Parameters.AddWithValue("@metadata", metadata ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@lastAccessed", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", id);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
            throw new KeyNotFoundException($"Session '{id}' not found.");
    }

    public async IAsyncEnumerable<ProfileSession> ListSessionsByProfileAsync(
        string profileId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        // Single query — no N+1 risk by design.
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ProfileId, Name, CreatedAt, LastAccessed, Metadata
            FROM ProfileSessions
            WHERE ProfileId = @profileId
            ORDER BY LastAccessed DESC;
            """;
        cmd.Parameters.AddWithValue("@profileId", profileId);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            yield return MapRow(reader);
    }

    public async Task SwitchSessionAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var session = await GetSessionAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Session '{id}' not found.");

        // Enforce cross-profile isolation at the service level.
        var currentProfile = await _profileService.GetCurrentProfileAsync(cancellationToken);
        if (currentProfile is not null && session.ProfileId != currentProfile.Id)
            throw new UnauthorizedAccessException(
                $"Session '{id}' belongs to profile '{session.ProfileId}', not the current profile '{currentProfile.Id}'.");

        // Atomic switch + update last_accessed.
        using var txn = _connection.BeginTransaction();

        using var updateAccessed = _connection.CreateCommand();
        updateAccessed.Transaction = txn;
        updateAccessed.CommandText = """
            UPDATE ProfileSessions SET LastAccessed = @now WHERE Id = @id;
            """;
        updateAccessed.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("O"));
        updateAccessed.Parameters.AddWithValue("@id", id);
        await updateAccessed.ExecuteNonQueryAsync(cancellationToken);

        using var setState = _connection.CreateCommand();
        setState.Transaction = txn;
        setState.CommandText = """
            INSERT INTO AppState (Key, Value) VALUES ('current_session_id', @id)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        setState.Parameters.AddWithValue("@id", id);
        await setState.ExecuteNonQueryAsync(cancellationToken);

        txn.Commit();
    }

    public async Task<ProfileSession?> GetCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var currentId = await GetAppStateAsync("current_session_id", cancellationToken);
        if (currentId is null) return null;

        return await GetSessionAsync(currentId, cancellationToken);
    }

    public async Task DeleteSessionAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        using var txn = _connection.BeginTransaction();
        try
        {
            // Clear current session pointer if it pointed to this session.
            using var clearCurrent = _connection.CreateCommand();
            clearCurrent.Transaction = txn;
            clearCurrent.CommandText = "DELETE FROM AppState WHERE Key = 'current_session_id' AND Value = @id;";
            clearCurrent.Parameters.AddWithValue("@id", id);
            await clearCurrent.ExecuteNonQueryAsync(cancellationToken);

            using var cmd = _connection.CreateCommand();
            cmd.Transaction = txn;
            cmd.CommandText = "DELETE FROM ProfileSessions WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);

            if (affected == 0)
                throw new KeyNotFoundException($"Session '{id}' not found.");

            txn.Commit();
        }
        catch
        {
            txn.Rollback();
            throw;
        }
    }

    public async Task UpdateSessionAsync(
        string id,
        string name,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            UPDATE ProfileSessions
            SET Name = @name, LastAccessed = @lastAccessed
            WHERE Id = @id;
            """;
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@lastAccessed", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", id);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
            throw new KeyNotFoundException($"Session '{id}' not found.");
    }

    public async IAsyncEnumerable<ProfileSession> GetSessionsByProfileAsync(
        string profileId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var s in ListSessionsByProfileAsync(profileId, cancellationToken))
            yield return s;
    }

    public async IAsyncEnumerable<ProfileSession> ListSessionsAsync(
        string? profileId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        if (profileId is null)
        {
            var current = await _profileService.GetCurrentProfileAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "No active profile. Switch to a profile before listing sessions without an explicit profileId.");
            profileId = current.Id;
        }

        await foreach (var s in ListSessionsByProfileAsync(profileId, cancellationToken))
            yield return s;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<string?> GetAppStateAsync(string key, CancellationToken cancellationToken)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Value FROM AppState WHERE Key = @key;";
        cmd.Parameters.AddWithValue("@key", key);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    private static ProfileSession MapRow(SqliteDataReader r) =>
        new()
        {
            Id = r.GetString(0),
            ProfileId = r.GetString(1),
            Name = r.GetString(2),
            CreatedAt = DateTimeOffset.Parse(r.GetString(3)),
            LastAccessed = DateTimeOffset.Parse(r.GetString(4)),
            Metadata = r.IsDBNull(5) ? null : r.GetString(5)
        };

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException(
                "SessionService has not been initialized. Call InitializeAsync() first.");
    }
}
