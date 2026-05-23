# M2 MVP Runtime — Test Strategy

**Status:** ✅ ACTIVE — Governs all M2 test work  
**Date:** 2026-05-22  
**Owner:** Lambert (Tester)  
**Milestone:** M2 — MVP Runtime (May 26 – Jun 6)

---

## Executive Summary

M2 delivers the first public MVP: profiles, sessions, markdown skills, curated memory, native tool registry, REST API (SSE), and CLI (`hermes chat`, `hermes session`, `hermes skill`, `hermes memory`). This document defines what "done" means for every deliverable, provides explicit acceptance criteria for every quality gate, and specifies the test scaffold Dallas and Parker must validate against.

Tests drive implementation. Dallas and Parker write code to satisfy these criteria; they do not write tests after.

---

## 1. Unit Test Strategy

### Coverage Target

| Module | Target | Measurement Command | Hard Gate? |
|--------|--------|---------------------|------------|
| `Hermes.Core/Profiles/` | ≥ 85% branch | `dotnet test --collect:"XPlat Code Coverage"` | ✅ Yes |
| `Hermes.Core/Session/` | ≥ 85% branch | `dotnet test --collect:"XPlat Code Coverage"` | ✅ Yes |
| `Hermes.Core/Skills/` | ≥ 85% branch | `dotnet test --collect:"XPlat Code Coverage"` | ✅ Yes |
| `Hermes.Core/Memory/` | ≥ 85% branch | `dotnet test --collect:"XPlat Code Coverage"` | ✅ Yes |

> Note: M1 exit was 87.5% on Hermes.Core overall. M2 raises the per-module bar to 85% branch on the four new core modules.

### What Gets Unit Tests

Every public method in the following classes requires unit test coverage:

- `ProfileManager` / `IProfileManager` — all CRUD operations, default profile resolution, switch logic
- `SessionCoordinator` / `ISessionCoordinator` — create, read, update, delete, list, profile scoping
- `MarkdownSkillParser` / `ISkillRegistry` — parse, resolve, validate, list
- `CuratedMemoryLoader` / `IMemoryCoordinator` — load snapshot, per-profile scoping, update
- `ToolRegistry` — register, invoke, policy-check (read-only sandbox enforcement)

### What Does NOT Require Unit Tests in M2

- `Hermes.Host` wiring / DI registration (covered by integration tests)
- REST API controllers (covered by contract tests)
- CLI command handlers (covered by smoke tests)
- Provider-specific adapters (M1 coverage carries over)

### Test Organization

```
tests/Hermes.Core.Tests/
├── Profiles/
│   ├── ProfileManagerTests.cs
│   └── ProfileFixtures.cs
├── Sessions/
│   ├── SessionCoordinatorTests.cs     (new M2 tests; extends M1 SessionStoreTests)
│   └── SessionFixtures.cs
├── Skills/
│   ├── SkillParserTests.cs            (M1 extended)
│   ├── SkillRegistryTests.cs          (new M2)
│   └── fixtures/
│       ├── valid-skill.yml
│       ├── malformed-empty.yml
│       ├── malformed-missing-name.yml
│       ├── malformed-missing-description.yml
│       ├── malformed-missing-type.yml
│       └── malformed-invalid-yaml.yml
├── Memory/
│   ├── CuratedMemoryLoaderTests.cs    (new M2)
│   └── MemoryFixtures.cs
├── Integration/
│   ├── R1IntegrationDrift.cs          (M1 — must not regress)
│   ├── ChatClientFactoryTests.cs      (M1 — must not regress)
│   ├── ProfileSessionScopingTests.cs  (new M2 — R2 gate)
│   └── MemoryIsolationTests.cs        (new M2 — R2 gate)
└── Usings.cs
```

---

## 2. Integration Test Strategy

### Profile Isolation

Every profile creates a logically isolated namespace. Integration tests must verify:

1. Sessions created under Profile A are not retrievable under Profile B
2. Skills enabled for Profile A do not appear in Profile B's skill list
3. Curated memory (`MEMORY.md`, `USER.md`) for Profile A is not accessible from Profile B
4. Deleting Profile A cleans up its sessions, skills, and memory entries

### Session Scoping

Sessions must be scoped to their parent profile. Integration tests must verify:

1. `GET /api/sessions?profileId=A` returns only Profile A's sessions
2. Session created under Profile A cannot be sent a message as Profile B
3. Session listing by profile returns correct count after multi-profile inserts

### Memory Cross-Contamination Detection (R2 Gate)

