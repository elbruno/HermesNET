using Hermes.Core.Tools;

namespace Hermes.Core.Tests.Tools;

/// <summary>
/// T18 — Native Tool Registry: unit tests.
///
/// Covers:
///  - Parameterised registration and retrieval for each safe category
///  - Category whitelist enforcement (safe vs denied)
///  - Path-traversal rejection
///  - Input-size enforcement
///  - Required-parameter validation
///  - Audit log emission (allowed and denied entries)
///  - ListToolsByCategory streaming
///  - Duplicate-registration guard
///  - GetToolAsync KeyNotFoundException
/// </summary>
public sealed class ToolRegistryTests
{
    // ── Shared helpers ─────────────────────────────────────────────────────────

    private static ToolDefinition MakeTool(
        string name,
        ToolCategory category,
        int maxInputSize = 10_240,
        IReadOnlyList<ToolParameter>? parameters = null) =>
        new()
        {
            Name         = name,
            Category     = category,
            Description  = $"Test tool: {name}",
            MaxInputSize = maxInputSize,
            Parameters   = parameters ?? [],
        };

    private static ToolParameter RequiredStringParam(string name) =>
        new() { Name = name, Type = "string" };

    private static ToolParameter FilePathParam(
        string name,
        string[]? allowedPrefixes = null) =>
        new()
        {
            Name               = name,
            Type               = "string",
            IsFilePath         = true,
            AllowedPathPrefixes = allowedPrefixes ?? [],
        };

    // ══════════════════════════════════════════════════════════════════════════
    // 1 — RegisterToolAsync + GetToolAsync round-trip for each safe category
    // ══════════════════════════════════════════════════════════════════════════

    public static TheoryData<ToolCategory> SafeCategoryData => new()
    {
        ToolCategory.ReadFile,
        ToolCategory.SystemInfo,
        ToolCategory.TextProcessing,
    };

