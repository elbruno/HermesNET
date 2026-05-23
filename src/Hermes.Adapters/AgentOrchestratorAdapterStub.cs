using Hermes.Core.Services;

namespace Hermes.Adapters;

/// <summary>
/// Pass-through stub implementation of <see cref="IAgentOrchestratorAdapter"/>.
/// Delegates directly to <see cref="IHermesChatService"/> while preserving
/// the <see cref="SessionContext"/> (ProfileId + SessionId) across every call.
/// No MAF dependency — M4 will provide the real implementation.
/// </summary>
public sealed class AgentOrchestratorAdapterStub : IAgentOrchestratorAdapter
{
    private readonly IHermesChatService _chatService;

    public AgentOrchestratorAdapterStub(IHermesChatService chatService)
    {
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
    }

    /// <inheritdoc/>
    public async Task<AgentResponse> OrchestrateAsync(
        AgentRequest request,
        SessionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (!ValidateSessionContext(context))
            throw new InvalidOperationException(
                $"Invalid session context: ProfileId='{context.ProfileId}', SessionId='{context.SessionId}'.");

        var content = await _chatService
            .ChatAsync(request.Message, cancellationToken)
            .ConfigureAwait(false);

        return new AgentResponse
        {
            Content = content,
            Context = context   // echo back unchanged — R2 contract
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<TokenEvent> StreamTokensAsync(
        AgentRequest request,
        SessionContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (!ValidateSessionContext(context))
            throw new InvalidOperationException(
                $"Invalid session context: ProfileId='{context.ProfileId}', SessionId='{context.SessionId}'.");

        var tokens = new List<string>();
        await foreach (var token in _chatService
                           .StreamChatAsync(request.Message, context.ProfileId, context.SessionId, cancellationToken)
                           .ConfigureAwait(false))
        {
            tokens.Add(token);
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            yield return new TokenEvent
            {
                Token = tokens[i],
                Context = context,          // R2: context is immutable and unchanged
                IsFinal = i == tokens.Count - 1
            };
        }
    }

    /// <inheritdoc/>
    public bool ValidateSessionContext(SessionContext context)
    {
        if (context is null) return false;
        return !string.IsNullOrWhiteSpace(context.ProfileId)
            && !string.IsNullOrWhiteSpace(context.SessionId);
    }
}
