namespace Hermes.Core.Services;

public interface IHermesChatService
{
    ValueTask<string> ChatAsync(string message, CancellationToken cancellationToken = default);
}
