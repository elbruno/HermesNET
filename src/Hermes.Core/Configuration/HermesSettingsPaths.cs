namespace Hermes.Core.Configuration;

public static class HermesSettingsPaths
{
    public static string GetDefaultUserConfigPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Hermes", "appsettings.json");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".hermes", "appsettings.json");
    }
}
