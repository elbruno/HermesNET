using System.Runtime.CompilerServices;
using Hermes.Core.Policy;

namespace Hermes.Core.Services;

/// <summary>
/// Hermes abstraction for chat clients — allows custom implementations
/// while abstracting away provider details.
/// </summary>
public interface IChatClient
{
    ValueTask<string> CompleteAsync(IList<string> messages, CancellationToken cancellationToken = default);

    /// <summary>Streams tokens as they arrive from the provider.</summary>
    IAsyncEnumerable<string> StreamAsync(IList<string> messages, CancellationToken cancellationToken = default);

    void Dispose();
}

public class HermesChatService : IHermesChatService
{
    private readonly IChatClient _chatClient;
    private readonly IPolicyEngine? _policyEngine;

    public HermesChatService(IChatClient chatClient, IPolicyEngine? policyEngine = null)
    {
        _chatClient    = chatClient;
        _policyEngine  = policyEngine;
    }

    public async ValueTask<string> ChatAsync(string message, CancellationToken cancellationToken = default)
    {
        var messages = new List<string> { message };
        var response = await _chatClient.CompleteAsync(messages, cancellationToken);
        return response ?? string.Empty;
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        string message,
        string profileId,
        string sessionId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // M3C: enforce rate limit before processing the request
        if (_policyEngine is not null)
        {
            var rateLimitResult = _policyEngine.CheckRateLimit(profileId, sessionId);
            if (rateLimitResult.Verdict == PolicyVerdict.Deny)
                throw new PolicyViolationException(rateLimitResult);
        }

        var messages = new List<string> { message };
        await foreach (var token in _chatClient.StreamAsync(messages, cancellationToken))
            yield return token;
    }
}
