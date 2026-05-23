using Hermes.Core.Skills;
using Hermes.Core.Tools;

namespace Hermes.Core.Policy;

/// <summary>
/// Default <see cref="IPolicyEngine"/> implementation.
///
/// <para>Evaluation order (first Deny wins within each method):</para>
/// <list type="number">
///   <item>Skill: SkillDenylistPolicy (ID → tag → author)</item>
///   <item>Tool:  ToolCategoryPolicy (denied category → redaction scan)</item>
///   <item>Memory: MemoryAccessPolicy (global scope → cross-profile check)</item>
///   <item>Rate limit: RateLimitPolicy (profile window → session window)</item>
/// </list>
///
/// <para>Every evaluation result (regardless of verdict) is appended to
/// <see cref="AuditLog"/> for audit-trail purposes.</para>
/// </summary>
public sealed class PolicyEngine : IPolicyEngine
{
    private readonly SkillDenylistPolicy  _skillDenylist;
    private readonly ToolCategoryPolicy   _toolCategory;
    private readonly MemoryAccessPolicy   _memoryAccess;
    private readonly RateLimitPolicy      _rateLimit;

    private readonly List<PolicyAuditEntry> _auditLog = [];
    private readonly object _auditLock = new();

    // ── Construction ──────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="PolicyEngine"/> that loads the skill denylist from
    /// <paramref name="denylistYamlPath"/> (permissive if file absent).
    /// </summary>
    public PolicyEngine(
        string? denylistYamlPath = null,
        int maxRequestsPerHour = 1_000,
        int maxRequestsPerSession = 500)
    {
        _skillDenylist = SkillDenylistPolicy.LoadFromFile(
            denylistYamlPath ?? Path.Combine("config", "policies", "skill-denylist.yaml"));
        _toolCategory  = new ToolCategoryPolicy();
        _memoryAccess  = new MemoryAccessPolicy();
        _rateLimit     = new RateLimitPolicy(maxRequestsPerHour, maxRequestsPerSession);
    }

    /// <summary>
    /// Constructor for tests — accepts pre-built sub-policies.
    /// </summary>
    public PolicyEngine(
        SkillDenylistPolicy skillDenylist,
        ToolCategoryPolicy toolCategory,
        MemoryAccessPolicy memoryAccess,
        RateLimitPolicy rateLimit)
    {
        _skillDenylist = skillDenylist;
        _toolCategory  = toolCategory;
        _memoryAccess  = memoryAccess;
        _rateLimit     = rateLimit;
    }

    // ── IPolicyEngine ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public PolicyResult ValidateSkill(ISkillDefinition skill)
    {
        ArgumentNullException.ThrowIfNull(skill);

        var result  = _skillDenylist.Evaluate(skill);
        var subject = skill.Id ?? skill.Name;

        Audit("SkillDenylist", subject, result);
        return result;
    }

    /// <inheritdoc/>
    public PolicyResult ValidateToolRequest(ToolCategory category, string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = _toolCategory.Evaluate(category, input);

        Audit("ToolCategory", category.ToString(), result);
        return result;
    }

    /// <inheritdoc/>
    public PolicyResult EvaluateMemoryAccess(
        string requestingProfileId,
        string targetProfileId,
        MemoryScope scope)
    {
        var result = _memoryAccess.Evaluate(requestingProfileId, targetProfileId, scope);

        Audit("MemoryAccess", $"{requestingProfileId}->{targetProfileId}:{scope}", result);
        return result;
    }

    /// <inheritdoc/>
    public PolicyResult CheckRateLimit(string profileId, string sessionId)
    {
        var result = _rateLimit.Evaluate(profileId, sessionId);

        Audit("RateLimit", $"{profileId}:{sessionId}", result);
        return result;
    }

    /// <inheritdoc/>
    public IReadOnlyList<PolicyAuditEntry> AuditLog
    {
        get
        {
            lock (_auditLock)
                return _auditLog.ToArray();
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void Audit(string policyType, string subject, PolicyResult result)
    {
        var entry = new PolicyAuditEntry
        {
            Timestamp  = DateTimeOffset.UtcNow,
            PolicyType = policyType,
            Subject    = subject,
            Verdict    = result.Verdict,
            Reason     = result.Reason,
            Metadata   = result.Metadata,
        };

        lock (_auditLock)
            _auditLog.Add(entry);
    }
}
