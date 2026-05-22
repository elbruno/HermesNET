using Microsoft.Extensions.Configuration;
using Hermes.Core.Services;
using Hermes.Host.Providers;

namespace Hermes.Host;

public class ChatClientFactory
{
    private readonly IConfiguration _config;

    public ChatClientFactory(IConfiguration config)
    {
        _config = config;
    }

    public IChatClient CreateClient()
    {
        var provider = _config["Provider"] ?? "Ollama";

        return provider switch
        {
            "Ollama" => CreateOllamaClient(),
            "OpenAI" => CreateOpenAIClient(),
            _ => throw new InvalidOperationException($"Unknown provider: {provider}")
        };
    }

    private IChatClient CreateOllamaClient()
    {
        var baseUrl = _config["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var model = _config["Ollama:Model"] ?? "llama2";
        return new OllamaClient(baseUrl, model);
    }

    private IChatClient CreateOpenAIClient()
    {
        var apiKey = _config["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey not found in configuration");
        var model = _config["OpenAI:Model"] ?? "gpt-4";
        return new OpenAIClient(apiKey, model);
    }
}