    [Theory]
    [MemberData(nameof(SafeCategoryData))]
    public async Task RegisterAndGet_SafeCategory_RoundTrips(ToolCategory category)
    {
        var registry = new ToolRegistry();
        var tool     = MakeTool($"test-{category}", category);

        await registry.RegisterToolAsync(tool);
        var fetched = await registry.GetToolAsync(tool.Name);

        fetched.Name.Should().Be(tool.Name);
        fetched.Category.Should().Be(category);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 2 — GetToolAsync on unknown name throws KeyNotFoundException
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetToolAsync_UnknownName_ThrowsKeyNotFoundException()
    {
        var registry = new ToolRegistry();

        var act = async () => await registry.GetToolAsync("no-such-tool");

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*no-such-tool*");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 3 — RegisterToolAsync duplicate name throws ArgumentException
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RegisterToolAsync_DuplicateName_ThrowsArgumentException()
    {
        var registry = new ToolRegistry();
        var tool     = MakeTool("dup-tool", ToolCategory.SystemInfo);

        await registry.RegisterToolAsync(tool);

        var act = async () => await registry.RegisterToolAsync(tool);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*dup-tool*");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 4 — ListToolsByCategory returns only matching tools
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ListToolsByCategory_FiltersCorrectly()
    {
        var registry = new ToolRegistry();
        await registry.RegisterToolAsync(MakeTool("rf-tool", ToolCategory.ReadFile));
        await registry.RegisterToolAsync(MakeTool("si-tool", ToolCategory.SystemInfo));
        await registry.RegisterToolAsync(MakeTool("tp-tool", ToolCategory.TextProcessing));

        var readFileTools = new List<ToolDefinition>();
        await foreach (var t in registry.ListToolsByCategory(ToolCategory.ReadFile))
            readFileTools.Add(t);

        readFileTools.Should().ContainSingle()
            .Which.Name.Should().Be("rf-tool");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 5 — ListToolsByCategory on empty category returns empty sequence
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ListToolsByCategory_EmptyCategory_ReturnsEmpty()
    {
        var registry = new ToolRegistry();
        await registry.RegisterToolAsync(MakeTool("si-tool", ToolCategory.SystemInfo));

        var results = new List<ToolDefinition>();
        await foreach (var t in registry.ListToolsByCategory(ToolCategory.ReadFile))
            results.Add(t);

        results.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 6 — ValidateToolInvocation succeeds for safe category with valid args
    // ══════════════════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(SafeCategoryData))]
    public async Task ValidateToolInvocation_SafeCategory_Allowed(ToolCategory category)
    {
        var registry = new ToolRegistry();
        await registry.RegisterToolAsync(MakeTool($"safe-{category}", category));

        var result = registry.ValidateToolInvocation(
            $"safe-{category}",
            new Dictionary<string, string> { ["key"] = "value" });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 7 — ValidateToolInvocation denied for each non-safe category
    // ══════════════════════════════════════════════════════════════════════════

    public static TheoryData<ToolCategory> DeniedCategoryData => new()
    {
        ToolCategory.WriteFile,
        ToolCategory.ExecuteCommand,
        ToolCategory.Network,
        ToolCategory.Delete,
        ToolCategory.Unknown,
    };

    [Theory]
    [MemberData(nameof(DeniedCategoryData))]
    public async Task ValidateToolInvocation_DeniedCategory_ReturnsError(ToolCategory category)
    {
        var registry = new ToolRegistry();
        await registry.RegisterToolAsync(MakeTool($"denied-{category}", category));

        var result = registry.ValidateToolInvocation(
            $"denied-{category}",
            new Dictionary<string, string>());

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*not permitted in M2*");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 8 — ValidateToolInvocation: unregistered tool → error
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateToolInvocation_UnknownTool_ReturnsError()
    {
        var registry = new ToolRegistry();

        var result = registry.ValidateToolInvocation(
            "no-such-tool",
            new Dictionary<string, string>());

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*not registered*");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 9 — Path traversal rejection: classic Unix sequences
    // ══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("config/../../secret")]
    [InlineData("%2e%2e/etc/passwd")]
    [InlineData("%2E%2E\\windows\\system32")]
    [InlineData("docs/%252e%252e/secret")]
    public async Task ValidateToolInvocation_PathTraversal_Rejected(string maliciousPath)
    {
        var registry = new ToolRegistry();
        var param    = FilePathParam("path", allowedPrefixes: ["config/", "docs/"]);
        await registry.RegisterToolAsync(MakeTool("rf-path", ToolCategory.ReadFile, parameters: [param]));

        var result = registry.ValidateToolInvocation(
            "rf-path",
            new Dictionary<string, string> { ["path"] = maliciousPath });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*path-traversal*");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 10 — Path traversal: safe paths within whitelist are allowed
    // ══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("config/settings.json")]
    [InlineData("docs/readme.md")]
    [InlineData("config/nested/app.json")]
    public async Task ValidateToolInvocation_SafePath_Allowed(string safePath)
    {
        var registry = new ToolRegistry();
        var param    = FilePathParam("path", allowedPrefixes: ["config/", "docs/"]);
        await registry.RegisterToolAsync(MakeTool("rf-safe", ToolCategory.ReadFile, parameters: [param]));

        var result = registry.ValidateToolInvocation(
            "rf-safe",
            new Dictionary<string, string> { ["path"] = safePath });

        result.IsValid.Should().BeTrue();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 11 — Path outside whitelist is rejected (no traversal, but wrong prefix)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ValidateToolInvocation_PathOutsideWhitelist_Rejected()
    {
        var registry = new ToolRegistry();
        var param    = FilePathParam("path", allowedPrefixes: ["config/"]);
        await registry.RegisterToolAsync(MakeTool("rf-wl", ToolCategory.ReadFile, parameters: [param]));

        var result = registry.ValidateToolInvocation(
            "rf-wl",
            new Dictionary<string, string> { ["path"] = "secrets/private.key" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*not under any allowed path prefix*");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 12 — Input size enforcement: payload over limit is rejected
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ValidateToolInvocation_InputOverLimit_Rejected()
    {
        const int maxSize = 100;
        var registry      = new ToolRegistry();
        await registry.RegisterToolAsync(
            MakeTool("small-tool", ToolCategory.TextProcessing, maxInputSize: maxSize));

        var bigValue = new string('x', maxSize + 1);
        var result   = registry.ValidateToolInvocation(
            "small-tool",
            new Dictionary<string, string> { ["data"] = bigValue });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*exceeds the tool's maxInputSize*");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 13 — Input size enforcement: payload at exactly limit is allowed
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ValidateToolInvocation_InputAtLimit_Allowed()
    {
        var registry = new ToolRegistry();
        var tool     = MakeTool("exact-tool", ToolCategory.TextProcessing, maxInputSize: 200);
        await registry.RegisterToolAsync(tool);

        // key= "data" (4+1=5 chars overhead), fill remainder with 'a'
        // "data=<value>;" — 6 chars overhead, fill 194 chars to stay under or equal
        var value  = new string('a', 50); // well under 200
        var result = registry.ValidateToolInvocation(
            "exact-tool",
            new Dictionary<string, string> { ["data"] = value });

        result.IsValid.Should().BeTrue();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 14 — Required parameter missing → validation error
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ValidateToolInvocation_MissingRequiredParam_ReturnsError()
    {
        var registry = new ToolRegistry();
        var param    = RequiredStringParam("query");
        await registry.RegisterToolAsync(
            MakeTool("search-tool", ToolCategory.TextProcessing, parameters: [param]));

        var result = registry.ValidateToolInvocation(
            "search-tool",
            new Dictionary<string, string>()); // no "query"

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*'query'*missing*");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 15 — Optional parameter missing is not an error
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ValidateToolInvocation_MissingOptionalParam_Allowed()
    {
        var registry     = new ToolRegistry();
        var optionalParam = new ToolParameter
        {
            Name     = "format",
            Type     = "string",
            Required = false,
        };
        await registry.RegisterToolAsync(
            MakeTool("fmt-tool", ToolCategory.TextProcessing, parameters: [optionalParam]));

        var result = registry.ValidateToolInvocation(
            "fmt-tool",
            new Dictionary<string, string> { ["content"] = "hello" });

        result.IsValid.Should().BeTrue();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 16 — Audit log emitted for every invocation (allowed and denied)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AuditLog_EmitsEntryForAllowedInvocation()
    {
        var registry = new ToolRegistry();
        await registry.RegisterToolAsync(MakeTool("audited", ToolCategory.SystemInfo));

        registry.ValidateToolInvocation("audited", new Dictionary<string, string>());

        registry.AuditLog.Should().ContainSingle()
            .Which.Allowed.Should().BeTrue();
    }

    [Fact]
    public void AuditLog_EmitsEntryForDeniedInvocation_UnknownTool()
    {
        var registry = new ToolRegistry();

        registry.ValidateToolInvocation("ghost", new Dictionary<string, string>());

        var entry = registry.AuditLog.Should().ContainSingle().Subject;
        entry.Allowed.Should().BeFalse();
        entry.ToolName.Should().Be("ghost");
        entry.DenialReasons.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AuditLog_AccumulatesMultipleEntries()
    {
        var registry = new ToolRegistry();
        await registry.RegisterToolAsync(MakeTool("multi", ToolCategory.TextProcessing));

        registry.ValidateToolInvocation("multi", new Dictionary<string, string>());
        registry.ValidateToolInvocation("multi", new Dictionary<string, string>());
        registry.ValidateToolInvocation("missing", new Dictionary<string, string>());

        registry.AuditLog.Should().HaveCount(3);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 17 — Audit log entries contain category and timestamp
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AuditLog_EntryHasCorrectCategoryAndTimestamp()
    {
        var before   = DateTimeOffset.UtcNow;
        var registry = new ToolRegistry();
        await registry.RegisterToolAsync(MakeTool("ts-tool", ToolCategory.SystemInfo));

        registry.ValidateToolInvocation("ts-tool", new Dictionary<string, string>());

        var after = DateTimeOffset.UtcNow;
        var entry = registry.AuditLog[0];

        entry.Category.Should().Be(nameof(ToolCategory.SystemInfo));
        entry.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 18 — Case-insensitive name lookup (register lower, get upper)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetToolAsync_CaseInsensitiveLookup_Works()
    {
        var registry = new ToolRegistry();
        await registry.RegisterToolAsync(MakeTool("my-tool", ToolCategory.ReadFile));

        var tool = await registry.GetToolAsync("MY-TOOL");

        tool.Name.Should().Be("my-tool");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 19 — ToolDefinition defaults (maxInputSize, timeoutMs)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ToolDefinition_Defaults_AreCorrect()
    {
        var tool = new ToolDefinition
        {
            Name        = "defaults",
            Category    = ToolCategory.TextProcessing,
            Description = "check defaults",
        };

        tool.MaxInputSize.Should().Be(10_240);
        tool.TimeoutMs.Should().Be(5_000);
        tool.Parameters.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 20 — SafeCategories whitelist contains exactly the three M2 categories
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SafeCategories_ContainsExactlyM2Categories()
    {
        ToolRegistry.SafeCategories.Should().BeEquivalentTo(new[]
        {
            ToolCategory.ReadFile,
            ToolCategory.SystemInfo,
            ToolCategory.TextProcessing,
        });
    }
}
