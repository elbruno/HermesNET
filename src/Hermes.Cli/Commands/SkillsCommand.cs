using System.CommandLine;
using System.CommandLine.Invocation;
using Hermes.Core.Skills;

namespace Hermes.Cli.Commands;

/// <summary>
/// Provides the <c>hermes skill</c> command group.
/// Subcommands: <c>hermes skill list</c>, <c>hermes skill show &lt;name&gt;</c>
/// </summary>
public static class SkillsCommand
{
    public static Command Build(ISkillRegistry registry)
    {
        var skillsCmd = new Command("skill", "Manage and inspect loaded skills");
        skillsCmd.Add(BuildListCommand(registry));
        skillsCmd.Add(BuildShowCommand(registry));
        return skillsCmd;
    }

    // -- hermes skill list -------------------------------------------------

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

    // -- hermes skill show <name> ------------------------------------------

    private static Command BuildShowCommand(ISkillRegistry registry)
    {
        var nameArg = new Argument<string>("name") { Description = "Skill ID or name to display" };
        var cmd     = new Command("show", "Display a skill definition and metadata");
        cmd.Arguments.Add(nameArg);
        cmd.Action = new ShowAction(registry, nameArg);
        return cmd;
    }

    private sealed class ShowAction : AsynchronousCommandLineAction
    {
        private readonly ISkillRegistry   _registry;
        private readonly Argument<string> _nameArg;

        public ShowAction(ISkillRegistry registry, Argument<string> nameArg)
        {
            _registry = registry;
            _nameArg  = nameArg;
        }

        public override async Task<int> InvokeAsync(
            ParseResult parseResult,
            CancellationToken cancellationToken = default)
        {
            var name = parseResult.GetValue(_nameArg)!;

            SkillDescriptor? skill = await _registry.FindByNameAsync(name).ConfigureAwait(false);
            if (skill is null)
            {
                Console.Error.WriteLine($"Skill ''{name}'' not found.");
                return 1;
            }

            Console.WriteLine($"ID:          {skill.Id ?? skill.Name}");
            Console.WriteLine($"Name:        {skill.Name}");
            Console.WriteLine($"Type:        {skill.Type}");
            Console.WriteLine($"Version:     {skill.SchemaVersion?.ToString() ?? "n/a"}");
            Console.WriteLine($"Category:    {skill.Category ?? "(none)"}");
            Console.WriteLine($"Description: {skill.Description}");

            var metadata = await _registry.GetSkillMetadataAsync(skill.Id ?? skill.Name)
                                          .ConfigureAwait(false);

            if (metadata.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Metadata:");
                foreach (var (key, val) in metadata)
                    Console.WriteLine($"  {key}: {val}");
            }

            if (!string.IsNullOrWhiteSpace(skill.Content))
            {
                Console.WriteLine();
                Console.WriteLine("Content:");
                Console.WriteLine(skill.Content);
            }

            return 0;
        }
    }
}
