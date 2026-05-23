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

    public HermesCliConfigStore(string? configPath = null)
    {
        _configPath = configPath ?? GetDefaultConfigPath();
    }

    public string ConfigPath => _configPath;

    public static string GetDefaultConfigPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Hermes", "appsettings.json");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".hermes", "appsettings.json");
    }

    public async Task<HermesCliSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_configPath))
        {
            return new HermesCliSettings();
        }

        await using var stream = File.OpenRead(_configPath);
        var settings = await JsonSerializer.DeserializeAsync<HermesCliSettings>(stream, JsonOptions, cancellationToken);
        return settings ?? new HermesCliSettings();
    }

    public async Task SaveAsync(HermesCliSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_configPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