This is a hard R2 risk checkpoint. Parker owns execution; Lambert owns pass/fail verdict.

**Test: `MemoryIsolation_TwoProfiles_NoContamination`**

```
Arrange: Create Profile A with MEMORY.md content "A-secret"
         Create Profile B with MEMORY.md content "B-secret"
Act:     Load curated memory snapshot for Profile A
Assert:  Snapshot content does NOT contain "B-secret"
         Snapshot content DOES contain "A-secret"

Act:     Load curated memory snapshot for Profile B
Assert:  Snapshot content does NOT contain "A-secret"
         Snapshot content DOES contain "B-secret"
```

R2 is GREEN only when this test (and its variants) pass on every CI run.

---

## 3. End-to-End Test Strategy

### CLI First-Run (Smoke Test)

Each of the four new CLI commands must succeed on a clean repo clone:

| Command | Expected Outcome | Failure Condition |
|---------|-----------------|-------------------|
| `hermes chat --profile default --message "ping"` | Response text + Session ID printed | Exit code ≠ 0 or no session ID in output |
| `hermes session list` | Lists sessions (empty OK) or `No sessions found` | Exit code ≠ 0 or unhandled exception |
| `hermes skill list` | Lists skills or `No skills installed` | Exit code ≠ 0 or unhandled exception |
| `hermes memory show --profile default` | Prints memory snapshot or `No memory entries` | Exit code ≠ 0 or unhandled exception |

All four must pass. This is a hard CLI quality gate.

### REST API Smoke Tests

The following endpoints must return the correct status codes and schema:

| Endpoint | Method | Expected Status | Minimum Response Fields |
|----------|--------|-----------------|------------------------|
| `GET /healthz` | GET | 200 | `status: "healthy"` |
| `GET /api/profiles` | GET | 200 | Array (empty OK) |
| `POST /api/profiles` | POST | 201 | `id`, `name` |
| `GET /api/profiles/{id}` | GET | 200 | `id`, `name`, `defaultModel` |
| `POST /api/sessions` | POST | 201 | `id`, `profileId` |
| `GET /api/sessions/{id}` | GET | 200 | `id`, `profileId`, `status` |
| `POST /api/sessions/{id}/messages` | POST | 200 | Response text or SSE stream |
| `GET /api/skills` | GET | 200 | Array |
| `POST /api/skills/install` | POST | 201 | `id`, `name` |
| `GET /api/tools` | GET | 200 | Array |
| `GET /metrics` | GET | 200 | Prometheus text format |

### Full Workflow Test

One end-to-end test covering the canonical user path:

```
1. Create profile "e2e-test"
2. Install a valid skill
3. Create session under "e2e-test"
4. Send message: "Hello, what skills do you have?"
5. Receive non-empty response
6. List session turns: at least 1 turn present
7. Load memory snapshot for "e2e-test": non-null
8. Delete profile "e2e-test"
9. Verify session no longer accessible (404 or profile-scoped exclusion)
```

---

## 4. Failure Mode Tests

These are explicit edge cases that MUST be tested before M2 ships. Each has an expected behavior contract.

### Profile Edge Cases

| Scenario | Expected Behavior | Test Method |
|----------|------------------|-------------|
| Create profile with duplicate name | 409 Conflict (REST) / clear exception (unit) | `ProfileManager_CreateProfile_DuplicateName_ThrowsConflictException` |
| Get non-existent profile ID | 404 Not Found (REST) / `KeyNotFoundException` (unit) | `ProfileManager_GetProfile_NonExistentId_ThrowsKeyNotFoundException` |
| Delete profile with active sessions | Sessions cleaned up OR explicit error; never silent data leak | `ProfileManager_DeleteProfile_WithActiveSessions_CleansUpOrThrows` |
| Switch to non-existent profile | Clear error, not silent fall-through to default | `ProfileManager_SwitchProfile_NonExistentTarget_ThrowsNotFoundException` |
| List profiles when none exist | Returns empty collection (no exception) | `ProfileManager_ListProfiles_WhenEmpty_ReturnsEmptyCollection` |

### Session Edge Cases

