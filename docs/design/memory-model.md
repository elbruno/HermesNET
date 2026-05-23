# Hermes.NET Curated Memory Model

**Status:** Design — M2 Week 1  
**Author:** Parker (Data/Memory Dev)  
**Date:** 2026-05-22  
**Risk checkpoint:** R2 — Cross-profile memory contamination

---

## 1. Purpose

Curated memory is the signature Hermes concept that makes an agent feel like it _knows_ the user and the project context. Unlike RAG/vector retrieval (which surfaces semantically similar text on demand), curated memory is:

- **Deliberate** — the agent or user explicitly writes entries, not search results.
- **Durable** — entries persist across sessions and restarts.
- **Profile-scoped** — each profile owns its own `MEMORY.md` and `USER.md`; there is no shared pool.
- **Human-readable** — Markdown files that a developer can inspect, edit, and version-control.

This document defines the canonical schema, lifecycle, scoping rules, and migration strategy for curated memory in Hermes.NET.

---

## 2. Two-File Model

### 2.1 `MEMORY.md` — Environmental / Project Facts

**What goes here:** Durable facts about the project, codebase, environment, or task context that the agent should know regardless of which session is active. Think of it as the agent's project notebook.

| Attribute | Value |
|---|---|
| Scope | Per-profile |
| Audience | Agent (injected as system context) |
| Format | Markdown — free-form sections |
| Retention | Until explicitly cleared or overwritten |
| Versioning | `version` integer, incremented on every write |
| Update trigger | Agent writes, user writes via CLI or API |

**Example content:**
```markdown
## Project Context
- Stack: .NET 10, ASP.NET Core, SQLite
- Repo: github.com/elbruno/HermesNET
- Key constraint: Must run offline with local Ollama

## Architecture Decisions
- Provider abstraction: IChatClient
- Test framework: xUnit + FluentAssertions
- OTel baseline P95: 55 ms (2026-05-22)
```

**What does NOT go here:** User preferences, communication style, session-specific state.

---

### 2.2 `USER.md` — User Preferences and Profile State

**What goes here:** Per-user interaction norms, preferences, communication style, and profile-level behavioral tuning. The agent uses this to tailor its tone and workflow to the specific user.

| Attribute | Value |
|---|---|
| Scope | Per-profile |
| Audience | Agent (injected as system context) |
| Format | Markdown — key/value or free-form sections |
| Retention | Until explicitly updated |
| Versioning | `schema_version` integer |
| Update trigger | User profile updates, explicit preference writes |

**Example content:**
```markdown
## Identity
- Name: Bruno
- Role: Principal Engineer / Advocate

## Preferences
- Response style: concise, schema-first
- Code language: C# (.NET 10)
- Avoid: lengthy preambles, unsolicited opinions

## Interaction Norms
- Always show code examples
- Use Markdown tables for comparisons
```

**What does NOT go here:** Project facts, architecture decisions, codebase state.

---

## 3. How Curated Memory Differs from RAG / Vector Retrieval

| Dimension | Curated Memory | Retrieval Memory (RAG) |
|---|---|---|
| Content selection | Explicit human/agent writes | Automated similarity search |
| Shape | Structured Markdown | Unstructured chunks + embeddings |
| Injection timing | Always injected at session start | On-demand per query |
| Scope enforcement | Hard profile_id boundary at DB level | Typically namespace/collection scoped |
| Editability | Human-editable files | Requires re-embedding to update |
| Availability | Always available, no vector store needed | Requires vector store |
| Trust level | High — deliberately curated | Lower — surfaced by similarity, not intent |

Curated memory **cannot replace** retrieval for large knowledge bases. It is intended for a small, high-signal context payload (< 4 KB typical). Retrieval memory is an optional complementary layer.

---

## 4. Profile Scoping Rules

**Rule 1: Hard profile isolation at the database layer.**  
Every memory row and user profile row carries a `profile_id` foreign-key constraint. Queries without a `profile_id` predicate are rejected at the service layer.

**Rule 2: No cross-profile read path.**  
`LoadMemoryAsync(profileId)` only returns rows where `profile_id = @profileId`. There is no "global" memory namespace.

**Rule 3: No cross-profile write path.**  
`UpdateMemoryAsync(profileId, ...)` only modifies rows matching `profile_id = @profileId`. Passing an incorrect `profileId` returns not-found, it does not silently write to another profile.

**Rule 4: Profile deletion cascades.**  
When a profile is deleted (future `IProfileService`), all `Memory` and `UserProfile` rows for that `profile_id` are removed. Orphaned rows are prevented by FK cascade delete.

**Rule 5: Schema version is per-profile.**  
Each profile tracks its own `schema_version`. A schema upgrade for profile A does not affect profile B.

---

## 5. Example: Two Profiles with Distinct Memory

### File Structure

```
/workspace/profiles/
  alice/
    profile.json
    MEMORY.md           ← "Alice's project: HermesNET, stack .NET 10"
    USER.md             ← "Alice prefers concise responses"
  bob/
    profile.json
    MEMORY.md           ← "Bob's project: LegacyApp, stack .NET Framework 4.8"
    USER.md             ← "Bob prefers verbose step-by-step explanations"
```

### Database State

| id | profile_id | kind | content | version |
|---|---|---|---|---|
| uuid-1 | alice | memory | Alice's project: HermesNET... | 1 |
| uuid-2 | alice | user_profile | Alice prefers concise... | 1 |
| uuid-3 | bob | memory | Bob's project: LegacyApp... | 1 |
| uuid-4 | bob | user_profile | Bob prefers verbose... | 1 |

