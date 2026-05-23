namespace Hermes.Core.Tools;

/// <summary>
/// Immutable audit-log entry emitted by <see cref="IToolRegistry.ValidateToolInvocation"/>
/// for every invocation attempt — whether allowed or denied.
/// M3 policy engine (T16) will consume these entries for policy decisions.
/// </summary>
public sealed class ToolAuditEntry
{
    /// <summary>UTC timestamp of the invocation attempt.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Name of the tool that was requested.</summary>
    public required string ToolName { get; init; }

    /// <summary>Category of the tool (or "unknown" when the tool was not found).</summary>
    public required string Category { get; init; }

    /// <summary>Whether the invocation was allowed.</summary>
    public required bool Allowed { get; init; }

    /// <summary>Blocking errors raised by validation (empty when <see cref="Allowed"/> is true).</summary>
    public IReadOnlyList<string> DenialReasons { get; init; } = [];

    /// <summary>
    /// Serialised byte-length of the args payload at invocation time.
    /// Used for input-size audit.
    /// </summary>
    public int ArgsByteLength { get; init; }
}
