namespace Hermes.Cli.Configuration;

public enum DoctorStatus
{
    Pass,
    Warning,
    Fail
}

public sealed record DoctorCheck(string Name, DoctorStatus Status, string Details)
{
    public bool HasFailure => Status == DoctorStatus.Fail;
}

public sealed record DoctorReport(IReadOnlyList<DoctorCheck> Checks)
{
    public bool HasFailures => Checks.Any(check => check.Status == DoctorStatus.Fail);
}

public static class HermesDoctor
{
    public static DoctorReport Evaluate(HermesCliSettings settings, string configPath, string? appConfigPath = null)
    {
        var checks = new List<DoctorCheck>
        {
            CreateUserConfigCheck(configPath),
            CreateAppConfigCheck(appConfigPath),
            CreateProviderCheck(settings.Provider)
        };

        if (settings.Provider.Equals(HermesProviderNames.Ollama, StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(CreateUriCheck("Ollama base URL", settings.Ollama.BaseUrl));
            checks.Add(CreateRequiredValueCheck("Ollama model", settings.Ollama.Model));
        }
        else if (settings.Provider.Equals(HermesProviderNames.OpenAI, StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(CreateRequiredValueCheck("OpenAI API key", settings.OpenAI.ApiKey, maskValue: true));
            checks.Add(CreateRequiredValueCheck("OpenAI model", settings.OpenAI.Model));
        }

        checks.Add(CreateRequiredValueCheck("Database connection string", settings.Database.ConnectionString));

        return new DoctorReport(checks);
    }

    private static DoctorCheck CreateUserConfigCheck(string configPath)
    {
        if (File.Exists(configPath))
        {
            return new DoctorCheck("User config", DoctorStatus.Pass, $"Found {configPath}");
        }

        return new DoctorCheck("User config", DoctorStatus.Warning, $"No user config found at {configPath}");
    }

    private static DoctorCheck CreateAppConfigCheck(string? configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return new DoctorCheck("Bundled config", DoctorStatus.Warning, "No bundled appsettings path was provided");
        }

        if (File.Exists(configPath))
        {
            return new DoctorCheck("Bundled config", DoctorStatus.Pass, $"Found {configPath}");
        }

        return new DoctorCheck("Bundled config", DoctorStatus.Fail, $"Missing bundled appsettings file at {configPath}");
    }

    private static DoctorCheck CreateProviderCheck(string provider)
    {
        if (HermesProviderNames.Supported.Any(name => name.Equals(provider, StringComparison.OrdinalIgnoreCase)))
        {
            return new DoctorCheck("Provider", DoctorStatus.Pass, $"Active provider: {provider}");
        }

        return new DoctorCheck("Provider", DoctorStatus.Fail, $"Unsupported provider: {provider}");
    }

    private static DoctorCheck CreateUriCheck(string name, string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            return new DoctorCheck(name, DoctorStatus.Pass, value);
        }

        return new DoctorCheck(name, DoctorStatus.Fail, $"Invalid URL: {value}");
    }

    private static DoctorCheck CreateRequiredValueCheck(string name, string value, bool maskValue = false)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return new DoctorCheck(name, DoctorStatus.Pass, maskValue ? "[set]" : value);
        }

        return new DoctorCheck(name, DoctorStatus.Fail, "Missing value");
    }
}
