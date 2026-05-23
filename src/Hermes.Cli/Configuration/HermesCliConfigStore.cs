using Hermes.Core.Configuration;
using System.Text.Json;

namespace Hermes.Cli.Configuration;

public sealed class HermesCliConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _configPath;
    private readonly ISecretStore _secretStore;

    public HermesCliConfigStore(string? configPath = null)
        : this(configPath, new CredentialManagerSecretStore())
    {
    }

    public HermesCliConfigStore(string? configPath, ISecretStore secretStore)
    {
        _configPath = configPath ?? GetDefaultConfigPath();
        _secretStore = secretStore;
    }

    public string ConfigPath => _configPath;

    public static string GetDefaultConfigPath()
    {
        return HermesSettingsPaths.GetDefaultUserConfigPath();
    }

    public async Task<HermesCliSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var hasSecret = _secretStore.TryGet(HermesSecretKeys.OpenAiApiKey, out var secretApiKey) &&
            !string.IsNullOrWhiteSpace(secretApiKey);

        if (!File.Exists(_configPath))
        {
            return hasSecret ? ApplySecret(new HermesCliSettings(), secretApiKey) : new HermesCliSettings();
        }

        var rawJson = await File.ReadAllTextAsync(_configPath, cancellationToken);
        var settings = JsonSerializer.Deserialize<HermesCliSettings>(rawJson, JsonOptions) ?? new HermesCliSettings();
        var legacyApiKey = TryReadLegacyApiKey(rawJson);

        if (!string.IsNullOrWhiteSpace(legacyApiKey))
        {
            if (hasSecret)
            {
                settings = ApplySecret(settings, secretApiKey);
                await WriteConfigurationAsync(settings, cancellationToken);
                return settings;
            }

            await WriteConfigurationAsync(settings, cancellationToken);

            try
            {
                _secretStore.Set(HermesSecretKeys.OpenAiApiKey, legacyApiKey);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Unable to migrate the legacy '{HermesSecretKeys.OpenAiApiKey}' value to secure storage.",
                    ex);
            }

            return ApplySecret(settings, legacyApiKey);
        }

        return hasSecret
            ? ApplySecret(settings, secretApiKey)
            : settings;
    }

    public async Task SaveAsync(HermesCliSettings settings, CancellationToken cancellationToken = default)
    {
        PersistSecret(settings.OpenAI.ApiKey);
        await WriteConfigurationAsync(settings, cancellationToken);
    }

    private async Task WriteConfigurationAsync(HermesCliSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(ToPersistedDocument(settings), JsonOptions), cancellationToken);
    }

    private void PersistSecret(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _secretStore.Delete(HermesSecretKeys.OpenAiApiKey);
            return;
        }

        _secretStore.Set(HermesSecretKeys.OpenAiApiKey, apiKey);
    }

    private static HermesCliSettings ApplySecret(HermesCliSettings settings, string? apiKey) =>
        settings with
        {
            OpenAI = settings.OpenAI with
            {
                ApiKey = apiKey ?? string.Empty
            }
        };

    private static string? TryReadLegacyApiKey(string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        if (!document.RootElement.TryGetProperty("OpenAI", out var openAi))
        {
            return null;
        }

        if (!openAi.TryGetProperty("ApiKey", out var apiKeyElement))
        {
            return null;
        }

        return apiKeyElement.ValueKind == JsonValueKind.String ? apiKeyElement.GetString() : null;
    }

    private static HermesCliConfigDocument ToPersistedDocument(HermesCliSettings settings) =>
        new()
        {
            Provider = settings.Provider,
            Ollama = settings.Ollama,
            OpenAI = new PersistedOpenAISettings
            {
                Model = settings.OpenAI.Model
            },
            Database = settings.Database
        };

    private sealed record HermesCliConfigDocument
    {
        public string Provider { get; init; } = HermesProviderNames.Ollama;
        public OllamaSettings Ollama { get; init; } = new();
        public PersistedOpenAISettings OpenAI { get; init; } = new();
        public DatabaseSettings Database { get; init; } = new();
    }

    private sealed record PersistedOpenAISettings
    {
        public string Model { get; init; } = "gpt-4";
    }
}
