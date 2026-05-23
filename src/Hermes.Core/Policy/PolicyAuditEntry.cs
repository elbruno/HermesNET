namespace Hermes.Core.Policy;

/// <summary>
/// Immutable audit record emitted by <see cref="IPolicyEngine"/> for every
/// policy decision — whether the verdict is Allow, Deny, or Redact.
/// Used to build a tamper-evident audit trail.
/// </summary>
public sealed class PolicyAuditEntry
{
    /// <summary>UTC timestamp of the evaluation.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Policy type that produced this entry (e.g., "SkillDenylist", "ToolCategory", "MemoryAccess", "RateLimit").</summary>
    public required string PolicyType { get; init; }

    /// <summary>Subject of the evaluation (e.g., skill ID, tool name, profile ID).</summary>
    public required string Subject { get; init; }

    /// <summary>The verdict returned by the policy engine.</summary>
    public required PolicyVerdict Verdict { get; init; }

    /// <summary>Human-readable reason for the verdict.</summary>
    public required string Reason { get; init; }

    /// <summary>Additional metadata attached to the evaluation result.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>();
}
