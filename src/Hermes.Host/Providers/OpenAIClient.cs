using Hermes.Core.Services;

namespace Hermes.Host.Providers;

public class OpenAIClient : IChatClient
{
    private readonly string _apiKey;
    private readonly string _model;

    public OpenAIClient(string apiKey, string model = "gpt-4")
    {
        _apiKey = apiKey;
        _model = model;
    }

    public async ValueTask<string> CompleteAsync(
        IList<string> messages,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(0, cancellationToken);
        throw new NotImplementedException("OpenAI provider will be implemented in a future milestone");
    }

    public void Dispose()
    {
    }
}
