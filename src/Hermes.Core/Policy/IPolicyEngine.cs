using Hermes.Core.Profiles;
using Hermes.Core.Skills;
using Hermes.Core.Tools;

namespace Hermes.Core.Policy;

/// <summary>
/// Central policy engine contract for Hermes M3C.
///
/// <para>
/// All three evaluation methods are synchronous (no I/O at evaluation time —
/// policies load config at construction). Every call emits a
/// <see cref="PolicyAuditEntry"/> into <see cref="AuditLog"/>.
/// </para>
///
/// <para>Integration points (enforced by callers):</para>
/// <list type="bullet">
///   <item>Before skill execution: call <see cref="ValidateSkill"/>; throw
///         <see cref="PolicyViolationException"/> on <see cref="PolicyVerdict.Deny"/>.</item>
///   <item>Before tool execution: call <see cref="ValidateToolRequest"/>; throw on Deny,
///         substitute <see cref="PolicyResult.RedactedInput"/> on Redact.</item>
///   <item>Before memory access: call <see cref="EvaluateMemoryAccess"/>; throw on Deny.</item>
/// </list>
/// </summary>
public interface IPolicyEngine
{
    // ── Skill validation ──────────────────────────────────────────────────────

    /// <summary>
    /// Validates a skill against the active denylist policies.
    ///
    /// <para>Checks (first match wins — Deny beats Allow):</para>
    /// <list type="bullet">
    ///   <item>Skill ID matches an entry in <c>config/policies/skill-denylist.yaml</c>.</item>
    ///   <item>Any tag in <c>skill.Metadata["tags"]</c> matches a denied tag in the denylist.</item>
    ///   <item>Skill author (Metadata["author"]) is on the denied-authors list.</item>
    /// </list>
    /// </summary>
    /// <param name="skill">The skill definition to evaluate.</param>
    /// <returns>
    ///   <see cref="PolicyVerdict.Allow"/> — skill may execute.<br/>
    ///   <see cref="PolicyVerdict.Deny"/> — skill is blocked; caller should throw
    ///   <see cref="PolicyViolationException"/>.
    /// </returns>
    PolicyResult ValidateSkill(ISkillDefinition skill);

    // ── Tool request validation ───────────────────────────────────────────────

    /// <summary>
    /// Validates a tool request against category restrictions and input redaction rules.
    ///
    /// <para>Checks:</para>
    /// <list type="bullet">
    ///   <item>Category is in the safe allowlist (<see cref="ToolCategory.ReadFile"/>,
    ///         <see cref="ToolCategory.SystemInfo"/>, <see cref="ToolCategory.TextProcessing"/>).</item>
    ///   <item>Denied categories (<see cref="ToolCategory.ExecuteCommand"/>,
    ///         <see cref="ToolCategory.Network"/>, <see cref="ToolCategory.WriteFile"/>,
    ///         <see cref="ToolCategory.Delete"/>) always return Deny.</item>
    ///   <item>Input is scanned for sensitive data (passwords, tokens, API keys, connection strings).
    ///         Returns <see cref="PolicyVerdict.Redact"/> with sanitised
    ///         <see cref="PolicyResult.RedactedInput"/> when found.</item>
    /// </list>
    /// </summary>
    /// <param name="category">The tool category being invoked.</param>
    /// <param name="input">The raw serialised tool input.</param>
    PolicyResult ValidateToolRequest(ToolCategory category, string input);

    // ── Memory access validation ──────────────────────────────────────────────

    /// <summary>
    /// Enforces R2 profile isolation for memory access.
    ///
    /// <para>Rules:</para>
    /// <list type="bullet">
    ///   <item><see cref="MemoryScope.Profile"/> — allowed only when
    ///         <paramref name="requestingProfileId"/> equals the profile that owns
    ///         the memory (<paramref name="targetProfileId"/>).</item>
    ///   <item><see cref="MemoryScope.Session"/> — allowed only within the same profile.</item>
    ///   <item><see cref="MemoryScope.Global"/> — always denied in M3.</item>
    /// </list>
    /// </summary>
    /// <param name="requestingProfileId">The profile making the request.</param>
    /// <param name="targetProfileId">The profile whose memory is being accessed.</param>
    /// <param name="scope">The scope of the access request.</param>
    PolicyResult EvaluateMemoryAccess(string requestingProfileId, string targetProfileId, MemoryScope scope);

    // ── Rate-limit validation ─────────────────────────────────────────────────

    /// <summary>
    /// Checks whether the profile has exceeded its request rate limit.
    /// </summary>
    /// <param name="profileId">Profile making the request.</param>
    /// <param name="sessionId">Session making the request (for per-session counters).</param>
    PolicyResult CheckRateLimit(string profileId, string sessionId);

    // ── Audit trail ───────────────────────────────────────────────────────────

    /// <summary>
    /// Ordered, append-only audit trail of every policy evaluation.
    /// Never null; empty until the first evaluation call.
    /// </summary>
    IReadOnlyList<PolicyAuditEntry> AuditLog { get; }
}