| Scenario | Expected Behavior | Test Method |
|----------|------------------|-------------|
| Get session with wrong profile context | Rejected (not returned) | `SessionCoordinator_GetSession_WrongProfile_ThrowsOrReturnsNull` |
| Update non-existent session | `KeyNotFoundException` (M1-011 contract) | `SessionCoordinator_UpdateSession_NonExistentId_ThrowsKeyNotFoundException` |
| Delete non-existent session | `KeyNotFoundException` (M1-011 contract) | `SessionCoordinator_DeleteSession_NonExistentId_ThrowsKeyNotFoundException` |
| Concurrent session updates | No race condition; last-writer-wins or optimistic concurrency error | `SessionCoordinator_ConcurrentUpdates_NoRaceCondition` |
| Create session with null profile ID | `ArgumentNullException` | `SessionCoordinator_CreateSession_NullProfileId_ThrowsArgumentNullException` |

### Skill Edge Cases

| Scenario | Expected Behavior | Test Method |
|----------|------------------|-------------|
| YAML with invalid syntax | `SkillParseException` | `SkillParser_InvalidYaml_ThrowsSkillParseException` |
| YAML missing `name` field | `SkillParseException` with field name in message | `SkillParser_MissingNameField_ThrowsSkillParseException` |
| YAML missing `description` field | `SkillParseException` with field name in message | `SkillParser_MissingDescriptionField_ThrowsSkillParseException` |
| YAML missing `type` field | `SkillParseException` with field name in message | `SkillParser_MissingTypeField_ThrowsSkillParseException` |
| Empty YAML file | `SkillParseException` | `SkillParser_EmptyYaml_ThrowsSkillParseException` |
| Null YAML input | `ArgumentNullException` or `SkillParseException` | `SkillParser_NullInput_ThrowsException` |
| Valid skill with all required fields | Returns populated `SkillDefinition` | `SkillParser_ValidSkillYaml_ReturnsSkillDefinition` |
| Install duplicate skill | Upsert (idempotent) or `ConflictException` — must be documented | `SkillRegistry_InstallDuplicateSkill_IsDeterministic` |

### Memory Edge Cases

| Scenario | Expected Behavior | Test Method |
|----------|------------------|-------------|
| `MEMORY.md` file missing for profile | Returns empty snapshot (no exception) | `CuratedMemoryLoader_MissingMemoryFile_ReturnsEmptySnapshot` |
| `USER.md` file missing for profile | Returns empty snapshot (no exception) | `CuratedMemoryLoader_MissingUserFile_ReturnsEmptySnapshot` |
| `MEMORY.md` file corrupt (not valid Markdown) | Returns empty snapshot OR structured error; never crashes | `CuratedMemoryLoader_CorruptMemoryFile_GracefullyDegraded` |
| Memory update with null content | `ArgumentNullException` | `CuratedMemoryLoader_UpdateWithNullContent_ThrowsArgumentNullException` |
| Cross-profile memory access attempt | Profile A cannot read Profile B's entries | `MemoryIsolation_TwoProfiles_NoContamination` (see R2) |

### REST API Edge Cases

| Scenario | Expected Behavior |
|----------|------------------|
| `POST /api/profiles` with missing `name` | 400 Bad Request with validation details |
| `POST /api/sessions` with non-existent `profileId` | 404 Not Found |
| `POST /api/sessions/{id}/messages` with missing `content` | 400 Bad Request |
| `GET /api/profiles/{id}` with invalid GUID format | 400 Bad Request |
| `DELETE /api/profiles/{id}` for non-existent ID | 404 Not Found |

---

## 5. Performance Smoke Tests

### Session Load Time

M2 adds profiles and memory loading to the session startup path. These must not degrade M1's P95 baseline.

| Metric | M1 Baseline | M2 Target | Method |
|--------|-------------|-----------|--------|
| P95 turn latency (local provider) | 51 ms | ≤ 61 ms (< 20% overhead) | `docs/benchmarks/m2-perf-baseline.md` |
| P95 session create time | Not measured in M1 | ≤ 20 ms | Benchmark in `Hermes.Benchmarks` |
| P95 memory snapshot load | Not measured in M1 | ≤ 10 ms | Benchmark in `Hermes.Benchmarks` |
| P95 REST API response time (`POST /api/sessions`) | Not measured in M1 | ≤ 100 ms | Load test in `Hermes.LoadTests` |

### R4 OTel Overhead Gate

Parker measures P95 turn latency with ALL new M2 spans active. Gate: ≤ 61 ms P95 (< 20% regression vs M1 51 ms).

If P95 > 61 ms:
1. Parker applies adaptive sampling on non-critical spans
2. Re-measure and document delta
3. If still > 61 ms after tuning, Ripley reduces span density and documents limitation

---

## 6. Quality Gate Mapping

Each M2 quality gate maps to one or more explicit tests that verify it. Gates without a linked test are RED by default.

