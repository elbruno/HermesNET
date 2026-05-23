using Hermes.Cli.Configuration;
using Hermes.Core.Configuration;
using Hermes.Host;
using Microsoft.Extensions.Configuration;

namespace Hermes.Core.Tests.Configuration;

public sealed class HermesSecureSettingsTests
{
    [Fact]
    public void HermesDoctor_Evaluate_OpenAISecretPresent_MasksApiKeyInDetails()
    {
        var testRoot = CreateTestRoot();

        try
        {
            var userConfigPath = Path.Combine(testRoot, "user", "appsettings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(userConfigPath)!);
            File.WriteAllText(userConfigPath, "{}");

            var settings = new HermesCliSettings
            {
                Provider = HermesProviderNames.OpenAI,
                OpenAI = new OpenAISettings
                {
                    ApiKey = "super-secret",
                    Model = "gpt-4.1"
                },
                Database = new DatabaseSettings
                {
                    ConnectionString = "Data Source=test.db"
                }
            };

            var report = HermesDoctor.Evaluate(settings, userConfigPath, userConfigPath);

            report.Checks.Should().Contain(check =>
                check.Name == "OpenAI API key" &&
                check.Status == DoctorStatus.Pass &&
                check.Details == "[set]");
            report.Checks.Should().NotContain(check => check.Details.Contains("super-secret"));
        }
        finally
        {
            CleanupTestRoot(testRoot);
        }
    }

    [Fact]
    public void HermesDoctor_Evaluate_OpenAISecretMissing_ReportsFailure()
    {
        var testRoot = CreateTestRoot();

        try
        {
            var userConfigPath = Path.Combine(testRoot, "user", "appsettings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(userConfigPath)!);
            File.WriteAllText(userConfigPath, "{}");

            var settings = new HermesCliSettings
            {
                Provider = HermesProviderNames.OpenAI,
                OpenAI = new OpenAISettings
                {
                    ApiKey = string.Empty,
                    Model = "gpt-4.1"
                },
                Database = new DatabaseSettings
                {
                    ConnectionString = "Data Source=test.db"
                }
            };

            var report = HermesDoctor.Evaluate(settings, userConfigPath, userConfigPath);

            report.HasFailures.Should().BeTrue();
            report.Checks.Should().Contain(check =>
                check.Name == "OpenAI API key" &&
                check.Status == DoctorStatus.Fail &&
                check.Details == "Missing value");
        }
        finally
        {
            CleanupTestRoot(testRoot);
        }
    }

    [Fact]
    public void ChatClientFactory_CreateClient_WithMissingOpenAIApiKey_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider"] = HermesProviderNames.OpenAI,
                ["OpenAI:Model"] = "gpt-4.1"
            })
            .Build();

        var factory = new ChatClientFactory(config);

        Action act = () => factory.CreateClient();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*OpenAI:ApiKey*");
    }

    [Fact]
    public async Task HermesCliConfigStore_SaveAsync_DoesNotPersistOpenAIApiKeyInPlaintext()
    {
        var testRoot = CreateTestRoot();

        try
        {
            var configPath = Path.Combine(testRoot, "user", "appsettings.json");
            var secrets = new FakeSecretStore();
            var store = new HermesCliConfigStore(configPath, secrets);
            var settings = new HermesCliSettings
            {
                Provider = HermesProviderNames.OpenAI,
                OpenAI = new OpenAISettings
                {
                    ApiKey = "super-secret",
                    Model = "gpt-4.1"
                }
            };

            await store.SaveAsync(settings);

            var persisted = await File.ReadAllTextAsync(configPath);

            persisted.Should().NotContain("super-secret");
            persisted.Should().NotContain("\"ApiKey\":");
            secrets.TryGet(HermesSecretKeys.OpenAiApiKey, out var storedKey).Should().BeTrue();
            storedKey.Should().Be("super-secret");
        }
        finally
        {
            CleanupTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task HermesCliConfigStore_LoadAsync_MigratesPlaintextOpenAIApiKeyToSecureStorage()
    {
        var testRoot = CreateTestRoot();

        try
        {
            var configPath = Path.Combine(testRoot, "user", "appsettings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            await File.WriteAllTextAsync(configPath, """
            {
              "Provider": "OpenAI",
              "OpenAI": {
                "ApiKey": "legacy-secret",
                "Model": "gpt-4.1"
              }
            }
            """);

            var secrets = new FakeSecretStore();
            var store = new HermesCliConfigStore(configPath, secrets);

            var loaded = await store.LoadAsync();

            loaded.Provider.Should().Be(HermesProviderNames.OpenAI);
            loaded.OpenAI.ApiKey.Should().Be("legacy-secret");

            var persisted = await File.ReadAllTextAsync(configPath);
            persisted.Should().NotContain("legacy-secret");
            persisted.Should().NotContain("\"ApiKey\":");
            secrets.TryGet(HermesSecretKeys.OpenAiApiKey, out var storedKey).Should().BeTrue();
            storedKey.Should().Be("legacy-secret");
        }
        finally
        {
            CleanupTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task HermesCliConfigStore_LoadAsync_FailsClosedWhenSecretMigrationFails()
    {
        var testRoot = CreateTestRoot();

        try
        {
            var configPath = Path.Combine(testRoot, "user", "appsettings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            await File.WriteAllTextAsync(configPath, """
            {
              "Provider": "OpenAI",
              "OpenAI": {
                "ApiKey": "legacy-secret",
                "Model": "gpt-4.1"
              }
            }
            """);

            var store = new HermesCliConfigStore(configPath, new ThrowingSecretStore());

            Func<Task> act = async () => await store.LoadAsync();

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*migrate the legacy*");

            var persisted = await File.ReadAllTextAsync(configPath);
            persisted.Should().NotContain("legacy-secret");
            persisted.Should().NotContain("\"ApiKey\":");
        }
        finally
        {
            CleanupTestRoot(testRoot);
        }
    }

    [Fact]
    public void ConfigurationBuilder_UsesSecureSecretValueForOpenAIApiKey()
    {
        var secrets = new FakeSecretStore();
        secrets.Set(HermesSecretKeys.OpenAiApiKey, "super-secret");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider"] = HermesProviderNames.OpenAI,
                ["OpenAI:Model"] = "gpt-4.1"
            })
            .Add(new HermesSecretConfigurationSource(secrets))
            .Build();

        var settings = HermesCliSettings.FromConfiguration(configuration);

        settings.OpenAI.ApiKey.Should().Be("super-secret");
    }

    private static string CreateTestRoot()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "test-dirs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CleanupTestRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class ThrowingSecretStore : ISecretStore
    {
        public bool TryGet(string key, out string value)
        {
            value = string.Empty;
            return false;
        }

        public void Set(string key, string value) => throw new InvalidOperationException("Secret store unavailable.");

        public void Delete(string key)
        {
        }
    }
}
