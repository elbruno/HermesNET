using Microsoft.Data.Sqlite;
using System.Runtime.CompilerServices;

namespace Hermes.Core.Profiles;

/// <summary>
/// SQLite-backed profile service.
/// Profile switching is atomic: a single UPDATE to AppState inside a transaction prevents partial state.
/// </summary>
public sealed class ProfileService : IProfileService, IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _initialized;

    public ProfileService(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        await _connection.OpenAsync(cancellationToken);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Profiles (
                Id          TEXT PRIMARY KEY,
                Name        TEXT NOT NULL,
                Description TEXT,
                CreatedAt   TEXT NOT NULL,
                UpdatedAt   TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS idx_profiles_name ON Profiles(Name);
            CREATE TABLE IF NOT EXISTS AppState (
                Key   TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        _initialized = true;
    }

    public async Task<Profile> CreateProfileAsync(
        string name,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var profile = new Profile
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Profiles (Id, Name, Description, CreatedAt, UpdatedAt)
            VALUES (@id, @name, @desc, @createdAt, @updatedAt);
            """;
        cmd.Parameters.AddWithValue("@id", profile.Id);
        cmd.Parameters.AddWithValue("@name", profile.Name);
        cmd.Parameters.AddWithValue("@desc", profile.Description ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@createdAt", profile.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@updatedAt", profile.UpdatedAt.ToString("O"));

        try
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // SQLITE_CONSTRAINT
        {
            throw new InvalidOperationException($"A profile named '{name}' already exists.");
        }

        return profile;
    }

    public async Task<Profile?> GetProfileAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        return await QuerySingleAsync("WHERE Id = @param", id, cancellationToken);
    }

    public async Task<Profile?> GetProfileByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        return await QuerySingleAsync("WHERE Name = @param", name, cancellationToken);
    }

    public async Task<Profile> UpdateProfileAsync(
        string id,
        string? name = null,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var existing = await GetProfileAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Profile '{id}' not found.");

        var newName = name ?? existing.Name;
        var newDesc = description ?? existing.Description;
        var updatedAt = DateTimeOffset.UtcNow;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            UPDATE Profiles SET Name = @name, Description = @desc, UpdatedAt = @updatedAt
            WHERE Id = @id;
            """;
        cmd.Parameters.AddWithValue("@name", newName);
        cmd.Parameters.AddWithValue("@desc", newDesc ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@updatedAt", updatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return new Profile
        {
            Id = id,
            Name = newName,
            Description = newDesc,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = updatedAt
        };
    }

    public async Task DeleteProfileAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        using var txn = _connection.BeginTransaction();
        try
        {
            // Clear current profile pointer if it pointed to this profile
            using var clearState = _connection.CreateCommand();
            clearState.Transaction = txn;
            clearState.CommandText = "DELETE FROM AppState WHERE Key = 'current_profile_id' AND Value = @id;";
            clearState.Parameters.AddWithValue("@id", id);
            await clearState.ExecuteNonQueryAsync(cancellationToken);

            using var cmd = _connection.CreateCommand();
            cmd.Transaction = txn;
            cmd.CommandText = "DELETE FROM Profiles WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);

            if (affected == 0)
                throw new KeyNotFoundException($"Profile '{id}' not found.");

            txn.Commit();
        }
        catch
        {
            txn.Rollback();
            throw;
        }
    }

    public async IAsyncEnumerable<Profile> ListProfilesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Name, Description, CreatedAt, UpdatedAt
            FROM Profiles
            ORDER BY CreatedAt ASC;
            """;

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            yield return MapRow(reader);
    }

    public async Task SwitchProfileAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        // Verify the profile exists before switching — fail-fast.
        var exists = await GetProfileAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Profile '{id}' not found.");

        // Atomic upsert — no partial state possible.
        using var txn = _connection.BeginTransaction();
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = txn;
        cmd.CommandText = """
            INSERT INTO AppState (Key, Value) VALUES ('current_profile_id', @id)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        txn.Commit();
    }

    public async Task<Profile?> GetCurrentProfileAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var currentId = await GetAppStateAsync("current_profile_id", cancellationToken);
        if (currentId is null) return null;

        return await GetProfileAsync(currentId, cancellationToken);
    }

    public void Dispose() => _connection.Dispose();

    private async Task<Profile?> QuerySingleAsync(
        string whereClause,
        string paramValue,
        CancellationToken cancellationToken)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT Id, Name, Description, CreatedAt, UpdatedAt FROM Profiles {whereClause};";
        cmd.Parameters.AddWithValue("@param", paramValue);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return MapRow(reader);
    }

    private async Task<string?> GetAppStateAsync(string key, CancellationToken cancellationToken)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Value FROM AppState WHERE Key = @key;";
        cmd.Parameters.AddWithValue("@key", key);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    private static Profile MapRow(SqliteDataReader r) =>
        new()
        {
            Id = r.GetString(0),
            Name = r.GetString(1),
            Description = r.IsDBNull(2) ? null : r.GetString(2),
            CreatedAt = DateTimeOffset.Parse(r.GetString(3)),
            UpdatedAt = DateTimeOffset.Parse(r.GetString(4))
        };

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException(
                "ProfileService has not been initialized. Call InitializeAsync() first.");
    }
}
