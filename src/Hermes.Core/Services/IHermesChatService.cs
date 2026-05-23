namespace Hermes.Core.Services;

public interface IHermesChatService
{
    ValueTask<string> ChatAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams tokens for a chat message in the context of a profile and session.
    /// </summary>
    IAsyncEnumerable<string> StreamChatAsync(
        string message,
        string profileId,
        string sessionId,
        CancellationToken cancellationToken = default);
}
