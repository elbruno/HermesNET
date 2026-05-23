# Skill: Curated Memory Architecture Pattern

**Domain:** Data/Memory  
**Language:** C# / .NET 10  
**Origin:** HermesNET M2 T17 (Parker)

---

## Pattern: Profile-Scoped Memory Store with SQLite

### Problem

An AI agent runtime needs durable, profile-scoped memory (MEMORY.md, USER.md) that:
- Is always available (no vector store required)
- Never leaks one profile's data to another
- Is human-readable and auditable
- Stays simple to reason about

### Solution

Two SQLite tables with a `(ProfileId, Kind)` unique index as the isolation boundary.

#### Schema

```sql
CREATE TABLE Memory (
    Id        TEXT NOT NULL PRIMARY KEY,
    ProfileId TEXT NOT NULL,
    Kind      TEXT NOT NULL DEFAULT 'memory',  -- 'memory' | 'user_profile'
    Content   TEXT NOT NULL DEFAULT '',
    Format    TEXT NOT NULL DEFAULT 'markdown',
    Version   INTEGER NOT NULL DEFAULT 1,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);
CREATE UNIQUE INDEX idx_memory_profile_kind ON Memory(ProfileId, Kind);

CREATE TABLE UserProfiles (
    Id            TEXT NOT NULL PRIMARY KEY,
    ProfileId     TEXT NOT NULL UNIQUE,
    Data          TEXT NOT NULL DEFAULT '',
    SchemaVersion INTEGER NOT NULL DEFAULT 1,
    CreatedAt     TEXT NOT NULL,
    UpdatedAt     TEXT NOT NULL
);
```

#### Interface Contract

```csharp
public interface IMemoryService
{
    Task<MemoryContext> LoadMemoryAsync(string profileId, CancellationToken ct = default);
    Task UpdateMemoryAsync(string profileId, string content, CancellationToken ct = default);
    Task<UserProfileData> LoadUserProfileAsync(string profileId, CancellationToken ct = default);
    Task UpdateUserProfileAsync(string profileId, string data, CancellationToken ct = default);
    Task<MemorySchema> GetMemorySchemaAsync(CancellationToken ct = default);
}
```

#### Upsert Pattern (version-safe write)

```sql
INSERT INTO Memory (Id, ProfileId, Kind, Content, Format, Version, CreatedAt, UpdatedAt)
VALUES (@id, @profileId, @kind, @content, 'markdown', 1, @now, @now)
ON CONFLICT(ProfileId, Kind) DO UPDATE SET
    Content   = excluded.Content,
    Version   = Memory.Version + 1,
    UpdatedAt = excluded.UpdatedAt;
```

#### Mandatory profiling guard (service layer)

```csharp
private static void ValidateProfileId(string profileId)
{
    if (string.IsNullOrWhiteSpace(profileId))
        throw new ArgumentException("profileId must not be null or empty.", nameof(profileId));
}
```

### Key Principles

1. **Schema-first:** Define the unique index before writing any application code. The DB enforces the invariant, not just the app.
2. **profileId is ALWAYS a WHERE predicate.** No query omits it. Ever.
3. **Return Empty, not null.** Missing rows → `MemoryContext.Empty(profileId)`, not null.
4. **Version counter is per-profile.** Increments on every write via the ON CONFLICT upsert.
5. **Schema validation at service boundary.** Content size checked before hitting the DB.
6. **MemorySchema is injectable.** Allows test overrides (smaller cap) and runtime configuration.

### Test Pattern: Adversarial Isolation

```csharp
// Write evil profile's data through the service
await _store.UpdateMemoryAsync("evil", "Evil data that should not leak");

// The legitimate profile's query must NOT be contaminated
var ctx = await _store.LoadMemoryAsync("alice");
ctx.Content.Should().NotContain("Evil");
```

### Latency Characteristics

- Load 2 KB MEMORY.md from in-memory SQLite: **< 1 ms** (measured)
- Load 2 KB MEMORY.md from file SQLite: **< 10 ms** (expected)
- Target gate: **< 50 ms** (R2 checkpoint requirement)

### Migration Strategy

SQL scripts in `src/Hermes.Core/Data/Migrations/` with sequential numbering.  
Each script is idempotent (`CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`).  
FK to profiles table added in a separate migration after the profiles table exists.

### When to Use This Pattern

- Any feature that needs per-entity isolated key/value or document storage in SQLite
- Profile-scoped configuration, preferences, or state that must never cross entity boundaries
- Durable but human-readable agent context injection
