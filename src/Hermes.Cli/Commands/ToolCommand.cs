using System.CommandLine;
using System.CommandLine.Invocation;
using Hermes.Core.Tools;

namespace Hermes.Cli.Commands;

/// <summary>
/// Provides the <c>hermes tool</c> command group.
/// Exposes: <c>hermes tool list [--category &lt;cat&gt;]</c>
///      and <c>hermes tool show &lt;name&gt;</c>
/// </summary>
public static class ToolCommand
{
    public static Command Build(IToolRegistry registry)
    {
        var toolCmd = new Command("tool", "Manage and inspect registered tools");
        toolCmd.Add(BuildListCommand(registry));
        toolCmd.Add(BuildShowCommand(registry));
        return toolCmd;
    }

    // ── hermes tool list [--category <cat>] ────────────────────────────────────

    private static Command BuildListCommand(IToolRegistry registry)
    {
        var categoryOption = new Option<string?>("--category", new[] { "-c" });
        categoryOption.Description = "Filter tools by category (read_file, system_info, text_processing, ...)";

        var cmd = new Command("list", "List registered tools, optionally filtered by category");
        cmd.Add(categoryOption);
        cmd.Action = new ListAction(registry, categoryOption);
        return cmd;
    }

    private sealed class ListAction : AsynchronousCommandLineAction
    {
        private readonly IToolRegistry _registry;
        private readonly Option<string?> _categoryOption;

        public ListAction(IToolRegistry registry, Option<string?> categoryOption)
        {
            _registry       = registry;
            _categoryOption = categoryOption;
        }

        public override async Task<int> InvokeAsync(
            ParseResult parseResult,
            CancellationToken cancellationToken = default)
        {
            var categoryFilter = parseResult.GetValue(_categoryOption);

            IAsyncEnumerable<ToolDefinition> source;

            if (!string.IsNullOrWhiteSpace(categoryFilter))
            {
                if (!Enum.TryParse<ToolCategory>(categoryFilter, ignoreCase: true, out var cat))
                {
                    Console.Error.WriteLine(
                        $"Unknown category '{categoryFilter}'. " +
                        $"Valid values: {string.Join(", ", Enum.GetNames<ToolCategory>())}");
                    return 1;
                }

                source = _registry.ListToolsByCategory(cat);
            }
            else
            {
                // No filter: collect all categories
                source = AllToolsAsync(_registry);
            }

            var tools = new List<ToolDefinition>();
            await foreach (var t in source.WithCancellation(cancellationToken))
                tools.Add(t);

            if (tools.Count == 0)
            {
                Console.WriteLine(categoryFilter is null
                    ? "No tools registered."
                    : $"No tools registered in category '{categoryFilter}'.");
                return 0;
            }

            Console.WriteLine(
                $"{"NAME",-30} {"CATEGORY",-16} {"TIMEOUT(ms)",-12} DESCRIPTION");
            Console.WriteLine(new string('-', 90));

            foreach (var t in tools.OrderBy(t => t.Name))
            {
                var safe = ToolRegistry.SafeCategories.Contains(t.Category) ? "" : " [DENIED]";
                Console.WriteLine(
                    $"{t.Name,-30} {t.Category,-16} {t.TimeoutMs,-12} {t.Description}{safe}");
            }

            return 0;
        }

        private static async IAsyncEnumerable<ToolDefinition> AllToolsAsync(IToolRegistry registry)
        {
            foreach (var cat in Enum.GetValues<ToolCategory>())
            {
                await foreach (var t in registry.ListToolsByCategory(cat))
                    yield return t;
            }
        }
    }

    // ── hermes tool show <name> ────────────────────────────────────────────────

    private static Command BuildShowCommand(IToolRegistry registry)
    {
        var nameArg = new Argument<string>("name") { Description = "Tool name to inspect" };
        var cmd     = new Command("show", "Show detailed information about a registered tool");
        cmd.Add(nameArg);
        cmd.Action = new ShowAction(registry, nameArg);
        return cmd;
    }

    private sealed class ShowAction : AsynchronousCommandLineAction
    {
        private readonly IToolRegistry _registry;
        private readonly Argument<string> _nameArg;

        public ShowAction(IToolRegistry registry, Argument<string> nameArg)
        {
            _registry = registry;
            _nameArg  = nameArg;
        }

        public override async Task<int> InvokeAsync(
            ParseResult parseResult,
            CancellationToken cancellationToken = default)
        {
            var name = parseResult.GetValue(_nameArg)!;

            ToolDefinition tool;
            try
            {
                tool = await _registry.GetToolAsync(name).ConfigureAwait(false);
            }
            catch (KeyNotFoundException)
            {
                Console.Error.WriteLine($"Tool '{name}' not found.");
                return 1;
            }

            var safe = ToolRegistry.SafeCategories.Contains(tool.Category)
                ? "ALLOWED"
                : "DENIED (not in M2 safe-category whitelist)";

            Console.WriteLine($"Name        : {tool.Name}");
            Console.WriteLine($"Category    : {tool.Category}");
            Console.WriteLine($"Status      : {safe}");
            Console.WriteLine($"Description : {tool.Description}");
            Console.WriteLine($"MaxInputSize: {tool.MaxInputSize} bytes");
            Console.WriteLine($"Timeout     : {tool.TimeoutMs} ms");

            if (tool.Parameters.Count > 0)
            {
                Console.WriteLine("Parameters  :");
                foreach (var p in tool.Parameters)
                {
                    var req      = p.Required ? "required" : "optional";
                    var pathInfo = p.IsFilePath ? " [file-path]" : string.Empty;
                    Console.WriteLine($"  {p.Name} ({p.Type}, {req}){pathInfo}: {p.Description}");
                }
            }

            return 0;
        }
    }
}
