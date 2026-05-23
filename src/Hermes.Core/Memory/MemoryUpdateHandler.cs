using Hermes.Core.Profiles;

namespace Hermes.Core.Memory;

/// <summary>
/// Applies profile-scoped curated memory updates atomically.
///
/// Responsibilities:
/// - Content validation (<see cref="MemoryParseException"/> on binary/control-character content)
/// - Profile existence check via optional <see cref="IProfileService"/>
/// - Version increment (delegated to <see cref="IMemoryService"/> upsert)
/// - Cache invalidation in a paired <see cref="CuratedMemoryLoader"/> after every write
/// - Write serialisation via a per-handler SemaphoreSlim (single-session scope)
///
/// Concurrency note: This serialises writes within one handler instance. Multiple concurrent
/// agents sharing the same handler are serialised. Distributed / multi-instance scenarios
/// are out of scope for M2 (single-session only).
/// </summary>
public sealed class MemoryUpdateHandler
{
    private readonly IMemoryService _memoryService;
    private readonly IProfileService? _profileService;
    private readonly CuratedMemoryLoader? _loader;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public MemoryUpdateHandler(
        IMemoryService memoryService,
        IProfileService? profileService = null,
        CuratedMemoryLoader? loader = null)
    {
        _memoryService = memoryService;
        _profileService = profileService;
        _loader = loader;
    }

    /// <summary>
    /// Writes (upserts) the MEMORY.md content for the given profile, then invalidates the
    /// loader cache so the next <see cref="CuratedMemoryLoader.LoadMemoryAsync"/> fetches
    /// fresh data.
    /// </summary>
    /// <exception cref="ArgumentException">profileId is null or empty.</exception>
    /// <exception cref="KeyNotFoundException">
    ///     Thrown when <see cref="IProfileService"/> is provided and no profile matches profileId.
    /// </exception>
    /// <exception cref="MemoryParseException">content contains invalid characters or binary data.</exception>
    public async Task UpdateMemoryAsync(string profileId, string content, CancellationToken ct = default)
    {
        ValidateProfileId(profileId);
        await ValidateProfileExistsAsync(profileId, ct);
        ValidateContent(content, nameof(content));

        await _writeLock.WaitAsync(ct);
        try
        {
            await _memoryService.UpdateMemoryAsync(profileId, content, ct);
            _loader?.InvalidateCache(profileId);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Writes (upserts) the USER.md content for the given profile, then invalidates cache.
    /// </summary>
    /// <exception cref="ArgumentException">profileId is null or empty.</exception>
    /// <exception cref="KeyNotFoundException">profileId not found (when IProfileService provided).</exception>
    /// <exception cref="MemoryParseException">data contains invalid characters.</exception>
    public async Task UpdateUserProfileAsync(string profileId, string data, CancellationToken ct = default)
    {
        ValidateProfileId(profileId);
        await ValidateProfileExistsAsync(profileId, ct);
        ValidateContent(data, nameof(data));

        await _writeLock.WaitAsync(ct);
        try
        {
            await _memoryService.UpdateUserProfileAsync(profileId, data, ct);
            _loader?.InvalidateCache(profileId);
        }
        finally
        {
            _writeLock.Release();
        }
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

    /// <summary>
    /// Rejects content that contains null bytes or non-printable ASCII control characters
    /// (excluding normal Markdown whitespace: LF, CR, TAB). This catches binary files,
    /// truncated UTF-8 streams, and legacy encodings that would corrupt stored Markdown.
    /// </summary>
    private static void ValidateContent(string content, string paramName)
    {
        if (content is null)
            throw new ArgumentNullException(paramName);

        foreach (var ch in content)
        {
            if (ch == '\0')
                throw new MemoryParseException(
                    "Memory content contains null bytes. Content must be valid UTF-8 text (Markdown).");

            if (ch < 0x20 && ch != '\n' && ch != '\r' && ch != '\t')
                throw new MemoryParseException(
                    $"Memory content contains invalid control character 0x{(int)ch:X2}. " +
                    "Content must be valid Markdown text.");
        }
    }
}
