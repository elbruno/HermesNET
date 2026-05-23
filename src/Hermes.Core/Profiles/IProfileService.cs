namespace Hermes.Core.Profiles;

/// <summary>
/// Contract for profile lifecycle management. All operations are idempotent-safe
/// and observable — callers can detect every state change.
/// </summary>
public interface IProfileService
{
    /// <summary>Initializes the backing store. Idempotent — safe to call on every startup.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a new profile. Name must be unique.</summary>
    /// <exception cref="InvalidOperationException">Thrown when a profile with the same name already exists.</exception>
    Task<Profile> CreateProfileAsync(string name, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>Returns the profile by ID, or null if it does not exist.</summary>
    Task<Profile?> GetProfileAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Returns the profile matching the given name, or null.</summary>
    Task<Profile?> GetProfileByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Updates mutable fields. Partial update — pass null to leave a field unchanged.</summary>
    /// <exception cref="KeyNotFoundException">Thrown when the profile does not exist.</exception>
    Task<Profile> UpdateProfileAsync(string id, string? name = null, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a profile and all sessions belonging to it.</summary>
    /// <exception cref="KeyNotFoundException">Thrown when the profile does not exist.</exception>
    Task DeleteProfileAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Returns all profiles ordered by creation date ascending.</summary>
    IAsyncEnumerable<Profile> ListProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>Sets the active profile atomically. Persists across restarts.</summary>
    /// <exception cref="KeyNotFoundException">Thrown when the profile does not exist.</exception>
    Task SwitchProfileAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Returns the current active profile, or null if none has been set.</summary>
    Task<Profile?> GetCurrentProfileAsync(CancellationToken cancellationToken = default);
}
