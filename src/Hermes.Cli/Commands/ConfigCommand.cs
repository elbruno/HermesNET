using Hermes.Cli.Configuration;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Hermes.Cli.Commands;

public static class ConfigCommand
{
    public static Command Build(IConfiguration configuration, HermesCliConfigStore store)
    {
        var command = new Command("config", "Configure the active LLM provider and model settings");
        command.Action = new ConfigAction(configuration, store);
        return command;
    }

    private sealed class ConfigAction : AsynchronousCommandLineAction
    {
        private readonly IConfiguration _configuration;
        private readonly HermesCliConfigStore _store;

        public ConfigAction(IConfiguration configuration, HermesCliConfigStore store)
        {
            _configuration = configuration;
            _store = store;
        }

        public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
        {
            if (Console.IsInputRedirected)
            {
                Console.Error.WriteLine("hermesnet config requires an interactive terminal.");
                return 1;
            }

            var current = HermesCliSettings.FromConfiguration(_configuration);

            AnsiConsole.Write(new Rule("[bold green]HermesNET config[/]"));
            AnsiConsole.MarkupLine($"Saving user settings to [grey]{Markup.Escape(_store.ConfigPath)}[/]");
            AnsiConsole.WriteLine();

            string[] providerChoices = current.Provider.Equals(HermesProviderNames.OpenAI, StringComparison.OrdinalIgnoreCase)
                ? [HermesProviderNames.OpenAI, HermesProviderNames.Ollama]
                : [HermesProviderNames.Ollama, HermesProviderNames.OpenAI];

            var provider = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Choose the [green]LLM provider[/]:")
                    .AddChoices(providerChoices));

            var updated = provider.Equals(HermesProviderNames.OpenAI, StringComparison.OrdinalIgnoreCase)
                ? current with
                {
                    Provider = HermesProviderNames.OpenAI,
                    OpenAI = new OpenAISettings
                    {
                        ApiKey = PromptValue("OpenAI API key", current.OpenAI.ApiKey, secret: true),
                        Model = PromptValue("OpenAI model", current.OpenAI.Model)
                    }
                }
                : current with
                {
                    Provider = HermesProviderNames.Ollama,
                    Ollama = new OllamaSettings
                    {
                        BaseUrl = PromptValue("Ollama base URL", current.Ollama.BaseUrl),
                        Model = PromptValue("Ollama model", current.Ollama.Model)
                    }
                };

            await _store.SaveAsync(updated, cancellationToken);

            var report = HermesDoctor.Evaluate(updated, _store.ConfigPath, Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
            RenderReport(report);
            return report.HasFailures ? 1 : 0;
        }

        private static string PromptValue(string label, string currentValue, bool secret = false)
        {
            var prompt = new TextPrompt<string>($"{label} [current: [grey]{Markup.Escape(Mask(currentValue))}[/]]")
                .AllowEmpty();

            if (secret)
            {
                prompt = prompt.Secret();
            }

            var value = AnsiConsole.Prompt(prompt);
            return string.IsNullOrWhiteSpace(value) ? currentValue : value.Trim();
        }

        private static string Mask(string value) => string.IsNullOrWhiteSpace(value) ? "(empty)" : "[set]";

        private static void RenderReport(DoctorReport report)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold]Configuration summary[/]"));

            var table = new Table { Border = TableBorder.Rounded };
            table.AddColumn("Check");
            table.AddColumn("Status");
            table.AddColumn("Details");

            foreach (var check in report.Checks)
            {
                table.AddRow(
                    Markup.Escape(check.Name),
                    FormatStatus(check.Status),
                    Markup.Escape(check.Details));
            }

            AnsiConsole.Write(table);
        }

        private static string FormatStatus(DoctorStatus status) => status switch
        {
            DoctorStatus.Pass => "[green]PASS[/]",
            DoctorStatus.Warning => "[yellow]WARN[/]",
            DoctorStatus.Fail => "[red]FAIL[/]",
            _ => "[grey]UNKNOWN[/]"
        };
    }
}
