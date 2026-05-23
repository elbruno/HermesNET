namespace Hermes.Core.Policy;

/// <summary>
/// Immutable result returned by every <see cref="IPolicyEngine"/> evaluation method.
/// Carries the verdict, human-readable reason, optional redacted input, and diagnostic metadata.
/// </summary>
public sealed record PolicyResult
{
    /// <summary>Enforcement verdict.</summary>
    public required PolicyVerdict Verdict { get; init; }

    /// <summary>Human-readable explanation (never null or empty).</summary>
    public required string Reason { get; init; }

    /// <summary>
    /// When <see cref="Verdict"/> is <see cref="PolicyVerdict.Redact"/>, contains the
    /// sanitised version of the original input with sensitive tokens replaced.
    /// <c>null</c> for Allow/Deny verdicts.
    /// </summary>
    public string? RedactedInput { get; init; }

    /// <summary>
    /// Optional diagnostic key-value pairs (e.g., <c>"matched_rule" → "skill-id:system-shell-execute"</c>).
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>();

    // ── Static convenience factories ─────────────────────────────────────────

    /// <summary>Creates an Allow result with the supplied reason.</summary>
    public static PolicyResult Allow(string reason, IReadOnlyDictionary<string, string>? metadata = null)
        => new()
        {
            Verdict  = PolicyVerdict.Allow,
            Reason   = reason,
            Metadata = metadata ?? new Dictionary<string, string>(),
        };

    /// <summary>Creates a Deny result with the supplied reason.</summary>
    public static PolicyResult Deny(string reason, IReadOnlyDictionary<string, string>? metadata = null)
        => new()
        {
            Verdict  = PolicyVerdict.Deny,
            Reason   = reason,
            Metadata = metadata ?? new Dictionary<string, string>(),
        };

    /// <summary>Creates a Redact result with the sanitised input and supplied reason.</summary>
    public static PolicyResult Redact(string reason, string redactedInput, IReadOnlyDictionary<string, string>? metadata = null)
        => new()
        {
            Verdict      = PolicyVerdict.Redact,
            Reason       = reason,
            RedactedInput = redactedInput,
            Metadata     = metadata ?? new Dictionary<string, string>(),
        };
}
