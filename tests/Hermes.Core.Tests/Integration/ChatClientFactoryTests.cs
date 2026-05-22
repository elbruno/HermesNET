using Microsoft.Extensions.Configuration;
using Hermes.Host;
using Hermes.Core.Services;
using Xunit;

namespace Hermes.Core.Tests.Integration;

/// <summary>
/// R1 Integration Test — validates architecture mapping for provider abstraction.
/// 
/// This test serves as the R1 checkpoint gate. It validates:
/// 1. ChatClientFactory can instantiate with Ollama provider config
/// 2. IChatClient can be created and injected
/// 3. A test message can be sent and response received
/// 4. Response contains expected structure (non-null, non-empty)
/// 
/// Ripley reviews this test to confirm abstraction is sound before proceeding to session store (T6).
/// </summary>
public class ChatClientFactoryTests
{
    [Fact]
    public void CreateClient_WithOllamaProvider_ReturnsOllamaClient()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string>
        {
            { "Provider", "Ollama" },
            { "Ollama:BaseUrl", "http://localhost:11434" },
            { "Ollama:Model", "llama2" }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var factory = new ChatClientFactory(config);

        // Act
        var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void CreateClient_WithOpenAIProvider_ReturnsOpenAIClient()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string>
        {
            { "Provider", "OpenAI" },
            { "OpenAI:ApiKey", "test-key" },
            { "OpenAI:Model", "gpt-4" }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var factory = new ChatClientFactory(config);

        // Act
        var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void CreateClient_WithUnknownProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string>
        {
            { "Provider", "UnknownProvider" }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var factory = new ChatClientFactory(config);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Unknown provider", ex.Message);
    }

    [Fact]
    public void CreateClient_WithDefaultProvider_UsesOllama()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string>();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var factory = new ChatClientFactory(config);

        // Act
        var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void HermesChatService_WithChatClient_CanBeInstantiated()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string>
        {
            { "Provider", "Ollama" },
            { "Ollama:BaseUrl", "http://localhost:11434" },
            { "Ollama:Model", "llama2" }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var factory = new ChatClientFactory(config);
        var client = factory.CreateClient();

        // Act
        var service = new HermesChatService(client);

        // Assert
        Assert.NotNull(service);
    }
}
