# Skill: Cache-Backed Service Loader Pattern

**Domain:** Data/Memory / Application Architecture  
**Language:** C# / .NET 10  
**Origin:** HermesNET M2 T15 (Parker)

---

## Pattern: Cached Application-Layer Loader Over an IService Contract

### Problem

A storage-backed service (`IMemoryService`, `ISessionService`, etc.) is called frequently at
session start and on every agent turn. Direct calls every time add unnecessary I/O. A local cache
is needed but must be:
- Profile/entity-scoped (no cross-entity cache pollution)
- Invalidated on write (stale reads after an update are a correctness bug)
- Thread-safe under concurrent async callers

### Solution

Pair a **loader** (cached read path) with an **update handler** (write + invalidate path). The
loader holds the cache; the handler holds a reference to the loader for invalidation.

#### Loader (read + cache)

```csharp
public sealed class CuratedMemoryLoader
{
    private readonly IMemoryService _svc;
    private readonly Dictionary<string, MemoryContext> _cache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<MemoryContext> LoadMemoryAsync(string profileId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(profileId, out var cached)) return cached;
            var result = await _svc.LoadMemoryAsync(profileId, ct);
            _cache[profileId] = result;
            return result;
        }
        finally { _lock.Release(); }
    }

    internal void InvalidateCache(string profileId) => _cache.Remove(profileId);
}
```

#### Update Handler (write + invalidate)

```csharp
public sealed class MemoryUpdateHandler
{
    private readonly IMemoryService _svc;
    private readonly CuratedMemoryLoader? _loader;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task UpdateMemoryAsync(string profileId, string content, CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            await _svc.UpdateMemoryAsync(profileId, content, ct);
            _loader?.InvalidateCache(profileId);   // always invalidate after write
        }
        finally { _writeLock.Release(); }
    }
}
```

#### DI Wiring

```csharp
services.AddSingleton<CuratedMemoryLoader>(sp =>
    new CuratedMemoryLoader(sp.GetRequiredService<IMemoryService>(), sp.GetRequiredService<IProfileService>()));
services.AddSingleton<MemoryUpdateHandler>(sp =>
    new MemoryUpdateHandler(
        sp.GetRequiredService<IMemoryService>(),
        sp.GetRequiredService<IProfileService>(),
        sp.GetRequiredService<CuratedMemoryLoader>()));
```

### Key Principles

1. **Loader owns the cache.** Only the loader can read from it; only the handler can invalidate it.
2. **Write path always invalidates.** `_loader?.InvalidateCache(profileId)` is called unconditionally
   after every successful write — never conditional on cache presence.
3. **SemaphoreSlim for both paths.** Reader holds `_cacheLock` for the cache mutation; writer holds
   `_writeLock` for the write+invalidate pair. No interleave between write and invalidation.
4. **Internal `InvalidateCache`.** Not part of the public API — only `MemoryUpdateHandler` calls it.
   Prevents external callers from accidentally clearing the cache.
5. **Null-safe loader reference.** `_loader` is optional (`?`) so `MemoryUpdateHandler` can be used
   without a loader (e.g., in background jobs where no cache is involved).

### Content Validation: MemoryParseException

Before writing, validate that content is valid text (not binary):

```csharp
foreach (var ch in content)
{
    if (ch == '\0') throw new MemoryParseException("null bytes");
    if (ch < 0x20 && ch != '\n' && ch != '\r' && ch != '\t')
        throw new MemoryParseException($"invalid control char 0x{(int)ch:X2}");
}
```

### Profile Existence Validation

Inject optional `IProfileService`. When provided:
- Loader validates profile existence before loading → `KeyNotFoundException` on unknown profileId
- Handler validates before writing too

```csharp
private async Task ValidateProfileExistsAsync(string profileId, CancellationToken ct)
{
    if (_profileService is null) return;
    var profile = await _profileService.GetProfileAsync(profileId, ct);
    if (profile is null) throw new KeyNotFoundException($"No profile found for '{profileId}'.");
}
```

### Scope and Limits

- **Single-session / single-process scope only.** Distributed cache invalidation (multi-agent M3+)
  requires a pub/sub layer (Redis, SignalR hub, etc.) — out of scope here.
- **Cache is in-process Dictionary.** No TTL, no eviction. Suitable for session-lifetime data.
  For long-lived processes, add a size limit or TTL in M3+.

### When to Use This Pattern

- High-read, low-write data accessed per-agent-turn (memory, user profile, skill config)
- Entities that are profile/user-scoped (never shared across owners)
- Any feature where a "load at session start, invalidate on write" contract is sufficient