| Gate | Target | Linked Tests | Hard? |
|------|--------|--------------|-------|
| Unit coverage — Profiles | ≥ 85% branch | All tests in `Tests/Profiles/` | ✅ Yes |
| Unit coverage — Sessions | ≥ 85% branch | All tests in `Tests/Sessions/` | ✅ Yes |
| Unit coverage — Skills | ≥ 85% branch | All tests in `Tests/Skills/` | ✅ Yes |
| Unit coverage — Memory | ≥ 85% branch | All tests in `Tests/Memory/` | ✅ Yes |
| OTel coverage (new flows) | ≥ 90% new paths emit traces | Parker manual walk + `HermesTelemetryTests.cs` | ✅ Yes |
| Latency regression | ≤ 61 ms P95 | `Hermes.Benchmarks` + `docs/benchmarks/m2-perf-baseline.md` | ✅ Yes |
| CLI smoke tests | All 4 commands succeed | CLI first-run matrix (Section 3) | ✅ Yes |
| REST API contract tests | All endpoints tested; OpenAPI spec generated | `Hermes.Integration.Tests/` REST contract tests | ✅ Yes |
| Skill YAML validation | Malformed YAML rejected; valid skills execute | `SkillParser_*` test series (7 cases) | ✅ Yes |
| Memory profile scoping | No cross-profile contamination | `MemoryIsolation_TwoProfiles_NoContamination` (R2) | ✅ Yes |
| M1 regression | 100% of M1 exit criteria still pass | `dotnet test` full suite (50 M1 tests) | ✅ Yes |

### CI Enforcement

```yaml
# .github/workflows/ci.yml addition for M2 coverage gates
- name: Enforce M2 Coverage Gates
  run: |
    dotnet test --collect:"XPlat Code Coverage" \
      -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include="[Hermes.Core]Hermes.Core.Profiles.*,[Hermes.Core]Hermes.Core.Session.*,[Hermes.Core]Hermes.Core.Skills.*,[Hermes.Core]Hermes.Core.Memory.*"
```

Minimum threshold configuration in `Directory.Build.props`:

```xml
<PropertyGroup>
  <!-- M2 coverage gate: 85% branch on core modules -->
  <CoverageThresholdType>Branch</CoverageThresholdType>
  <CoverageThresholdValue>85</CoverageThresholdValue>
</PropertyGroup>
```

---

## 7. Test Scaffold — M2 Subdirectory Structure

### New Directories Required

```
tests/Hermes.Core.Tests/
├── Profiles/                  ← NEW — create before T13
│   ├── ProfileManagerTests.cs
│   └── ProfileFixtures.cs
├── Memory/                    ← NEW — create before T15
│   ├── CuratedMemoryLoaderTests.cs
│   └── MemoryFixtures.cs
├── Sessions/                  ← EXTEND from M1 Session/
│   └── SessionCoordinatorTests.cs
└── Skills/                    ← EXTEND from M1 Skills/
    └── SkillRegistryTests.cs
```

### Template: ProfileManagerTests.cs

```csharp
namespace Hermes.Core.Tests.Profiles;

public class ProfileManagerTests : IAsyncLifetime
{
    private IProfileManager _profileManager = null!;
    private readonly ITestOutputHelper _output;

    public ProfileManagerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        // Initialize with in-memory store
        _profileManager = new ProfileManager(/* in-memory store */);
        await _profileManager.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        // Cleanup
    }

    [Fact]
    public async Task ProfileManager_CreateProfile_ReturnsNewProfileWithId()
    {
        // Arrange
        var request = new CreateProfileRequest { Name = "test-profile", DefaultModel = "ollama:llama3" };

        // Act
        var profile = await _profileManager.CreateAsync(request);

        // Assert
        profile.Should().NotBeNull();
        profile.Id.Should().NotBeEmpty();
        profile.Name.Should().Be("test-profile");
        _output.WriteLine($"Created profile: {profile.Id}");
    }

    [Fact]
    public async Task ProfileManager_GetProfile_NonExistentId_ThrowsKeyNotFoundException()
    {
        // Act
        Func<Task> act = async () => await _profileManager.GetAsync(Guid.NewGuid().ToString());

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ProfileManager_CreateProfile_DuplicateName_ThrowsConflictException()
    {
        // Arrange
        var request = new CreateProfileRequest { Name = "duplicate" };
        await _profileManager.CreateAsync(request);

        // Act
        Func<Task> act = async () => await _profileManager.CreateAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ProfileManager_ListProfiles_WhenEmpty_ReturnsEmptyCollection()
    {
        // Act
        var profiles = await _profileManager.ListAsync();

        // Assert
        profiles.Should().BeEmpty();
    }

    [Fact]
    public async Task ProfileManager_DeleteProfile_RemovesFromStore()
    {
        // Arrange
        var profile = await _profileManager.CreateAsync(new CreateProfileRequest { Name = "to-delete" });

        // Act
        await _profileManager.DeleteAsync(profile.Id);

        // Assert
        Func<Task> act = async () => await _profileManager.GetAsync(profile.Id);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
```