### Query Behavior

```csharp
var aliceCtx = await memoryService.LoadMemoryAsync("alice");
// → content: "Alice's project: HermesNET..." — Bob's rows are NOT returned

var bobCtx = await memoryService.LoadMemoryAsync("bob");
// → content: "Bob's project: LegacyApp..." — Alice's rows are NOT returned

await memoryService.UpdateMemoryAsync("alice", new MemoryUpdate("new content for alice"));
// → Only alice's row updated. Bob's row is NOT touched.
```

---

## 6. Data Schema

### 6.1 `Memory` table

```sql
CREATE TABLE Memory (
    Id             TEXT NOT NULL PRIMARY KEY,
    ProfileId      TEXT NOT NULL,
    Kind           TEXT NOT NULL DEFAULT 'memory',   -- 'memory' | 'user_profile'
    Content        TEXT NOT NULL DEFAULT '',
    Format         TEXT NOT NULL DEFAULT 'markdown',
    Version        INTEGER NOT NULL DEFAULT 1,
    CreatedAt      TEXT NOT NULL,
    UpdatedAt      TEXT NOT NULL,
    FOREIGN KEY (ProfileId) REFERENCES AgentProfiles(Id) ON DELETE CASCADE
);

CREATE INDEX idx_memory_profile ON Memory(ProfileId);
CREATE UNIQUE INDEX idx_memory_profile_kind ON Memory(ProfileId, Kind);
```

The `(ProfileId, Kind)` unique index enforces exactly one `MEMORY.md` and one `USER.md` per profile. Upsert semantics are used on write.

### 6.2 `UserProfiles` table

```sql
CREATE TABLE UserProfiles (
    Id             TEXT NOT NULL PRIMARY KEY,
    ProfileId      TEXT NOT NULL UNIQUE,
    Data           TEXT NOT NULL DEFAULT '{}',
    SchemaVersion  INTEGER NOT NULL DEFAULT 1,
    CreatedAt      TEXT NOT NULL,
    UpdatedAt      TEXT NOT NULL,
    FOREIGN KEY (ProfileId) REFERENCES AgentProfiles(Id) ON DELETE CASCADE
);

CREATE INDEX idx_userprofiles_profile ON UserProfiles(ProfileId);
```

---

## 7. Interface Contract Summary

```csharp
public interface IMemoryService
{
    // Returns the MEMORY.md snapshot for the given profile.
    // Throws ArgumentException if profileId is null/empty.
    // Returns empty MemoryContext (not null) if no memory has been written yet.
    Task<MemoryContext> LoadMemoryAsync(string profileId, CancellationToken ct = default);

    // Writes/upserts the MEMORY.md content for the given profile.
    // Increments version on every write.
    Task UpdateMemoryAsync(string profileId, string content, CancellationToken ct = default);

    // Returns the USER.md snapshot (preferences and profile state).
    Task<UserProfileData> LoadUserProfileAsync(string profileId, CancellationToken ct = default);

    // Writes/upserts the USER.md content for the given profile.
    Task UpdateUserProfileAsync(string profileId, string data, CancellationToken ct = default);

    // Returns the validation schema — used for content validation before writes.
    Task<MemorySchema> GetMemorySchemaAsync(CancellationToken ct = default);
}
```

---

## 8. Lifecycle and Retention

| Event | Memory behaviour |
|---|---|
| Profile created | Empty `Memory` and `UserProfile` rows inserted (lazy or explicit) |
| Session started | `LoadMemoryAsync` + `LoadUserProfileAsync` called; content injected into system prompt |
| Agent writes memory | `UpdateMemoryAsync` with new content; version incremented |
| Profile deleted | All `Memory` and `UserProfile` rows cascade-deleted |
| Schema upgraded | `Version`/`SchemaVersion` incremented; old format supported via migration script |

---

## 9. Migration Strategy

Migrations are SQL scripts in `src/Hermes.Core/Data/Migrations/` with sequential numbering (`001_`, `002_`, ...). Each migration is idempotent (`CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`).

Memory schema is added in `002_MemorySchema.sql`. If the M1 `AgentProfiles` table does not exist (profiles are deferred to M2 T13), the `Memory` table uses a soft FK approach: `ProfileId` carries a `NOT NULL` constraint and `idx_memory_profile` index but the FK reference is only activated when the `AgentProfiles` table is present.

---

## 10. R2 Risk Checkpoint

**R2 is GREEN when all of the following are true:**

1. `LoadMemoryAsync("alice")` never returns rows owned by `bob`.
2. `UpdateMemoryAsync("alice", ...)` never modifies rows owned by `bob`.
3. `LoadUserProfileAsync("alice")` never returns `bob`'s preferences.
4. Adversarial test: Insert a row with `profile_id = 'evil'` directly in the DB; confirm `LoadMemoryAsync("alice")` still returns only Alice's data.
5. Latency baseline: `LoadMemoryAsync` for a 2 KB `MEMORY.md` content completes in < 50 ms on local SQLite (in-memory test DB).

**R2 test file:** `tests/Hermes.Core.Tests/Memory/MemoryIsolationTests.cs`

---

## 11. Open Questions / Future Work

| Item | Status |
|---|---|
| Should MEMORY.md support section-level merging (not just full replace)? | Deferred to M3 |
| Encrypted memory at rest for sensitive profiles? | Deferred post-MVP |
| Retrieval memory adapter interface (`IRetrievalMemoryProvider`) | M3 scope |
| Memory diff/audit trail (who wrote what and when)? | M3 scope |
| Profile-level memory quotas (max content size)? | M3 scope |
