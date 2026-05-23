using Microsoft.Extensions.Configuration;

namespace Hermes.Cli.Configuration;

public static class HermesProviderNames
{
    public const string Ollama = "Ollama";
    public const string OpenAI = "OpenAI";

    public static readonly string[] Supported = [Ollama, OpenAI];
}

public sealed record HermesCliSettings
{
    public string Provider { get; init; } = HermesProviderNames.Ollama;
    public OllamaSettings Ollama { get; init; } = new();
    public OpenAISettings OpenAI { get; init; } = new();
    public DatabaseSettings Database { get; init; } = new();

    public static HermesCliSettings FromConfiguration(IConfiguration configuration) =>
        new()
        {
            Provider = configuration["Provider"] ?? HermesProviderNames.Ollama,
            Ollama = new OllamaSettings
            {
                BaseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434",
                Model = configuration["Ollama:Model"] ?? "llama3"
            },
            OpenAI = new OpenAISettings
            {
                ApiKey = configuration["OpenAI:ApiKey"] ?? string.Empty,
                Model = configuration["OpenAI:Model"] ?? "gpt-4"
            },
            Database = new DatabaseSettings
            {
                ConnectionString = configuration["Database:ConnectionString"] ?? "Data Source=hermes.db"
            }
        };
}

public sealed record OllamaSettings
{
    public string BaseUrl { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = "llama3";
}

public sealed record OpenAISettings
{
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = "gpt-4";
}

public sealed record DatabaseSettings
{
    public string ConnectionString { get; init; } = "Data Source=hermes.db";
}
