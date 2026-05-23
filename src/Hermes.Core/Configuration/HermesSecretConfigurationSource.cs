using Microsoft.Extensions.Configuration;

namespace Hermes.Core.Configuration;

public sealed class HermesSecretConfigurationSource : IConfigurationSource
{
    private readonly ISecretStore _secretStore;

    public HermesSecretConfigurationSource()
        : this(new CredentialManagerSecretStore())
    {
    }

    public HermesSecretConfigurationSource(ISecretStore secretStore)
    {
        _secretStore = secretStore;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder) => new HermesSecretConfigurationProvider(_secretStore);
}
