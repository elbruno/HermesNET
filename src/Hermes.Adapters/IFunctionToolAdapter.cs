using Hermes.Core.Tools;

namespace Hermes.Adapters;

/// <summary>
/// Adapter contract that bridges <see cref="IToolRegistry"/> to the MAF function-tool surface.
///
/// <para>
/// MAF (M4) invokes tools as strongly-typed "function tools" with a JSON-schema parameter
/// contract. This adapter translates Hermes <see cref="ToolDefinition"/> objects — including
/// category-based sandboxing — into the MAF function-tool descriptor format.
/// </para>
///
/// <para>
/// M3 status: stub only. No MAF dependency is present yet. All methods return
/// pass-through or placeholder results. The interface shape matches what M4 will require.
/// </para>
/// </summary>
public interface IFunctionToolAdapter
{
    /// <summary>
    /// Projects all tools currently registered in <paramref name="registry"/> into
    /// a list of MAF function-tool descriptors.
    /// </summary>
    /// <param name="registry">The source tool registry.</param>
    /// <param name="cancellationToken">Propagates cancellation.</param>
    /// <returns>An ordered list of <see cref="MafFunctionToolDescriptor"/> instances.</returns>
    Task<IReadOnlyList<MafFunctionToolDescriptor>> ProjectFunctionToolsAsync(
        IToolRegistry registry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a single <see cref="ToolDefinition"/> into a <see cref="MafFunctionToolDescriptor"/>.
    /// </summary>
    MafFunctionToolDescriptor ToMafFunctionTool(ToolDefinition tool);

    /// <summary>
    /// Returns whether the given <see cref="ToolCategory"/> is allowed in the MAF execution
    /// context. In M3 this mirrors the M2 safe-category whitelist.
    /// </summary>
    bool IsCategoryAllowed(ToolCategory category);

    /// <summary>
    /// Filters <paramref name="tools"/> to only those categories allowed for MAF execution.
    /// </summary>
    IReadOnlyList<MafFunctionToolDescriptor> FilterAllowed(
        IReadOnlyList<MafFunctionToolDescriptor> tools);
}

/// <summary>
/// Lightweight DTO representing a MAF function-tool entry projected from a Hermes tool definition.
/// This is a stub type; M4 will replace it with the real MAF function-tool descriptor.
/// </summary>
public sealed class MafFunctionToolDescriptor
{
    /// <summary>Tool name — forwarded verbatim from <see cref="ToolDefinition.Name"/>.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description.</summary>
    public required string Description { get; init; }

    /// <summary>Hermes category preserved for sandbox enforcement.</summary>
    public required ToolCategory Category { get; init; }

    /// <summary>Whether this tool is allowed under the M2/M3 safe-category whitelist.</summary>
    public required bool IsAllowed { get; init; }

    /// <summary>
    /// JSON-schema-compatible parameter descriptors projected from
    /// <see cref="ToolDefinition.Parameters"/>.
    /// </summary>
    public IReadOnlyList<MafParameterDescriptor> Parameters { get; init; } = [];

    /// <summary>Maximum input size in bytes forwarded from <see cref="ToolDefinition.MaxInputSize"/>.</summary>
    public int MaxInputSize { get; init; }

    /// <summary>Timeout in milliseconds forwarded from <see cref="ToolDefinition.TimeoutMs"/>.</summary>
    public int TimeoutMs { get; init; }
}

/// <summary>
/// Lightweight DTO for a single MAF function-tool parameter.
/// Stub for M3; replaced by MAF schema type in M4.
/// </summary>
public sealed class MafParameterDescriptor
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required bool IsRequired { get; init; }
    public string? Description { get; init; }
}
