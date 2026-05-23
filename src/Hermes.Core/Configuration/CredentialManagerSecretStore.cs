using GitCredentialManager;

namespace Hermes.Core.Configuration;

public sealed class CredentialManagerSecretStore : ISecretStore
{
    private readonly ICredentialStore _store;

    public CredentialManagerSecretStore()
        : this(CredentialManager.Create(HermesSecretKeys.StoreNamespace))
    {
    }

    internal CredentialManagerSecretStore(ICredentialStore store)
    {
        _store = store;
    }

    public bool TryGet(string key, out string value)
    {
        try
        {
            var credential = _store.Get(HermesSecretKeys.StoreNamespace, key);
            if (!string.IsNullOrWhiteSpace(credential?.Password))
            {
                value = credential.Password;
                return true;
            }
        }
        catch
        {
            // Treat missing or unavailable OS secret stores as an absent secret.
        }

        value = string.Empty;
        return false;
    }

    public void Set(string key, string value)
    {
        try
        {
            _store.AddOrUpdate(HermesSecretKeys.StoreNamespace, key, value);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unable to persist secret '{key}'.", ex);
        }
    }

    public void Delete(string key)
    {
        try
        {
            _store.Remove(HermesSecretKeys.StoreNamespace, key);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unable to delete secret '{key}'.", ex);
        }
    }
}
