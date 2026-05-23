namespace Hermes.Core.Tools;

/// <summary>
/// Immutable definition of a CLI tool registered in <see cref="IToolRegistry"/>.
/// Schema mirrors the JSON contract documented in M2-T18.
/// </summary>
public sealed class ToolDefinition
{
    /// <summary>Unique tool name (used as the registry key, case-insensitive).</summary>
    public required string Name { get; init; }

    /// <summary>Category controlling execution permissions and sandboxing rules.</summary>
    public required ToolCategory Category { get; init; }

    /// <summary>Human-readable description of what the tool does.</summary>
    public required string Description { get; init; }

    /// <summary>Ordered list of parameters accepted by this tool.</summary>
    public IReadOnlyList<ToolParameter> Parameters { get; init; } = [];

    /// <summary>
    /// Maximum combined serialised input size in bytes.
    /// Validated by <see cref="IToolRegistry.ValidateToolInvocation"/> before execution.
    /// Default: 10 240 bytes (10 KiB).
    /// </summary>
    public int MaxInputSize { get; init; } = 10_240;

    /// <summary>
    /// Maximum execution wall-clock time in milliseconds.
    /// Stored here for T19 invocation enforcement; not enforced by the registry itself.
    /// Default: 5 000 ms.
    /// </summary>
    public int TimeoutMs { get; init; } = 5_000;
}
