# Skill: .NET Service Contract Pattern

**Category:** Backend / Architecture  
**Extracted by:** Dallas  
**Date:** 2026-05-22T17:14:32.037-04:00  
**Source:** T13 Profile and Session Management implementation

---

## Pattern: Explicit Service Contracts with SQLite Backing

When implementing a new service layer in Hermes.NET, follow this pattern for predictable, testable contracts.

### 1. Interface First — Explicit Contracts

```csharp
public interface IProfileService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<Profile> CreateProfileAsync(string name, string? description = null, CancellationToken cancellationToken = default);
    Task<Profile?> GetProfileAsync(string id, CancellationToken cancellationToken = default);
    Task<Profile> UpdateProfileAsync(string id, string? name = null, string? description = null, CancellationToken cancellationToken = default);
    Task DeleteProfileAsync(string id, CancellationToken cancellationToken = default);
    IAsyncEnumerable<Profile> ListProfilesAsync(CancellationToken cancellationToken = default);
    Task SwitchProfileAsync(string id, CancellationToken cancellationToken = default);
    Task<Profile?> GetCurrentProfileAsync(CancellationToken cancellationToken = default);
}
```

**Rules:**
- No `void` returns on mutations — always return the affected object or throw (caller knows what happened)
- Nullable return (`T?`) means "not found" — never throw for missing reads
- `KeyNotFoundException` for writes to missing IDs (fail-fast)
- `InvalidOperationException` for constraint violations (duplicate name, cross-profile access)
- All methods take `CancellationToken` as last parameter with default

### 2. SQLite ADO.NET Implementation

Use `Microsoft.Data.Sqlite` directly (not EF Core) for services where:
- All queries are explicit and don't need change tracking
- Schema is simple (2-4 tables)
- P95 latency targets exist

**Pattern:**
```csharp
public sealed class ProfileService : IProfileService, IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _initialized;

    public ProfileService(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;
        await _connection.OpenAsync(ct);
        // CREATE TABLE IF NOT EXISTS ...
        _initialized = true;
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("Call InitializeAsync() first.");
    }
}
```

### 3. Atomic State Switching with AppState Table

For "current X" pointers (profile, session) that must persist across restarts:

```sql
CREATE TABLE AppState (Key TEXT PRIMARY KEY, Value TEXT NOT NULL);
```

Atomic upsert:
```sql
INSERT INTO AppState (Key, Value) VALUES ('current_profile_id', @id)
ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
```

Always wrap in a transaction so the switch is all-or-nothing.

### 4. Transaction Safety — Avoid Double Rollback

**Wrong pattern:**
```csharp
try {
    if (affected == 0) {
        txn.Rollback(); // Manual rollback before throw
        throw new KeyNotFoundException(...);
    }
    txn.Commit();
} catch {
    txn.Rollback(); // Second rollback = SqliteException!
    throw;
}
```

**Correct pattern:**
```csharp
try {
    if (affected == 0)
        throw new KeyNotFoundException(...); // Let catch handle rollback
    txn.Commit();
} catch {
    txn.Rollback(); // Single rollback path
    throw;
}
```

### 5. IAsyncEnumerable for List Operations

Use `IAsyncEnumerable<T>` for list methods — avoids loading all rows into memory:

```csharp
public async IAsyncEnumerable<Profile> ListProfilesAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    using var cmd = _connection.CreateCommand();
    cmd.CommandText = "SELECT ... ORDER BY CreatedAt ASC;";
    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
        yield return MapRow(reader);
}
```

### 6. In-Memory SQLite for Tests

Each test gets an isolated named in-memory database:

```csharp
var dbName = $"hermes-test-{Guid.NewGuid():N}";
var connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
var service = new ProfileService(connectionString);
await service.InitializeAsync();
```

`Cache=Shared` allows a second connection to the same in-memory database (for direct verification).
Each test has a unique db name — full isolation, no cleanup needed.

### 7. System.CommandLine 2.0.0 CLI Wiring

Use property initializer for `Argument<T>` description (not constructor argument):

```csharp
// Wrong (CS1729):
var arg = new Argument<string>("name", "description");

// Correct:
var arg = new Argument<string>("name") { Description = "description" };

// Option aliases:
var opt = new Option<string?>("--description", new[] { "-d" });
opt.Description = "Optional description";
```

Use `AsynchronousCommandLineAction` subclass for async handlers:

```csharp
private sealed class CreateAction : AsynchronousCommandLineAction
{
    public override async Task<int> InvokeAsync(ParseResult pr, CancellationToken ct = default)
    {
        // ...
        return 0;
    }
}
cmd.Action = new CreateAction(...);
```