### Template: CuratedMemoryLoaderTests.cs

```csharp
namespace Hermes.Core.Tests.Memory;

public class CuratedMemoryLoaderTests
{
    private readonly CuratedMemoryLoader _loader;
    private readonly string _testWorkspace;
    private readonly ITestOutputHelper _output;

    public CuratedMemoryLoaderTests(ITestOutputHelper output)
    {
        _output = output;
        _testWorkspace = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testWorkspace);
        _loader = new CuratedMemoryLoader(_testWorkspace);
    }

    [Fact]
    public async Task CuratedMemoryLoader_MissingMemoryFile_ReturnsEmptySnapshot()
    {
        // Arrange — no MEMORY.md file created

        // Act
        var snapshot = await _loader.GetCuratedSnapshotAsync("profile-a");

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.MemoryContent.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task MemoryIsolation_TwoProfiles_NoContamination()
    {
        // Arrange
        await WriteMemoryFile("profile-a", "A-secret context");
        await WriteMemoryFile("profile-b", "B-secret context");

        // Act
        var snapshotA = await _loader.GetCuratedSnapshotAsync("profile-a");
        var snapshotB = await _loader.GetCuratedSnapshotAsync("profile-b");

        // Assert — no contamination
        snapshotA.MemoryContent.Should().Contain("A-secret context");
        snapshotA.MemoryContent.Should().NotContain("B-secret context");

        snapshotB.MemoryContent.Should().Contain("B-secret context");
        snapshotB.MemoryContent.Should().NotContain("A-secret context");
    }

    private async Task WriteMemoryFile(string profileId, string content)
    {
        var dir = Path.Combine(_testWorkspace, "profiles", profileId);
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "MEMORY.md"), content);
    }
}
```

---

## 8. M1 Regression Requirements

M2 must not break any M1 passing tests. The full M1 suite (50 tests) must remain GREEN at M2 exit.

CI enforces this automatically: `dotnet test` runs all test projects. Any M1 test regression is a M2 blocker.

Critical M1 tests that M2 code must not regress:

- `ChatClientFactory_*` (6 tests) — provider abstraction
- `SessionStore_*` (17 tests) — SQLite persistence
- `SkillParser_*` (6 tests) — YAML validation
- `R1IntegrationDrift.cs` (3 tests) — E2E integration
- `HermesTelemetry_*` (5 tests) — OTel instrumentation
- `SessionLoadTest.cs` (1 test) — P95 load gate

---

## 9. Test Schedule

| Date | Action | Owner |
|------|--------|-------|
| 2026-05-22 | This document finalized | Lambert |
| 2026-05-26 | M2 Day 1: Dallas starts T13 (Profile CRUD); scaffold `tests/Hermes.Core.Tests/Profiles/` | Dallas + Lambert |
| 2026-05-26 | Parker starts T15 (memory); scaffold `tests/Hermes.Core.Tests/Memory/` | Parker + Lambert |
| 2026-05-28 | R2 gate: run `MemoryIsolation_TwoProfiles_NoContamination`; Lambert issues verdict | Lambert |
| 2026-05-28 | R4 gate: P95 latency benchmark after new spans wired; Lambert reviews result | Parker + Lambert |
| 2026-06-02 | Week 2 complete: 85% branch coverage on all four modules verified | Lambert |
| 2026-06-06 | T22: REST contract tests complete; OpenAPI spec reviewed | Dallas + Lambert |
| 2026-06-09 | T24 Go/No-Go: Lambert certifies all quality gates GREEN before Ripley review | Lambert |

---

## References

- M2 Kickoff: `.squad/decisions/m2-kickoff.md`
- M1 Quality Gates: `docs/testing/M1-QUALITY-GATES.md`
- Test Conventions: `docs/testing/TEST-CONVENTIONS.md`
- Test Framework: `docs/testing/TEST-FRAMEWORK.md`
- M2 Plan: `docs/research/plan.md` (MVP Runtime section)
