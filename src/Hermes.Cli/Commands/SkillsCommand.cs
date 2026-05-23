using System.CommandLine;
using System.CommandLine.Invocation;
using Hermes.Core.Skills;

namespace Hermes.Cli.Commands;

/// <summary>
/// Provides the <c>hermes skills</c> command group.
/// Currently exposes: <c>hermes skills list</c>
/// </summary>
public static class SkillsCommand
{
    public static Command Build(ISkillRegistry registry)
    {
        var skillsCmd = new Command("skills", "Manage and inspect loaded skills");
        skillsCmd.Add(BuildListCommand(registry));
        return skillsCmd;
    }

    // ── hermes skills list ─────────────────────────────────────────────────────

    private static Command BuildListCommand(ISkillRegistry registry)
    {
        var cmd = new Command("list", "List all skills loaded in the registry");
        cmd.Action = new ListAction(registry);
        return cmd;
    }

    private sealed class ListAction : AsynchronousCommandLineAction
    {
        private readonly ISkillRegistry _registry;

        public ListAction(ISkillRegistry registry) => _registry = registry;

        public override async Task<int> InvokeAsync(
            ParseResult parseResult,
            CancellationToken cancellationToken = default)
        {
            var skills = await _registry.ListSkillsAsync().ConfigureAwait(false);

            if (skills.Count == 0)
            {
                Console.WriteLine("No skills loaded.");
                return 0;
            }

            Console.WriteLine($"{"ID",-30} {"TYPE",-8} {"VERSION",-10} DESCRIPTION");
            Console.WriteLine(new string('-', 80));

            foreach (var s in skills.OrderBy(s => s.Id ?? s.Name))
            {
                var id      = (s.Id ?? s.Name).PadRight(30);
                var type    = s.Type.PadRight(8);
                var version = (s.SchemaVersion?.ToString() ?? "n/a").PadRight(10);
                Console.WriteLine($"{id} {type} {version} {s.Description}");
            }

            return 0;
        }
    }
}
