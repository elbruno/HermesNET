namespace Hermes.Core.Services;

/// <summary>
/// Hermes abstraction for chat clients - allows for custom implementations
/// while abstracting away the details of the underlying provider.
/// </summary>
public interface IChatClient
{
    ValueTask<string> CompleteAsync(IList<string> messages, CancellationToken cancellationToken = default);
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
}
