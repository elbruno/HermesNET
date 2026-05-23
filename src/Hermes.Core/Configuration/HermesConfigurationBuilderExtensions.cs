using Microsoft.Extensions.Configuration;

namespace Hermes.Core.Configuration;

public static class HermesConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddHermesSettings(
        this IConfigurationBuilder builder,
        string bundledConfigPath,
        string userConfigPath,
        ISecretStore? secretStore = null)
    {
        return builder
            .AddJsonFile(bundledConfigPath, optional: false, reloadOnChange: true)
            .AddJsonFile(userConfigPath, optional: true, reloadOnChange: true)
            .Add(new HermesSecretConfigurationSource(secretStore ?? new CredentialManagerSecretStore()));
    }
}
