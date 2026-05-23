namespace Hermes.Core.Tools;

/// <summary>
/// Describes a single parameter accepted by a <see cref="ToolDefinition"/>.
/// </summary>
public sealed class ToolParameter
{
    /// <summary>Parameter name (must match the key in the invocation args dictionary).</summary>
    public required string Name { get; init; }

    /// <summary>
    /// JSON Schema-style type hint: "string", "integer", "number", "boolean", "array", "object".
    /// Used for input validation.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>Human-readable description of what the parameter does.</summary>
    public string? Description { get; init; }

    /// <summary>Whether this parameter must be present in every invocation.</summary>
    public bool Required { get; init; } = true;

    /// <summary>
    /// For "string" parameters of category <c>read_file</c>: if true, the value is treated
    /// as a file-system path and validated against path-traversal attacks and whitelisted
    /// directories before use.
    /// </summary>
    public bool IsFilePath { get; init; }

    /// <summary>
    /// Whitelisted directory prefixes for file-path parameters (relative, e.g. "config/").
    /// Ignored when <see cref="IsFilePath"/> is false.
    /// </summary>
    public IReadOnlyList<string> AllowedPathPrefixes { get; init; } = [];
}
