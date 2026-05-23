using Hermes.Core.Services;

namespace Hermes.Adapters;

/// <summary>
/// Adapter contract that bridges <see cref="IHermesChatService"/> to the MAF IAgentOrchestrator surface.
///
/// <para>
/// MAF (M4) drives conversations through an IAgentOrchestrator that accepts agent requests
/// and streams token events. This adapter wraps <see cref="IHermesChatService"/> — which
/// already supports streaming via <c>StreamChatAsync</c> — and translates calls to/from
/// the MAF orchestrator contract.
/// </para>
///
/// <para>
/// Critical R2 constraint: ProfileId and SessionId must be carried through every adapter
/// call without loss. The adapter is the last line of defence before MAF; profile isolation
/// (hard FK in session context) must survive the translation.
/// </para>
///
/// <para>
/// M3 status: stub only. No MAF dependency is present yet. All methods return
/// pass-through or placeholder results. The interface shape matches what M4 will require.
/// </para>
/// </summary>
public interface IAgentOrchestratorAdapter
{
    /// <summary>
    /// Sends a chat message through the Hermes chat service and returns the full response.
    /// Preserves <paramref name="context"/> across the call.
    /// </summary>
    /// <param name="request">The agent request payload.</param>
    /// <param name="context">Session context carrying ProfileId and SessionId — must not be mutated.</param>
    /// <param name="cancellationToken">Propagates cancellation.</param>
    /// <returns>The agent response payload.</returns>
    Task<AgentResponse> OrchestrateAsync(
        AgentRequest request,
        SessionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams token events through the Hermes chat service.
    /// Every emitted <see cref="TokenEvent"/> carries the originating <paramref name="context"/>
    /// so that downstream consumers can enforce profile-level isolation.
    /// </summary>
    IAsyncEnumerable<TokenEvent> StreamTokensAsync(
        AgentRequest request,
        SessionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that <paramref name="context"/> satisfies the R2 isolation constraints:
    /// ProfileId is non-empty, SessionId is non-empty, and they are internally consistent.
    /// </summary>
    /// <returns><c>true</c> if the context is valid; <c>false</c> otherwise.</returns>
    bool ValidateSessionContext(SessionContext context);
}

/// <summary>
/// Carries the profile and session identity through the adapter layer.
/// Immutable — adapters must never mutate this record.
/// </summary>
public sealed class SessionContext
{
    /// <summary>
    /// The active profile ID. Must be non-empty.
    /// This is the R2 isolation boundary — every adapter operation is scoped to this profile.
    /// </summary>
    public required string ProfileId { get; init; }

    /// <summary>
    /// The active session ID. Must be non-empty and belong to <see cref="ProfileId"/>.
    /// </summary>
    public required string SessionId { get; init; }
}

/// <summary>
/// MAF-compatible agent request stub.
/// M4 will replace this with the real MAF AgentRequest type.
/// </summary>
public sealed class AgentRequest
{
    /// <summary>The user message text.</summary>
    public required string Message { get; init; }
}

/// <summary>
/// MAF-compatible agent response stub.
/// M4 will replace this with the real MAF AgentResponse type.
/// </summary>
public sealed class AgentResponse
{
    /// <summary>The assistant response text.</summary>
    public required string Content { get; init; }

    /// <summary>The session context echoed back from the adapter — must equal the input context.</summary>
    public required SessionContext Context { get; init; }
}

/// <summary>
/// A single streaming token event emitted by the orchestrator adapter.
/// Carries the originating session context for downstream isolation enforcement.
/// </summary>
public sealed class TokenEvent
{
    /// <summary>The token text fragment.</summary>
    public required string Token { get; init; }

    /// <summary>The session context at the time of emission — must not be mutated.</summary>
    public required SessionContext Context { get; init; }

    /// <summary>Whether this is the final token in the stream.</summary>
    public bool IsFinal { get; init; }
}
