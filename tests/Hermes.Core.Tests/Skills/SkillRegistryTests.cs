using System.Collections.Concurrent;
using System.Text;
using Hermes.Core.Skills;

namespace Hermes.Core.Tests.Skills;

/// <summary>
/// T14 — Skill Registry &amp; Markdown Parser: 18 unit tests (all concrete, none skipped).
///
/// Tests use an isolated scratch directory under AppContext.BaseDirectory so that
/// no test shares state with another, and no /tmp dependency is introduced.
/// </summary>
public sealed class SkillRegistryTests : IDisposable
{
    // ── Shared fixture markdown content ───────────────────────────────────────

    private const string ValidSkillMd = """
        # Skill ID: calculate-sum
        **Version:** 1.0
        **Description:** Adds two numbers together
        **Type:** action
        **Category:** math

        ## Metadata
        - Input: { a: number, b: number }
        - Output: { result: number }

        ## Implementation Notes
        This skill demonstrates basic arithmetic operations.
        """;

    private const string SecondSkillMd = """
        # Skill ID: echo-text
        **Version:** 1.0
        **Description:** Echoes input text back to the caller
        **Type:** tool

        ## Implementation Notes
        Simple echo tool.
        """;

    // ── Temp directory infrastructure ─────────────────────────────────────────

    private readonly string _testRoot;

