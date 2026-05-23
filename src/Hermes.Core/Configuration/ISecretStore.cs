namespace Hermes.Core.Configuration;

public interface ISecretStore
{
    bool TryGet(string key, out string value);

    void Set(string key, string value);

    void Delete(string key);
}
