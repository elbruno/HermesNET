using System.Runtime.CompilerServices;

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

    public HermesChatService(IChatClient chatClient)
    {
        _chatClient = chatClient;
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
        var messages = new List<string> { message };
        await foreach (var token in _chatClient.StreamAsync(messages, cancellationToken))
            yield return token;
    }
}
