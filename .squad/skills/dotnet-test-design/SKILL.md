# Skill: .NET Test Design for Agent Runtimes

**Category:** Testing  
**Stack:** .NET 10, xUnit, Coverlet, FluentAssertions  
**Author:** Lambert  
**Date:** 2026-05-22  
**Status:** ✅ Validated in M1; extended for M2

---

## Pattern: Blocked Tests as Living Contracts

When writing tests before interfaces are implemented, use `Skip` to create a living contract that compiles immediately.

```csharp
[Fact(Skip = "Blocked: IProfileManager not yet implemented (T13)")]
public async Task ProfileManager_CreateProfile_ValidRequest_ReturnsProfileWithId()
{
    // Arrange — comment out real calls with TODO
    // var result = await _profileManager.CreateAsync(request);

    // Assert — document expected behavior
    // result.Id.Should().NotBeEmpty();
    await Task.CompletedTask;
}
```

**Why it works:**
- Tests compile immediately → CI stays GREEN
- Test names document the acceptance criteria → no spec drift
- Removing `Skip` activates the contract → implementation acceptance signal
- `Task.CompletedTask` placeholder keeps async pattern correct

---

## Pattern: R2-Style Isolation Gate

For any two-profile isolation test (memory, sessions, skills):

```csharp
[Fact]
public async Task MemoryIsolation_TwoProfiles_NoContamination()
{
    // Arrange — write distinct secrets to each profile
    await WriteMemoryFile("profile-a", "A-secret");
    await WriteMemoryFile("profile-b", "B-secret");

    // Act — load each independently
    var snapshotA = await _loader.GetCuratedSnapshotAsync("profile-a");
    var snapshotB = await _loader.GetCuratedSnapshotAsync("profile-b");

    // Assert — bidirectional isolation
    snapshotA.MemoryContent.Should().Contain("A-secret");
    snapshotA.MemoryContent.Should().NotContain("B-secret");
    snapshotB.MemoryContent.Should().Contain("B-secret");
    snapshotB.MemoryContent.Should().NotContain("A-secret");
}
```

**Key principle:** Test bidirectional isolation in a single test. One-direction tests miss contamination that flows only one way.

---

## Pattern: Error Contract Testing (KeyNotFoundException Standard)

For all store operations on missing IDs (locked in M1-011):

```csharp
[Theory]
[InlineData("nonexistent-id")]
[InlineData("00000000-0000-0000-0000-000000000000")]
public async Task Store_GetByMissingId_ThrowsKeyNotFoundException(string missingId)
{
    Func<Task> act = async () => await _store.GetAsync(missingId);
    await act.Should().ThrowAsync<KeyNotFoundException>();
}
```

**Contract:** `KeyNotFoundException` for missing-ID operations; `ArgumentNullException` for null IDs. Never silent `null` return.

---

## Pattern: Failure Mode Matrix

For each new module, map all failure modes before writing happy-path tests:

| Category | Failure | Expected Behavior |
|----------|---------|------------------|
| Missing input | null ID | `ArgumentNullException` |
| Missing resource | non-existent ID | `KeyNotFoundException` |
| Duplicate resource | same name twice | `InvalidOperationException` or 409 |
| Missing file | MEMORY.md absent | Empty snapshot (no throw) |
| Corrupt file | binary garbage | Empty snapshot or structured error |
| Cross-profile access | wrong profile context | Rejected, never leaked |

Write one test per failure mode before writing any happy-path test.

---

## Pattern: IDisposable for File-Based Tests

For tests that write to disk (memory files, skill fixtures):

```csharp
public class CuratedMemoryLoaderTests : IDisposable
{
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(), $"hermes-test-{Guid.NewGuid()}");

    public CuratedMemoryLoaderTests()
    {
        Directory.CreateDirectory(_workspace);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
            Directory.Delete(_workspace, recursive: true);
    }
}
```

**Why:** Unique GUID workspace per test class instance → no test-to-test cross-contamination even under parallel execution.

---

## Coverage Gate: Per-Module vs. Overall

When M2 raises per-module coverage targets:

```yaml
# In CI, collect coverage scoped to specific namespaces
dotnet test --collect:"XPlat Code Coverage" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include="[Hermes.Core]Hermes.Core.Profiles.*"
```

Run one coverage pass per module. Overall aggregate can mask under-covered modules.

---

## References

- `docs/testing/TEST-CONVENTIONS.md` — naming, AAA, FluentAssertions
- `docs/testing/m2-test-strategy.md` — M2 full strategy
- M1-011 decision — error contract standard (`KeyNotFoundException`)
