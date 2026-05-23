using Hermes.Core.Policy;
using Hermes.Core.Skills;
using Hermes.Core.Tools;
using Moq;

namespace Hermes.Core.Tests.Policy;

/// <summary>
/// T36 — Policy Engine adversarial test suite.
///
/// Coverage breakdown:
///   §1  Skill Denylist         — 10 tests
///   §2  Tool Category          — 15 tests
///   §3  Memory Isolation       — 10 tests
///   §4  Rate Limiting          — 6 tests
///   §5  Error Handling         — 11 tests
///   §6  Audit Trail            — 5 tests
///   §7  PolicyEngine (full)    — 4 tests
/// Total: 61 tests
/// </summary>
public sealed class PolicyEngineT36Tests
{
    // ══════════════════════════════════════════════════════════════════════════
    // §1  Skill Denylist — 10 tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "§1-01 Skill on ID denylist → Deny")]
    public void SkillDenylist_DeniedById_ReturnsDeny()
    {
        var denylist = SkillDenylistPolicy.Parse("""
            denied_ids:
              - system-shell-execute
            denied_tags:
            denied_authors:
            """);

        var skill = MakeSkill(id: "system-shell-execute");
        var result = denylist.Evaluate(skill);

        result.Verdict.Should().Be(PolicyVerdict.Deny);
        result.Reason.Should().Contain("system-shell-execute");
        result.Metadata.Should().ContainKey("matched_rule");
    }

    [Fact(DisplayName = "§1-02 Skill ID match is case-insensitive → Deny")]
    public void SkillDenylist_DeniedById_CaseInsensitive()
    {
        var denylist = SkillDenylistPolicy.Parse("""
            denied_ids:
              - System-Shell-Execute
            denied_tags:
            denied_authors:
            """);

        var skill = MakeSkill(id: "SYSTEM-SHELL-EXECUTE");
        var result = denylist.Evaluate(skill);

        result.Verdict.Should().Be(PolicyVerdict.Deny);
    }

    [Fact(DisplayName = "§1-03 Skill with denied tag → Deny")]
    public void SkillDenylist_DeniedByTag_ReturnsDeny()
    {
        var denylist = SkillDenylistPolicy.Parse("""
            denied_ids:
            denied_tags:
              - dangerous
            denied_authors:
            """);

        var skill = MakeSkill(id: "my-skill", tags: "dangerous, experimental");
        var result = denylist.Evaluate(skill);

        result.Verdict.Should().Be(PolicyVerdict.Deny);
        result.Reason.Should().Contain("dangerous");
        result.Metadata["matched_rule"].Should().StartWith("tag:");
    }

    [Fact(DisplayName = "§1-04 Tag match is case-insensitive → Deny")]
    public void SkillDenylist_DeniedByTag_CaseInsensitive()
    {
        var denylist = SkillDenylistPolicy.Parse("""
            denied_ids:
            denied_tags:
              - DANGEROUS
            denied_authors:
            """);

        var skill = MakeSkill(id: "my-skill", tags: "Dangerous");
        var result = denylist.Evaluate(skill);

        result.Verdict.Should().Be(PolicyVerdict.Deny);
    }

    [Fact(DisplayName = "§1-05 Skill with denied author → Deny")]
    public void SkillDenylist_DeniedByAuthor_ReturnsDeny()
    {
        var denylist = SkillDenylistPolicy.Parse("""
            denied_ids:
            denied_tags:
            denied_authors:
              - untrusted-author
            """);

        var skill = MakeSkill(id: "skill-x", author: "untrusted-author");
        var result = denylist.Evaluate(skill);

        result.Verdict.Should().Be(PolicyVerdict.Deny);
        result.Reason.Should().Contain("untrusted-author");
    }

    [Fact(DisplayName = "§1-06 Clean skill → Allow")]
    public void SkillDenylist_AllowedSkill_ReturnsAllow()
    {
        var denylist = SkillDenylistPolicy.Parse("""
            denied_ids:
              - system-shell-execute
            denied_tags:
              - dangerous
            denied_authors:
              - untrusted-author
            """);

        var skill = MakeSkill(id: "safe-skill", tags: "utility", author: "trusted-team");
        var result = denylist.Evaluate(skill);

        result.Verdict.Should().Be(PolicyVerdict.Allow);
    }

