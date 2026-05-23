using FluentAssertions;
using Hermes.Cli.Configuration;
using Microsoft.Extensions.Configuration;

namespace Hermes.Core.Tests.Configuration;

public sealed class HermesCliConfigStoreTests
{
    [Fact]
    public async Task SaveAndLoadRoundTripsTheConfiguration()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new HermesCliConfigStore(Path.Combine(directory, "appsettings.json"));

        var settings = new HermesCliSettings
        {
            Provider = HermesProviderNames.OpenAI,
            OpenAI = new OpenAISettings
            {
                ApiKey = "test-key",
                Model = "gpt-4.1"
            }
        };

        await store.SaveAsync(settings);
        var loaded = await store.LoadAsync();

        loaded.Provider.Should().Be(HermesProviderNames.OpenAI);
        loaded.OpenAI.ApiKey.Should().Be("test-key");
        loaded.OpenAI.Model.Should().Be("gpt-4.1");
    }

    [Fact]
    public void FromConfigurationReadsTheKnownKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider"] = HermesProviderNames.Ollama,
                ["Ollama:BaseUrl"] = "http://localhost:11434",
                ["Ollama:Model"] = "llama3.1",
                ["Database:ConnectionString"] = "Data Source=test.db"
            })
            .Build();

        var settings = HermesCliSettings.FromConfiguration(configuration);

        settings.Provider.Should().Be(HermesProviderNames.Ollama);
        settings.Ollama.Model.Should().Be("llama3.1");
        settings.Database.ConnectionString.Should().Be("Data Source=test.db");
    }
}
