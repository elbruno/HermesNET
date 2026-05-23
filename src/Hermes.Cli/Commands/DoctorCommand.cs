using Hermes.Cli.Configuration;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Hermes.Cli.Commands;

public static class DoctorCommand
{
    public static Command Build(IConfiguration configuration, HermesCliConfigStore store)
    {
        var command = new Command("doctor", "Inspect HermesNET configuration and runtime health");
        command.Action = new DoctorAction(configuration, store);
        return command;
    }

    private sealed class DoctorAction : AsynchronousCommandLineAction
    {
        private readonly IConfiguration _configuration;
        private readonly HermesCliConfigStore _store;

        public DoctorAction(IConfiguration configuration, HermesCliConfigStore store)
        {
            _configuration = configuration;
            _store = store;
        }

        public override Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
        {
            var settings = HermesCliSettings.FromConfiguration(_configuration);
            var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var report = HermesDoctor.Evaluate(settings, _store.ConfigPath, appSettingsPath);

            AnsiConsole.Write(new Rule("[bold green]HermesNET doctor[/]"));
            AnsiConsole.MarkupLine($"Effective config from [grey]{Markup.Escape(appSettingsPath)}[/]");
            AnsiConsole.MarkupLine($"User config path: [grey]{Markup.Escape(_store.ConfigPath)}[/]");
            AnsiConsole.WriteLine();

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

            AnsiConsole.WriteLine();
            if (report.HasFailures)
            {
                AnsiConsole.MarkupLine("[red]One or more checks failed.[/]");
                return Task.FromResult(1);
            }

            AnsiConsole.MarkupLine("[green]No blocking issues found.[/]");
            return Task.FromResult(0);
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