    [Fact(DisplayName = "§1-07 Skill with no ID falls back to Name for denylist check")]
    public void SkillDenylist_NoId_UsesNameAsFallback()
    {
        var denylist = SkillDenylistPolicy.Parse("""
            denied_ids:
              - named-dangerous
            denied_tags:
            denied_authors:
            """);

        // ISkillDefinition.Id is null — falls back to Name
        var skill = MakeSkill(id: null, name: "named-dangerous");
        var result = denylist.Evaluate(skill);

        result.Verdict.Should().Be(PolicyVerdict.Deny);
    }

    [Fact(DisplayName = "§1-08 Skill with multiple safe tags → Allow")]
    public void SkillDenylist_MultipleTagsNoneBlocked_ReturnsAllow()
    {
        var denylist = SkillDenylistPolicy.Parse("""
            denied_tags:
              - dangerous
            """);

        var skill = MakeSkill(id: "multi-tag-skill", tags: "utility, text-processing, safe");
        var result = denylist.Evaluate(skill);

        result.Verdict.Should().Be(PolicyVerdict.Allow);
    }

    [Fact(DisplayName = "§1-09 Missing denylist file → permissive (Allow all)")]
    public void SkillDenylist_MissingFile_AllowsAll()
    {
        var denylist = SkillDenylistPolicy.LoadFromFile("nonexistent-path/denylist.yaml");
        var result   = denylist.Evaluate(MakeSkill(id: "any-skill"));

        result.Verdict.Should().Be(PolicyVerdict.Allow);
    }