    public SkillRegistryTests()
    {
        _testRoot = Path.Combine(
            AppContext.BaseDirectory, "t14-test-dirs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    private string CreateDir(string name)
    {
        var dir = Path.Combine(_testRoot, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task WriteFile(string dir, string fileName, string content)
        => await File.WriteAllTextAsync(Path.Combine(dir, fileName), content, Encoding.UTF8);

    // ═════════════════════════════════════════════════════════════════════════
    // Test 1 — Valid skill load + GetSkillAsync returns descriptor
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetSkillAsync_ValidSkill_ReturnsDescriptor()
    {
        var dir = CreateDir("t01");
        await WriteFile(dir, "calculate-sum.md", ValidSkillMd);

        var registry = new SkillRegistry();
        await registry.LoadFromDirectoryAsync(dir);

        var skill = await registry.GetSkillAsync("calculate-sum");

        skill.Should().NotBeNull();
        skill.Id.Should().Be("calculate-sum");
        skill.Description.Should().Be("Adds two numbers together");
        skill.Type.Should().Be("action");
        skill.SchemaVersion.Should().Be(new Version(1, 0));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test 2 — Multiple skills: ListSkillsAsync returns all
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ListSkillsAsync_MultipleSkills_ReturnsAll()
    {
        var dir = CreateDir("t02");
        await WriteFile(dir, "calculate-sum.md", ValidSkillMd);
        await WriteFile(dir, "echo-text.md", SecondSkillMd);

        var registry = new SkillRegistry();
        await registry.LoadFromDirectoryAsync(dir);

        var all = await registry.ListSkillsAsync();

        all.Should().HaveCount(2);
        all.Select(s => s.Id).Should().Contain(["calculate-sum", "echo-text"]);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test 3 — Skill not found throws KeyNotFoundException with ID in message
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetSkillAsync_UnknownId_ThrowsKeyNotFoundException()
    {
        var registry = new SkillRegistry();

        var act = async () => await registry.GetSkillAsync("nonexistent-skill");

        await act.Should().ThrowAsync<KeyNotFoundException>()
                 .WithMessage("*nonexistent-skill*");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test 4 — Malformed markdown: missing ID → SkillParseException
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoadFromDirectoryAsync_MissingId_ThrowsSkillParseException()
    {
        var dir = CreateDir("t04");
        await WriteFile(dir, "bad.md", """
            **Version:** 1.0
            **Description:** No ID header
            **Type:** action
            """);

        var registry = new SkillRegistry();
        var act = async () => await registry.LoadFromDirectoryAsync(dir);

        await act.Should().ThrowAsync<SkillParseException>()
                 .WithMessage("*id*");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test 5 — Malformed markdown: missing Version → SkillParseException
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoadFromDirectoryAsync_MissingVersion_ThrowsSkillParseException()
    {
        var dir = CreateDir("t05");
        await WriteFile(dir, "bad.md", """
            # Skill ID: no-version
            **Description:** Version field is absent
            **Type:** action
            """);

        var registry = new SkillRegistry();
        var act = async () => await registry.LoadFromDirectoryAsync(dir);

        await act.Should().ThrowAsync<SkillParseException>()
                 .WithMessage("*version*");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test 6 — Duplicate skill ID in directory throws DuplicateSkillException
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoadFromDirectoryAsync_DuplicateSkillId_ThrowsDuplicateSkillException()
    {
        var dir = CreateDir("t06");
        await WriteFile(dir, "a.md", ValidSkillMd);
        await WriteFile(dir, "b.md", """
            # Skill ID: calculate-sum
            **Version:** 1.0
            **Description:** Duplicate of calculate-sum
            **Type:** action
            """);

        var registry = new SkillRegistry();
        var act = async () => await registry.LoadFromDirectoryAsync(dir);

        await act.Should().ThrowAsync<DuplicateSkillException>()
                 .WithMessage("*calculate-sum*");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test 7 — Schema version mismatch: raises LoadWarning, still loads skill
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoadFromDirectoryAsync_VersionMismatch_LoadsWithWarning()
    {
        var dir = CreateDir("t07");
        await WriteFile(dir, "future.md", """
            # Skill ID: future-skill
            **Version:** 99.0
            **Description:** Far-future schema version
            **Type:** action
            """);

        var registry = new SkillRegistry();
        await registry.LoadFromDirectoryAsync(dir); // must NOT throw

        // Skill is loaded despite version mismatch.
        var skill = await registry.GetSkillAsync("future-skill");
        skill.Should().NotBeNull();

        // A warning must have been recorded.
        registry.LoadWarnings.Should().ContainSingle(w => w.Contains("future-skill") && w.Contains("99"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test 8 — FindByNameAsync happy path: finds by ID
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FindByNameAsync_MatchesById_ReturnsDescriptor()
    {
        var dir = CreateDir("t08");
        await WriteFile(dir, "calculate-sum.md", ValidSkillMd);

        var registry = new SkillRegistry();
        await registry.LoadFromDirectoryAsync(dir);

        var found = await registry.FindByNameAsync("calculate-sum");

        found.Should().NotBeNull();
        found!.Id.Should().Be("calculate-sum");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test 9 — FindByNameAsync not found: returns null, doesn't throw
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FindByNameAsync_NoMatch_ReturnsNull()
    {
        var registry = new SkillRegistry();

        var found = await registry.FindByNameAsync("does-not-exist");

        found.Should().BeNull();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test 10 — ValidateAsync passing skill returns Success
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ValidateAsync_ValidSkill_ReturnsSuccess()
    {
        var dir = CreateDir("t10");
        await WriteFile(dir, "calculate-sum.md", ValidSkillMd);

        var registry = new SkillRegistry();
        await registry.LoadFromDirectoryAsync(dir);

        var result = await registry.ValidateAsync("calculate-sum");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test 11 — ValidateAsync failing skill returns result with errors list
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ValidateAsync_UnsupportedMajorVersion_ReturnsErrors()
    {
        var dir = CreateDir("t11");
        await WriteFile(dir, "future.md", """
            # Skill ID: future-skill
            **Version:** 99.0
            **Description:** Unsupported major version
            **Type:** action
            """);

        var registry = new SkillRegistry();
        await registry.LoadFromDirectoryAsync(dir);

        var result = await registry.ValidateAsync("future-skill");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("99"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test 12 — LoadFromDirectoryAsync idempotency: second load is no-op
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoadFromDirectoryAsync_SecondLoad_IsNoOp()
    {
        var dir = CreateDir("t12");
        await WriteFile(dir, "calculate-sum.md", ValidSkillMd);

        var registry = new SkillRegistry();
        await registry.LoadFromDirectoryAsync(dir); // first load
        await registry.LoadFromDirectoryAsync(dir); // second load — must not throw or duplicate

        var all = await registry.ListSkillsAsync();
        all.Should().HaveCount(1); // still exactly one entry
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test 13 — Skill file missing required metadata field → SkillParseException
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoadFromDirectoryAsync_MissingDescription_ThrowsSkillParseException()
    {
        var dir = CreateDir("t13");
        await WriteFile(dir, "bad.md", """
            # Skill ID: nodesc-skill
            **Version:** 1.0
            **Type:** action
            """);

        var registry = new SkillRegistry();
        var act = async () => await registry.LoadFromDirectoryAsync(dir);

        await act.Should().ThrowAsync<SkillParseException>()
                 .WithMessage("*description*");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test 14 — Skill file with unknown extension is ignored
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoadFromDirectoryAsync_NonMdFiles_AreIgnored()
    {
        var dir = CreateDir("t14");
        await WriteFile(dir, "calculate-sum.md", ValidSkillMd);
        await WriteFile(dir, "readme.txt", "Not a skill file.");
        await WriteFile(dir, "config.yaml", "not: parsed");

        var registry = new SkillRegistry();
        await registry.LoadFromDirectoryAsync(dir); // must not throw on .txt/.yaml

        var all = await registry.ListSkillsAsync();
        all.Should().HaveCount(1); // only the .md was loaded
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test 15 — Very large skill content (5 MB) loads without OOM
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoadFromDirectoryAsync_LargeSkillFile_LoadsWithoutOOM()
    {
        var dir = CreateDir("t15");

        var header = """
            # Skill ID: large-skill
            **Version:** 1.0
            **Description:** A very large skill for stress testing
            **Type:** skill

            ## Implementation Notes
            """;

        var sb = new StringBuilder();
        sb.Append(header);
        sb.AppendLine();
        var chunk = new string('x', 1024); // 1 KB per line
        for (int i = 0; i < 5 * 1024; i++) // ~5 MB total
            sb.AppendLine(chunk);

        await File.WriteAllTextAsync(
            Path.Combine(dir, "large-skill.md"),
            sb.ToString(),
            Encoding.UTF8);

        var registry = new SkillRegistry();
        await registry.LoadFromDirectoryAsync(dir); // must not OOM or throw

        var skill = await registry.GetSkillAsync("large-skill");
        skill.Content.Should().NotBeNullOrWhiteSpace();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test 16 — Concurrent GetSkillAsync calls are thread-safe
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetSkillAsync_ConcurrentCalls_ThreadSafe()
    {
        var dir = CreateDir("t16");
        await WriteFile(dir, "calculate-sum.md", ValidSkillMd);

        var registry = new SkillRegistry();
        await registry.LoadFromDirectoryAsync(dir);

        var results = new ConcurrentBag<SkillDescriptor>();
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 100),
            new ParallelOptions { MaxDegreeOfParallelism = 20 },
            async (_, _) =>
            {
                var s = await registry.GetSkillAsync("calculate-sum");
                results.Add(s);
            });

        results.Should().HaveCount(100);
        results.All(s => s.Id == "calculate-sum").Should().BeTrue();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test 17 — Skill type validation: invalid type throws SkillParseException
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoadFromDirectoryAsync_InvalidType_ThrowsSkillParseException()
    {
        var dir = CreateDir("t17");
        await WriteFile(dir, "bad.md", """
            # Skill ID: bad-type-skill
            **Version:** 1.0
            **Description:** Has an invalid type
            **Type:** wizard
            """);

        var registry = new SkillRegistry();
        var act = async () => await registry.LoadFromDirectoryAsync(dir);

        await act.Should().ThrowAsync<SkillParseException>()
                 .WithMessage("*Invalid type*wizard*");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test 18 — UTF-8 with BOM is handled correctly
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoadFromDirectoryAsync_Utf8WithBom_ParsedCorrectly()
    {
        var dir = CreateDir("t18");
        var filePath = Path.Combine(dir, "bom-skill.md");

        // UTF8Encoding with BOM emits the \uFEFF byte-order mark.
        await File.WriteAllTextAsync(
            filePath, ValidSkillMd,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var registry = new SkillRegistry();
        await registry.LoadFromDirectoryAsync(dir);

        var skill = await registry.GetSkillAsync("calculate-sum");
        skill.Should().NotBeNull();
        skill.Id.Should().Be("calculate-sum");
    }
}