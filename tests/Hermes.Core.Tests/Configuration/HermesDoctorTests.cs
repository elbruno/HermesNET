using FluentAssertions;
using Hermes.Cli.Configuration;

namespace Hermes.Core.Tests.Configuration;

public sealed class HermesDoctorTests
{
    [Fact]
    public void EvaluateReturnsWarningsForMissingUserConfigButPassesForValidRuntimeSettings()
    {
        var settings = new HermesCliSettings
        {
            Provider = HermesProviderNames.Ollama,
            Ollama = new OllamaSettings
            {
                BaseUrl = "http://localhost:11434",
                Model = "llama3"
            }
        };

        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var bundledConfigPath = Path.Combine(tempDirectory, "appsettings.json");
        File.WriteAllText(bundledConfigPath, "{}");

        var report = HermesDoctor.Evaluate(settings, Path.Combine(tempDirectory, "user", "appsettings.json"), bundledConfigPath);

        report.HasFailures.Should().BeFalse();
        report.Checks.Should().Contain(check => check.Name == "User config" && check.Status == DoctorStatus.Warning);
        report.Checks.Should().Contain(check => check.Name == "Provider" && check.Status == DoctorStatus.Pass);
    }
}
