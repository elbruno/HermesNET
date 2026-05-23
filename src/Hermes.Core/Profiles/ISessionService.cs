namespace Hermes.Core.Profiles;

/// <summary>
/// Contract for session management scoped to profiles.
/// Sessions are always owned by exactly one profile; cross-profile access is denied by contract.
/// </summary>
public interface ISessionService
{
    /// <summary>Initializes the backing store. Idempotent — safe to call on every startup.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a new named session under the given profile.</summary>
    /// <exception cref="KeyNotFoundException">Thrown when the profile does not exist.</exception>
    Task<ProfileSession> CreateSessionAsync(string profileId, string name, CancellationToken cancellationToken = default);

    /// <summary>Returns the session by ID, or null if it does not exist.</summary>
    Task<ProfileSession?> GetSessionAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Updates the session metadata blob (Parker uses this for memory scoping).</summary>
    /// <exception cref="KeyNotFoundException">Thrown when the session does not exist.</exception>
    Task SaveSessionAsync(string id, string? metadata, CancellationToken cancellationToken = default);

    /// <summary>Returns all sessions for a given profile ordered by last_accessed descending. No N+1 loads.</summary>
    IAsyncEnumerable<ProfileSession> ListSessionsByProfileAsync(string profileId, CancellationToken cancellationToken = default);

    /// <summary>Sets the active session atomically. Session must belong to the current profile.</summary>
    /// <exception cref="KeyNotFoundException">Thrown when the session does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the session does not belong to the current profile.</exception>
    Task SwitchSessionAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Returns the current active session, or null if none has been set.</summary>
    Task<ProfileSession?> GetCurrentSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a session. If the deleted session was current, current_session_id is cleared.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when the session does not exist.</exception>
    Task DeleteSessionAsync(string id, CancellationToken cancellationToken = default);
}
