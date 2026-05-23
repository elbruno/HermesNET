using Hermes.Core.Configuration;

namespace Hermes.Core.Tests.Configuration;

internal sealed class FakeSecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _secrets = new();

    public bool TryGet(string key, out string value) => _secrets.TryGetValue(key, out value!);

    public void Set(string key, string value) => _secrets[key] = value;

    public void Delete(string key) => _secrets.Remove(key);
}
