using Hermes.Core.Tools;

namespace Hermes.Adapters;

/// <summary>
/// Pass-through stub implementation of <see cref="IFunctionToolAdapter"/>.
/// No MAF dependency — M4 will provide the real implementation.
/// </summary>
public sealed class FunctionToolAdapterStub : IFunctionToolAdapter
{
    // M2 safe-category whitelist — mirrors IToolRegistry contract
    private static readonly HashSet<ToolCategory> _allowedCategories =
    [
        ToolCategory.ReadFile,
        ToolCategory.SystemInfo,
        ToolCategory.TextProcessing
    ];

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MafFunctionToolDescriptor>> ProjectFunctionToolsAsync(
        IToolRegistry registry,
        CancellationToken cancellationToken = default)
    {
        var result = new List<MafFunctionToolDescriptor>();
        foreach (var category in Enum.GetValues<ToolCategory>())
        {
            await foreach (var tool in registry.ListToolsByCategory(category)
                               .WithCancellation(cancellationToken))
            {
                result.Add(ToMafFunctionTool(tool));
            }
        }
        return result;
    }

    /// <inheritdoc/>
    public MafFunctionToolDescriptor ToMafFunctionTool(ToolDefinition tool)
    {
        return new MafFunctionToolDescriptor
        {
            Name = tool.Name,
            Description = tool.Description,
            Category = tool.Category,
            IsAllowed = IsCategoryAllowed(tool.Category),
            Parameters = tool.Parameters.Select(p => new MafParameterDescriptor
            {
                Name = p.Name,
                Type = p.Type,
                IsRequired = p.Required,
                Description = p.Description
            }).ToList(),
            MaxInputSize = tool.MaxInputSize,
            TimeoutMs = tool.TimeoutMs
        };
    }

    /// <inheritdoc/>
    public bool IsCategoryAllowed(ToolCategory category) =>
        _allowedCategories.Contains(category);

    /// <inheritdoc/>
    public IReadOnlyList<MafFunctionToolDescriptor> FilterAllowed(
        IReadOnlyList<MafFunctionToolDescriptor> tools) =>
        tools.Where(t => t.IsAllowed).ToList();
}
