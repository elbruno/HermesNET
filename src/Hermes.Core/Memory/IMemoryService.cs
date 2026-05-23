namespace Hermes.Core.Memory;

/// <summary>
/// Service contract for profile-scoped curated memory (MEMORY.md and USER.md).
///
/// All methods require a non-null, non-empty profileId. The implementation MUST
/// enforce that every read and write is scoped to the supplied profileId — cross-
/// profile access is a data-integrity violation, not just a bug.
/// </summary>
public interface IMemoryService
{
    /// <summary>
    /// Loads the MEMORY.md snapshot for the given profile.
    /// Returns <see cref="MemoryContext.Empty"/> if no memory has been written yet.
    /// Never returns data belonging to a different profile.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if profileId is null or empty.</exception>
    Task<MemoryContext> LoadMemoryAsync(string profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes (upserts) the MEMORY.md content for the given profile.
    /// Increments the version counter on every write.
    /// Content is validated against <see cref="GetMemorySchemaAsync"/> limits before persisting.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if profileId is null/empty or content exceeds schema limits.</exception>
    Task UpdateMemoryAsync(string profileId, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the USER.md snapshot (preferences and profile state) for the given profile.
    /// Returns <see cref="UserProfileData.Empty"/> if no profile data has been written yet.
    /// Never returns data belonging to a different profile.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if profileId is null or empty.</exception>
    Task<UserProfileData> LoadUserProfileAsync(string profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes (upserts) the USER.md content for the given profile.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if profileId is null or empty.</exception>
    Task UpdateUserProfileAsync(string profileId, string data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the memory validation schema: size limits, supported formats, schema version.
    /// Used to validate content before writes and to report schema version to callers.
    /// </summary>
    Task<MemorySchema> GetMemorySchemaAsync(CancellationToken cancellationToken = default);
}
