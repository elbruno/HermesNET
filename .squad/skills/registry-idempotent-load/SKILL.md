---
name: "registry-idempotent-load"
description: "File-path-tracked idempotent loading for in-memory registries backed by filesystem directories"
domain: "runtime-services, registry-design"
confidence: "high"
source: "earned — implemented and validated in T14 SkillRegistry (18 tests pass)"
---

## Context

When building an in-memory registry that loads resources from a filesystem directory
(e.g., skill files, plugin manifests, config fragments), you need idempotent load behavior:
calling `LoadFromDirectoryAsync` twice on the same directory must not duplicate entries or throw.

## Patterns

**Track loaded file paths, not just loaded keys:**
```csharp
private readonly HashSet<string> _loadedFiles = new(StringComparer.OrdinalIgnoreCase);

public async Task LoadFromDirectoryAsync(string dir)
{
    await _loadLock.WaitAsync();
    try
    {
        foreach (var file in Directory.GetFiles(dir, "*.md"))
        {
            if (_loadedFiles.Contains(file)) continue; // idempotent skip
            // parse + register
            _loadedFiles.Add(file);
        }
    }
    finally { _loadLock.Release(); }
}
```

**Use a SemaphoreSlim (1,1) load lock:**
Prevents concurrent calls from racing on the same directory. Single-entry lock is sufficient
since loading is I/O-bound, not CPU-bound across multiple threads.

**Separate "loading errors" from "validation errors":**
- `LoadFromDirectoryAsync` throws on hard failures (parse error, duplicate ID)
- Soft issues (unexpected schema version) are recorded in `LoadWarnings: IReadOnlyList<string>`
- `ValidateAsync(id)` performs deep validation on an already-loaded resource

**Non-registered extension filter:** Use `Directory.GetFiles(dir, "*.ext")` rather than
post-filtering — this keeps non-matching files completely out of the load loop.

## Examples

See `src/Hermes.Core/Skills/SkillRegistry.cs` — full implementation with:
- `ConcurrentDictionary<string, SkillDescriptor>` for O(1) lookup
- `HashSet<string> _loadedFiles` for idempotency tracking
- `SemaphoreSlim _loadLock` for thread-safe load operations
- `List<string> _warnings` for non-blocking load observations

18 unit tests in `tests/Hermes.Core.Tests/Skills/SkillRegistryTests.cs` cover:
idempotency, concurrency, large files, BOM handling, duplicate detection, warning emission.

## Anti-Patterns

- **Don't track only by key:** If two different files have the same key (e.g., duplicate ID),
  key-only tracking won't protect against re-loading the second file — you'd silently overwrite
  or miss the duplicate. Always track by file path AND check key uniqueness separately.

- **Don't lock the entire dictionary on reads:** Use `ConcurrentDictionary` for reads; only
  use the semaphore lock for the write-heavy load operation.

- **Don't silently swallow parse errors:** Hard errors (missing required fields, invalid types)
  should throw. Only soft errors (unexpected version, forward-compat warnings) should produce
  warnings and continue loading.
