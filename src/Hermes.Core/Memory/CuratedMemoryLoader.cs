using Hermes.Core.Profiles;
using Hermes.Core.Telemetry;
using System.Diagnostics;

namespace Hermes.Core.Memory;

/// <summary>
/// Application-facing loader for profile-scoped curated memory (MEMORY.md snapshots).
///
/// Wraps <see cref="IMemoryService"/> with:
/// - An in-process cache (invalidated on every write via <see cref="MemoryUpdateHandler"/>)
/// - Optional profile existence validation via <see cref="IProfileService"/>
/// - Strict profile isolation: every access path carries a profileId; no "default" path exists
///
/// Thread-safe for concurrent reads across profiles; a single lock guards cache mutations.
/// </summary>
public sealed class CuratedMemoryLoader
{
    private readonly IMemoryService _memoryService;
    private readonly IProfileService? _profileService;

    private readonly Dictionary<string, MemoryContext> _memoryCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UserProfileData> _userCache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public CuratedMemoryLoader(IMemoryService memoryService, IProfileService? profileService = null)
    {
        _memoryService = memoryService;
        _profileService = profileService;
    }

    /// <summary>
    /// Loads the MEMORY.md snapshot for the given profile. Returns a cached snapshot on
    /// subsequent calls until the cache is invalidated by <see cref="MemoryUpdateHandler"/>.
    ///
    /// Returns <see cref="MemoryContext.Empty"/> when the profile exists but has no memory written.
    /// </summary>
    /// <exception cref="ArgumentException">profileId is null or empty.</exception>
    /// <exception cref="KeyNotFoundException">
    ///     Thrown when <see cref="IProfileService"/> is provided and no profile matches profileId.
    /// </exception>
    public async Task<MemoryContext> LoadMemoryAsync(string profileId, CancellationToken ct = default)
    {
        using var span = TelemetryProvider.GetActivitySource().StartActivity("CuratedMemoryLoader.LoadMemoryAsync");
        span?.SetTag("profile.id", profileId);
        span?.SetTag("operation", "load");
        
        ValidateProfileId(profileId);
        await ValidateProfileExistsAsync(profileId, ct);

        await _cacheLock.WaitAsync(ct);
        try
        {
            if (_memoryCache.TryGetValue(profileId, out var cached))
            {
                span?.SetTag("cache.hit", true);
                return cached;
            }
            
            span?.SetTag("cache.hit", false);

            var memory = await _memoryService.LoadMemoryAsync(profileId, ct);
            _memoryCache[profileId] = memory;
            return memory;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Loads the USER.md snapshot for the given profile.
    ///
    /// Returns a cached snapshot on subsequent calls until invalidated by
    /// <see cref="MemoryUpdateHandler"/>.
    /// </summary>
    /// <exception cref="ArgumentException">profileId is null or empty.</exception>
    /// <exception cref="KeyNotFoundException">
    ///     Thrown when no user profile data exists for the given profileId.
    /// </exception>
    public async Task<UserProfileData> LoadUserProfileAsync(string profileId, CancellationToken ct = default)
    {
        using var span = TelemetryProvider.GetActivitySource().StartActivity("CuratedMemoryLoader.LoadUserProfileAsync");
        span?.SetTag("profile.id", profileId);
        span?.SetTag("operation", "load");
        
        ValidateProfileId(profileId);

        await _cacheLock.WaitAsync(ct);
        try
        {
            if (_userCache.TryGetValue(profileId, out var cached))
            {
                span?.SetTag("cache.hit", true);
                return cached;
            }
            
            span?.SetTag("cache.hit", false);

            var profile = await _memoryService.LoadUserProfileAsync(profileId, ct);
            if (profile.IsEmpty)
                throw new KeyNotFoundException($"No user profile found for profileId '{profileId}'.");

            _userCache[profileId] = profile;
            return profile;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Removes cached entries for the given profile. Called by <see cref="MemoryUpdateHandler"/>
    /// after each successful write so the next read fetches fresh data.
    /// </summary>
    internal void InvalidateCache(string profileId)
    {
        _memoryCache.Remove(profileId);
        _userCache.Remove(profileId);
    }

    private async Task ValidateProfileExistsAsync(string profileId, CancellationToken ct)
    {
        if (_profileService is null) return;

        var profile = await _profileService.GetProfileAsync(profileId, ct);
        if (profile is null)
            throw new KeyNotFoundException($"No profile found for profileId '{profileId}'.");
    }

    private static void ValidateProfileId(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("profileId must not be null or empty.", nameof(profileId));
    }
}