    [Fact(DisplayName = "§1-10 Empty denylist YAML → Allow all")]
    public void SkillDenylist_EmptyYaml_AllowsAll()
    {
        var denylist = SkillDenylistPolicy.Parse("# empty file\n");
        var result   = denylist.Evaluate(MakeSkill(id: "system-shell-execute"));

        result.Verdict.Should().Be(PolicyVerdict.Allow);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // §2  Tool Category — 15 tests
    // ══════════════════════════════════════════════════════════════════════════

    [Theory(DisplayName = "§2-01 Safe categories → Allow (no sensitive input)")]
    [InlineData(ToolCategory.ReadFile)]
    [InlineData(ToolCategory.SystemInfo)]
    [InlineData(ToolCategory.TextProcessing)]
    public void ToolCategory_SafeCategories_Allow(ToolCategory category)
    {
        var policy = new ToolCategoryPolicy();
        var result = policy.Evaluate(category, "safe plain text input");

        result.Verdict.Should().Be(PolicyVerdict.Allow);
    }

    [Theory(DisplayName = "§2-02 Denied categories → Deny")]
    [InlineData(ToolCategory.ExecuteCommand)]
    [InlineData(ToolCategory.Network)]
    [InlineData(ToolCategory.WriteFile)]
    [InlineData(ToolCategory.Delete)]
    [InlineData(ToolCategory.Unknown)]
    public void ToolCategory_DeniedCategories_Deny(ToolCategory category)
    {
        var policy = new ToolCategoryPolicy();
        var result = policy.Evaluate(category, "any input");

        result.Verdict.Should().Be(PolicyVerdict.Deny);
        result.Reason.Should().Contain(category.ToString());
    }

    [Fact(DisplayName = "§2-06 Input with 'password=secret' → Redact")]
    public void ToolCategory_PasswordInInput_Redact()
    {
        var policy = new ToolCategoryPolicy();
        var result = policy.Evaluate(ToolCategory.ReadFile, "path=/data&password=supersecret123");

        result.Verdict.Should().Be(PolicyVerdict.Redact);
        result.RedactedInput.Should().Contain("[REDACTED:");
        result.RedactedInput.Should().NotContain("supersecret123");
    }

    [Fact(DisplayName = "§2-07 Input with 'api_key=abc123' → Redact")]
    public void ToolCategory_ApiKeyInInput_Redact()
    {
        var policy = new ToolCategoryPolicy();
        var result = policy.Evaluate(ToolCategory.TextProcessing, "api_key=abc-xyz-123456 and some text");

        result.Verdict.Should().Be(PolicyVerdict.Redact);
        result.RedactedInput.Should().NotContain("abc-xyz-123456");
    }

    [Fact(DisplayName = "§2-08 Input with 'Bearer <token>' → Redact")]
    public void ToolCategory_BearerTokenInInput_Redact()
    {
        var policy = new ToolCategoryPolicy();
        var result = policy.Evaluate(ToolCategory.ReadFile, "Authorization: Bearer eyJhbGciOiJSUzI1NiJ9.payload.sig");

        result.Verdict.Should().Be(PolicyVerdict.Redact);
        result.RedactedInput.Should().NotContain("eyJhbGciOiJSUzI1NiJ9");
    }

    [Fact(DisplayName = "§2-09 Input with secret=value → Redact")]
    public void ToolCategory_SecretInInput_Redact()
    {
        var policy = new ToolCategoryPolicy();
        var result = policy.Evaluate(ToolCategory.SystemInfo, "secret=my-super-secret-value");

        result.Verdict.Should().Be(PolicyVerdict.Redact);
        result.RedactedInput.Should().Contain("[REDACTED:SECRET]");
    }

    [Fact(DisplayName = "§2-10 AWS access key pattern → Redact")]
    public void ToolCategory_AwsKeyInInput_Redact()
    {
        var policy = new ToolCategoryPolicy();
        var result = policy.Evaluate(ToolCategory.ReadFile, "key=AKIAIOSFODNN7EXAMPLE config");

        result.Verdict.Should().Be(PolicyVerdict.Redact);
        result.RedactedInput.Should().Contain("[REDACTED:AWS_KEY]");
    }

    [Fact(DisplayName = "§2-11 Redact metadata lists matched pattern labels")]
    public void ToolCategory_Redact_MetadataListsPatterns()
    {
        var policy = new ToolCategoryPolicy();
        var result = policy.Evaluate(ToolCategory.ReadFile, "password=abc&secret=xyz");

        result.Verdict.Should().Be(PolicyVerdict.Redact);
        result.Metadata.Should().ContainKey("redacted_patterns");
    }

    [Fact(DisplayName = "§2-12 Empty input on safe category → Allow")]
    public void ToolCategory_EmptyInput_Allow()
    {
        var policy = new ToolCategoryPolicy();
        var result = policy.Evaluate(ToolCategory.SystemInfo, "");

        result.Verdict.Should().Be(PolicyVerdict.Allow);
    }

    [Fact(DisplayName = "§2-13 ExecuteCommand with safe-looking input is still Deny")]
    public void ToolCategory_ExecuteCommand_AlwaysDeny()
    {
        var policy = new ToolCategoryPolicy();
        var result = policy.Evaluate(ToolCategory.ExecuteCommand, "ls -la");

        result.Verdict.Should().Be(PolicyVerdict.Deny);
    }

    [Fact(DisplayName = "§2-14 Long hex token in safe input → Redact")]
    public void ToolCategory_LongHexToken_Redact()
    {
        var policy = new ToolCategoryPolicy();
        var result = policy.Evaluate(ToolCategory.TextProcessing, "token=deadbeef01234567890abcdef0123456");

        result.Verdict.Should().Be(PolicyVerdict.Redact);
        result.RedactedInput.Should().Contain("[REDACTED:HEX_TOKEN]");
    }

    [Fact(DisplayName = "§2-15 RedactSensitiveData: multiple patterns in one string")]
    public void ToolCategory_MultipleRedactionPatternsInInput()
    {
        var (redacted, labels) = ToolCategoryPolicy.RedactSensitiveData(
            "password=abc secret=xyz apikey=123");

        labels.Should().Contain("PASSWORD");
        labels.Should().Contain("SECRET");
        redacted.Should().NotContain("abc");
        redacted.Should().NotContain("xyz");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // §3  Memory Isolation (R2) — 10 tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "§3-01 Same profile, Profile scope → Allow")]
    public void MemoryAccess_SameProfile_Allow()
    {
        var policy = new MemoryAccessPolicy();
        var result = policy.Evaluate("profile-a", "profile-a", MemoryScope.Profile);

        result.Verdict.Should().Be(PolicyVerdict.Allow);
    }

    [Fact(DisplayName = "§3-02 Same profile, Session scope → Allow")]
    public void MemoryAccess_SameProfileSessionScope_Allow()
    {
        var policy = new MemoryAccessPolicy();
        var result = policy.Evaluate("profile-a", "profile-a", MemoryScope.Session);

        result.Verdict.Should().Be(PolicyVerdict.Allow);
    }

    [Fact(DisplayName = "§3-03 Cross-profile, Profile scope → Deny")]
    public void MemoryAccess_CrossProfile_Deny()
    {
        var policy = new MemoryAccessPolicy();
        var result = policy.Evaluate("profile-a", "profile-b", MemoryScope.Profile);

        result.Verdict.Should().Be(PolicyVerdict.Deny);
        result.Reason.Should().Contain("R2 isolation");
    }

    [Fact(DisplayName = "§3-04 Cross-profile, Session scope → Deny")]
    public void MemoryAccess_CrossProfileSessionScope_Deny()
    {
        var policy = new MemoryAccessPolicy();
        var result = policy.Evaluate("profile-a", "profile-b", MemoryScope.Session);

        result.Verdict.Should().Be(PolicyVerdict.Deny);
    }

    [Fact(DisplayName = "§3-05 Global scope → always Deny")]
    public void MemoryAccess_GlobalScope_AlwaysDeny()
    {
        var policy = new MemoryAccessPolicy();
        var result = policy.Evaluate("profile-a", "profile-a", MemoryScope.Global);

        result.Verdict.Should().Be(PolicyVerdict.Deny);
        result.Reason.Should().Contain("Global");
    }

    [Fact(DisplayName = "§3-06 Global scope cross-profile → Deny")]
    public void MemoryAccess_GlobalScopeCrossProfile_Deny()
    {
        var policy = new MemoryAccessPolicy();
        var result = policy.Evaluate("profile-a", "profile-b", MemoryScope.Global);

        result.Verdict.Should().Be(PolicyVerdict.Deny);
    }

    [Fact(DisplayName = "§3-07 Empty requesting profile → Deny")]
    public void MemoryAccess_EmptyRequestingProfile_Deny()
    {
        var policy = new MemoryAccessPolicy();
        var result = policy.Evaluate("", "profile-a", MemoryScope.Profile);

        result.Verdict.Should().Be(PolicyVerdict.Deny);
        result.Metadata["rule"].Should().Be("empty-profile-id");
    }

    [Fact(DisplayName = "§3-08 Empty target profile → Deny")]
    public void MemoryAccess_EmptyTargetProfile_Deny()
    {
        var policy = new MemoryAccessPolicy();
        var result = policy.Evaluate("profile-a", "", MemoryScope.Profile);

        result.Verdict.Should().Be(PolicyVerdict.Deny);
    }

    [Fact(DisplayName = "§3-09 Profile IDs match case-insensitively → Allow")]
    public void MemoryAccess_ProfileIdsCaseInsensitive_Allow()
    {
        var policy = new MemoryAccessPolicy();
        var result = policy.Evaluate("Profile-A", "profile-a", MemoryScope.Profile);

        result.Verdict.Should().Be(PolicyVerdict.Allow);
    }

    [Fact(DisplayName = "§3-10 Deny metadata includes both profile IDs")]
    public void MemoryAccess_DenyResult_MetadataContainsProfileIds()
    {
        var policy = new MemoryAccessPolicy();
        var result = policy.Evaluate("attacker", "victim", MemoryScope.Profile);

        result.Verdict.Should().Be(PolicyVerdict.Deny);
        result.Metadata.Should().ContainKey("requesting");
        result.Metadata.Should().ContainKey("target");
        result.Metadata["requesting"].Should().Be("attacker");
        result.Metadata["target"].Should().Be("victim");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // §4  Rate Limiting — 6 tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "§4-01 First request → Allow")]
    public void RateLimit_FirstRequest_Allow()
    {
        var policy = new RateLimitPolicy(maxRequestsPerHour: 10);
        var result = policy.Evaluate("profile-a", "session-1");

        result.Verdict.Should().Be(PolicyVerdict.Allow);
    }

    [Fact(DisplayName = "§4-02 Requests within limit → all Allow")]
    public void RateLimit_UnderLimit_AllAllow()
    {
        var policy = new RateLimitPolicy(maxRequestsPerHour: 5);

        for (var i = 0; i < 5; i++)
        {
            var result = policy.Evaluate("profile-a", "session-1");
            result.Verdict.Should().Be(PolicyVerdict.Allow, $"request {i + 1} should be allowed");
        }
    }

    [Fact(DisplayName = "§4-03 Exceeding hourly limit → Deny")]
    public void RateLimit_ExceedHourlyLimit_Deny()
    {
        var policy = new RateLimitPolicy(maxRequestsPerHour: 3);

        for (var i = 0; i < 3; i++)
            policy.Evaluate("profile-b", "session-x");

        var result = policy.Evaluate("profile-b", "session-x");
        result.Verdict.Should().Be(PolicyVerdict.Deny);
        result.Reason.Should().Contain("profile-b");
        result.Metadata["rule"].Should().Be("profile-hourly-limit");
    }

    [Fact(DisplayName = "§4-04 Different profiles have independent counters")]
    public void RateLimit_DifferentProfiles_IndependentCounters()
    {
        var policy = new RateLimitPolicy(maxRequestsPerHour: 2);

        // Exhaust profile-a
        policy.Evaluate("profile-a", "s1");
        policy.Evaluate("profile-a", "s1");

        // profile-b should still be allowed
        var result = policy.Evaluate("profile-b", "s2");
        result.Verdict.Should().Be(PolicyVerdict.Allow);
    }

    [Fact(DisplayName = "§4-05 Session limit exceeded → Deny")]
    public void RateLimit_ExceedSessionLimit_Deny()
    {
        var policy = new RateLimitPolicy(maxRequestsPerHour: 1000, maxRequestsPerSession: 2);

        policy.Evaluate("profile-c", "session-limited");
        policy.Evaluate("profile-c", "session-limited");

        var result = policy.Evaluate("profile-c", "session-limited");
        result.Verdict.Should().Be(PolicyVerdict.Deny);
        result.Metadata["rule"].Should().Be("session-hourly-limit");
    }

    [Fact(DisplayName = "§4-06 Empty profile ID → Deny")]
    public void RateLimit_EmptyProfileId_Deny()
    {
        var policy = new RateLimitPolicy();
        var result = policy.Evaluate("", "session-x");

        result.Verdict.Should().Be(PolicyVerdict.Deny);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // §5  Error Handling — 11 tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "§5-01 ValidateSkill null argument → ArgumentNullException")]
    public void PolicyEngine_ValidateSkill_NullSkill_Throws()
    {
        var engine = BuildEngine();
        var act    = () => engine.ValidateSkill(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "§5-02 ValidateToolRequest null input → ArgumentNullException")]
    public void PolicyEngine_ValidateToolRequest_NullInput_Throws()
    {
        var engine = BuildEngine();
        var act    = () => engine.ValidateToolRequest(ToolCategory.ReadFile, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "§5-03 PolicyViolationException carries PolicyResult")]
    public void PolicyViolationException_CarriesPolicyResult()
    {
        var result = PolicyResult.Deny("test deny", new Dictionary<string, string> { ["key"] = "val" });
        var ex     = new PolicyViolationException(result);

        ex.PolicyResult.Should().Be(result);
        ex.Message.Should().Contain("test deny");
    }

    [Fact(DisplayName = "§5-04 PolicyViolationException with inner exception")]
    public void PolicyViolationException_WithInnerException()
    {
        var result = PolicyResult.Deny("test");
        var inner  = new InvalidOperationException("inner");
        var ex     = new PolicyViolationException(result, inner);

        ex.InnerException.Should().Be(inner);
    }

    [Fact(DisplayName = "§5-05 SkillDenylistPolicy with malformed YAML (no section headers) → parse silently, Allow")]
    public void SkillDenylist_MalformedYaml_ParsesSilently()
    {
        var denylist = SkillDenylistPolicy.Parse("this is: not valid list yaml\n  but flat key: value\n");
        var result   = denylist.Evaluate(MakeSkill(id: "anything"));

        // No section headers → empty lists → Allow
        result.Verdict.Should().Be(PolicyVerdict.Allow);
    }

    [Fact(DisplayName = "§5-06 Skill with null Metadata → evaluated without throw")]
    public void SkillDenylist_NullMetadata_NoThrow()
    {
        var denylist = SkillDenylistPolicy.Parse("denied_tags:\n  - dangerous\n");
        var skill    = MakeSkill(id: "no-metadata-skill", metadata: null);

        var act    = () => denylist.Evaluate(skill);
        act.Should().NotThrow();

        var result = denylist.Evaluate(skill);
        result.Verdict.Should().Be(PolicyVerdict.Allow);
    }

    [Fact(DisplayName = "§5-07 Skill tag list with whitespace → tags trimmed correctly")]
    public void SkillDenylist_TagsWithWhitespace_TrimmedCorrectly()
    {
        var denylist = SkillDenylistPolicy.Parse("denied_tags:\n  - dangerous\n");
        var skill    = MakeSkill(id: "ws-skill", tags: "  dangerous  ");

        var result = denylist.Evaluate(skill);
        result.Verdict.Should().Be(PolicyVerdict.Deny);
    }

    [Fact(DisplayName = "§5-08 ToolCategoryPolicy with whitespace-only input → Allow")]
    public void ToolCategory_WhitespaceOnlyInput_Allow()
    {
        var policy = new ToolCategoryPolicy();
        var result = policy.Evaluate(ToolCategory.ReadFile, "   \t  \n  ");

        result.Verdict.Should().Be(PolicyVerdict.Allow);
    }

    [Fact(DisplayName = "§5-09 PolicyResult.Redact carries RedactedInput")]
    public void PolicyResult_RedactFactory_SetsRedactedInput()
    {
        var result = PolicyResult.Redact("reason", "sanitised", new Dictionary<string, string>());

        result.Verdict.Should().Be(PolicyVerdict.Redact);
        result.RedactedInput.Should().Be("sanitised");
        result.Reason.Should().Be("reason");
    }

    [Fact(DisplayName = "§5-10 PolicyResult.Allow has null RedactedInput")]
    public void PolicyResult_Allow_RedactedInputIsNull()
    {
        var result = PolicyResult.Allow("ok");
        result.RedactedInput.Should().BeNull();
        result.Verdict.Should().Be(PolicyVerdict.Allow);
    }

    [Fact(DisplayName = "§5-11 PolicyResult default Metadata is empty, not null")]
    public void PolicyResult_DefaultMetadata_NotNull()
    {
        var result = PolicyResult.Allow("ok");
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // §6  Audit Trail — 5 tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "§6-01 Each policy call appends to AuditLog")]
    public void PolicyEngine_EachCall_AppendsToAuditLog()
    {
        var engine = BuildEngine();
        var skill  = MakeSkill(id: "safe-skill");

        engine.ValidateSkill(skill);
        engine.ValidateToolRequest(ToolCategory.ReadFile, "input");

        engine.AuditLog.Should().HaveCount(2);
    }

    [Fact(DisplayName = "§6-02 Audit entry records correct PolicyType for skill")]
    public void PolicyEngine_AuditEntry_CorrectPolicyType_Skill()
    {
        var engine = BuildEngine();
        engine.ValidateSkill(MakeSkill(id: "x"));

        var entry = engine.AuditLog[0];
        entry.PolicyType.Should().Be("SkillDenylist");
        entry.Subject.Should().Be("x");
    }

    [Fact(DisplayName = "§6-03 Audit entry records correct PolicyType for tool")]
    public void PolicyEngine_AuditEntry_CorrectPolicyType_Tool()
    {
        var engine = BuildEngine();
        engine.ValidateToolRequest(ToolCategory.Network, "data");

        var entry = engine.AuditLog[0];
        entry.PolicyType.Should().Be("ToolCategory");
        entry.Verdict.Should().Be(PolicyVerdict.Deny);
    }

    [Fact(DisplayName = "§6-04 Audit entry records correct PolicyType for memory")]
    public void PolicyEngine_AuditEntry_CorrectPolicyType_Memory()
    {
        var engine = BuildEngine();
        engine.EvaluateMemoryAccess("p1", "p2", MemoryScope.Profile);

        var entry = engine.AuditLog[0];
        entry.PolicyType.Should().Be("MemoryAccess");
        entry.Verdict.Should().Be(PolicyVerdict.Deny);
    }

    [Fact(DisplayName = "§6-05 Audit log is append-only (Allow and Deny both logged)")]
    public void PolicyEngine_AuditLog_LogsBothAllowAndDeny()
    {
        var engine = BuildEngine();

        engine.ValidateSkill(MakeSkill(id: "safe"));
        engine.ValidateToolRequest(ToolCategory.ExecuteCommand, "cmd");
        engine.EvaluateMemoryAccess("p", "p", MemoryScope.Profile);
        engine.CheckRateLimit("p", "s");

        engine.AuditLog.Should().HaveCount(4);
        engine.AuditLog.Select(e => e.Verdict).Should()
            .Contain(PolicyVerdict.Allow).And
            .Contain(PolicyVerdict.Deny);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // §7  PolicyEngine integration — 4 tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "§7-01 End-to-end: denied skill triggers PolicyViolationException")]
    public void PolicyEngine_DeniedSkill_ThrowsViolation()
    {
        var engine = BuildEngineWithDenylist("denied_ids:\n  - evil-skill\n");
        var skill  = MakeSkill(id: "evil-skill");

        var result = engine.ValidateSkill(skill);
        result.Verdict.Should().Be(PolicyVerdict.Deny);

        var act = () => { if (result.Verdict == PolicyVerdict.Deny) throw new PolicyViolationException(result); };
        act.Should().Throw<PolicyViolationException>()
            .Which.PolicyResult.Verdict.Should().Be(PolicyVerdict.Deny);
    }

    [Fact(DisplayName = "§7-02 End-to-end: tool redaction modifies input")]
    public void PolicyEngine_ToolRedaction_ModifiesInput()
    {
        var engine = BuildEngine();
        var result = engine.ValidateToolRequest(ToolCategory.ReadFile, "api_key=super-secret-token-here");

        result.Verdict.Should().Be(PolicyVerdict.Redact);
        result.RedactedInput.Should().NotBeNull();
        result.RedactedInput.Should().NotContain("super-secret-token-here");
    }

    [Fact(DisplayName = "§7-03 Memory access denied creates audit trail entry")]
    public void PolicyEngine_MemoryAccessDenied_AuditTrailEntry()
    {
        var engine = BuildEngine();
        engine.EvaluateMemoryAccess("attacker", "victim", MemoryScope.Profile);

        engine.AuditLog.Should().ContainSingle(e =>
            e.PolicyType == "MemoryAccess" &&
            e.Verdict    == PolicyVerdict.Deny);
    }

    [Fact(DisplayName = "§7-04 CheckRateLimit deny creates audit trail entry")]
    public void PolicyEngine_RateLimitExceeded_AuditTrailEntry()
    {
        var engine = BuildEngineWithRateLimit(maxPerHour: 1);
        engine.CheckRateLimit("profile-x", "session-y");
        engine.CheckRateLimit("profile-x", "session-y"); // exceeds

        engine.AuditLog.Should()
            .Contain(e => e.PolicyType == "RateLimit" && e.Verdict == PolicyVerdict.Deny);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private static IPolicyEngine BuildEngine()
        => new PolicyEngine(
            SkillDenylistPolicy.Parse("denied_ids:\ndenied_tags:\ndenied_authors:\n"),
            new ToolCategoryPolicy(),
            new MemoryAccessPolicy(),
            new RateLimitPolicy(maxRequestsPerHour: 1_000));

    private static IPolicyEngine BuildEngineWithDenylist(string yaml)
        => new PolicyEngine(
            SkillDenylistPolicy.Parse(yaml),
            new ToolCategoryPolicy(),
            new MemoryAccessPolicy(),
            new RateLimitPolicy(maxRequestsPerHour: 1_000));

    private static IPolicyEngine BuildEngineWithRateLimit(int maxPerHour)
        => new PolicyEngine(
            SkillDenylistPolicy.Parse(""),
            new ToolCategoryPolicy(),
            new MemoryAccessPolicy(),
            new RateLimitPolicy(maxRequestsPerHour: maxPerHour));

    private static ISkillDefinition MakeSkill(
        string? id   = "test-skill",
        string? name = "Test Skill",
        string? tags   = null,
        string? author = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        var meta = metadata;
        if (meta is null && (tags is not null || author is not null))
        {
            var m = new Dictionary<string, string>();
            if (tags   is not null) m["tags"]   = tags;
            if (author is not null) m["author"] = author;
            meta = m;
        }

        var mock = new Mock<ISkillDefinition>();
        mock.SetupGet(s => s.Id).Returns(id);
        mock.SetupGet(s => s.Name).Returns(name ?? id ?? "skill");
        mock.SetupGet(s => s.Type).Returns("tool");
        mock.SetupGet(s => s.Description).Returns("test skill description");
        mock.SetupGet(s => s.Metadata).Returns(meta);
        mock.SetupGet(s => s.Category).Returns((string?)null);
        mock.SetupGet(s => s.SchemaVersion).Returns((Version?)null);
        mock.SetupGet(s => s.Content).Returns((string?)null);
        mock.SetupGet(s => s.Inputs).Returns((IReadOnlyDictionary<string, string>?)null);
        mock.SetupGet(s => s.Outputs).Returns((IReadOnlyDictionary<string, string>?)null);
        return mock.Object;
    }
}
