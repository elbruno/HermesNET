namespace Hermes.Core.Tools;

/// <summary>
/// Read-only, sandboxed tool registry for Hermes M2.
///
/// <para>
/// Only CLI tools are registered here.  Skills are managed separately via
/// <see cref="Skills.ISkillRegistry"/> and must never register tools.
/// </para>
///
/// <para>
/// Safe categories for M2 are: <see cref="ToolCategory.ReadFile"/>,
/// <see cref="ToolCategory.SystemInfo"/>, and <see cref="ToolCategory.TextProcessing"/>.
/// All other categories are denied by default.
/// </para>
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Registers a new tool definition.
    /// Throws <see cref="ArgumentException"/> if a tool with the same name (case-insensitive)
    /// is already registered.
    /// </summary>
    Task RegisterToolAsync(ToolDefinition toolDefinition);

    /// <summary>
    /// Returns the definition for the tool identified by <paramref name="name"/>.
    /// Throws <see cref="KeyNotFoundException"/> with the tool name in the message when not found.
    /// </summary>
    Task<ToolDefinition> GetToolAsync(string name);

    /// <summary>
    /// Streams all tools whose <see cref="ToolDefinition.Category"/> equals
    /// <paramref name="category"/>.
    /// Returns an empty sequence when no tools match.
    /// </summary>
    IAsyncEnumerable<ToolDefinition> ListToolsByCategory(ToolCategory category);

    /// <summary>
    /// Validates whether the tool identified by <paramref name="name"/> may be invoked
    /// with the supplied <paramref name="args"/>.
    ///
    /// <para>Checks (all must pass):</para>
    /// <list type="bullet">
    ///   <item>Tool is registered.</item>
    ///   <item>Category is in the M2 safe-category whitelist.</item>
    ///   <item>All required parameters are present.</item>
    ///   <item>File-path parameters contain no path-traversal sequences.</item>
    ///   <item>Serialised input size does not exceed <see cref="ToolDefinition.MaxInputSize"/>.</item>
    /// </list>
    ///
    /// Every call — whether allowed or denied — emits an entry to <see cref="AuditLog"/>.
    /// </summary>
    ToolInvocationValidationResult ValidateToolInvocation(
        string name,
        IReadOnlyDictionary<string, string> args);

    /// <summary>
    /// Ordered audit trail of every <see cref="ValidateToolInvocation"/> call.
    /// Consumed by the M3 policy engine (T16).
    /// </summary>
    IReadOnlyList<ToolAuditEntry> AuditLog { get; }
}
