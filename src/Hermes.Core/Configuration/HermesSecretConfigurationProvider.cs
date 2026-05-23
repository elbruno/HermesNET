using Microsoft.Extensions.Configuration;

namespace Hermes.Core.Configuration;

public sealed class HermesSecretConfigurationProvider : ConfigurationProvider
{
    private readonly ISecretStore _secretStore;

    public HermesSecretConfigurationProvider(ISecretStore secretStore)
    {
        _secretStore = secretStore;
    }

    public override void Load()
    {
        if (_secretStore.TryGet(HermesSecretKeys.OpenAiApiKey, out var apiKey) && !string.IsNullOrWhiteSpace(apiKey))
        {
            Data[HermesSecretKeys.OpenAiApiKey] = apiKey;
        }
    }
}
