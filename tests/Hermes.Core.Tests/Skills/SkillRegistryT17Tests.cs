using System.Text;
using Hermes.Core.Skills;

namespace Hermes.Core.Tests.Skills;

/// <summary>
/// T17 — SkillRegistry new API + SkillRegistryBootstrapper tests.
///
/// Covers:
///   - RegisterSkillAsync: direct skill registration + DuplicateSkillException
///   - GetSkillMetadataAsync: metadata retrieval + KeyNotFoundException
///   - SkillRegistryBootstrapper: directory scan, missing dir no-op, cancellation
///   - MarkdownSkillParser: memory + policy types accepted
/// </summary>
public sealed class SkillRegistryT17Tests : IDisposable
{
    private readonly string _testRoot;

    public SkillRegistryT17Tests()
    {
        _testRoot = Path.Combine(
            AppContext.BaseDirectory, "t17-test-dirs", Guid.NewGuid().ToString("N"));
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
    // RegisterSkillAsync — happy path
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RegisterSkillAsync_ValidDescriptor_IsReturnedByGetSkill()
    {
        var registry = new SkillRegistry();
        var descriptor = new SkillDescriptor
        {
            Name        = "inline-skill",
            Type        = "tool",
            Description = "Registered directly via RegisterSkillAsync"
        };

        await registry.RegisterSkillAsync(descriptor);

        var found = await registry.GetSkillAsync("inline-skill");
        found.Should().NotBeNull();
        found.Name.Should().Be("inline-skill");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // RegisterSkillAsync — duplicate throws DuplicateSkillException
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RegisterSkillAsync_Duplicate_ThrowsDuplicateSkillException()
    {
        var registry = new SkillRegistry();
        var descriptor = new SkillDescriptor
        {
            Name        = "dup-skill",
            Type        = "tool",
            Description = "First registration"
        };

        await registry.RegisterSkillAsync(descriptor);

        var dup = new SkillDescriptor
        {
            Name        = "dup-skill",
            Type        = "action",
            Description = "Second registration — should throw"
        };

        var act = async () => await registry.RegisterSkillAsync(dup);

        await act.Should().ThrowAsync<DuplicateSkillException>()
                 .WithMessage("*dup-skill*");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // RegisterSkillAsync — case-insensitive ID collision
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RegisterSkillAsync_DuplicateCaseInsensitive_ThrowsDuplicateSkillException()
    {
        var registry = new SkillRegistry();
        await registry.RegisterSkillAsync(new SkillDescriptor
        {
            Name = "MY-SKILL", Type = "tool", Description = "uppercase"
        });

        var act = async () => await registry.RegisterSkillAsync(new SkillDescriptor
        {
            Name = "my-skill", Type = "action", Description = "lowercase collision"
        });

        await act.Should().ThrowAsync<DuplicateSkillException>();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetSkillMetadataAsync — skill with metadata returns metadata dict
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetSkillMetadataAsync_SkillWithMetadata_ReturnsMetadataDict()
    {
        var dir = CreateDir("meta01");
        await WriteFile(dir, "meta-skill.md", """
            # Skill ID: meta-skill
            **Version:** 1.0
            **Description:** Skill with rich metadata
            **Type:** tool
            **Category:** test

            ## Metadata
            - Author: dallas
            - Scope: global
            - Callable: true
            """);

        var registry = new SkillRegistry();
        await registry.LoadFromDirectoryAsync(dir);

        var metadata = await registry.GetSkillMetadataAsync("meta-skill");

        metadata.Should().ContainKey("Author");
        metadata["Author"].Should().Be("dallas");
        metadata.Should().ContainKey("Scope");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetSkillMetadataAsync — skill with no metadata returns empty dict
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetSkillMetadataAsync_SkillWithNoMetadata_ReturnsEmptyDict()
    {
        var dir = CreateDir("meta02");
        await WriteFile(dir, "bare-skill.md", """
            # Skill ID: bare-skill
            **Version:** 1.0
            **Description:** No metadata section
            **Type:** action
            """);

        var registry = new SkillRegistry();
        await registry.LoadFromDirectoryAsync(dir);

        var metadata = await registry.GetSkillMetadataAsync("bare-skill");

        metadata.Should().BeEmpty();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetSkillMetadataAsync — unknown skill throws KeyNotFoundException
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetSkillMetadataAsync_UnknownSkill_ThrowsKeyNotFoundException()
    {
        var registry = new SkillRegistry();

        var act = async () => await registry.GetSkillMetadataAsync("ghost");

        await act.Should().ThrowAsync<KeyNotFoundException>()
                 .WithMessage("*ghost*");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SkillRegistryBootstrapper — happy path: loads skills from directory
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Bootstrapper_SkillsDirectoryExists_LoadsAllSkills()
    {
        var dir = CreateDir("boot01");
        await WriteFile(dir, "alpha.md", """
            # Skill ID: alpha
            **Version:** 1.0
            **Description:** Alpha skill
            **Type:** action
            """);
        await WriteFile(dir, "beta.md", """
            # Skill ID: beta
            **Version:** 1.0
            **Description:** Beta skill
            **Type:** tool
            """);

        var registry     = new SkillRegistry();
        var bootstrapper = new SkillRegistryBootstrapper(registry, dir);
        await bootstrapper.BootstrapAsync();

        var all = await registry.ListSkillsAsync();
        all.Should().HaveCount(2);
        all.Select(s => s.Id).Should().Contain(["alpha", "beta"]);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SkillRegistryBootstrapper — missing directory is a silent no-op
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Bootstrapper_MissingDirectory_CompletesWithoutError()
    {
        var registry     = new SkillRegistry();
        var bootstrapper = new SkillRegistryBootstrapper(
            registry, Path.Combine(_testRoot, "nonexistent-dir"));

        var act = async () => await bootstrapper.BootstrapAsync();

        await act.Should().NotThrowAsync();
        var all = await registry.ListSkillsAsync();
        all.Should().BeEmpty();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SkillRegistryBootstrapper — idempotent double-bootstrap
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Bootstrapper_CalledTwice_IsIdempotent()
    {
        var dir = CreateDir("boot02");
        await WriteFile(dir, "skill.md", """
            # Skill ID: idempotent-skill
            **Version:** 1.0
            **Description:** Idempotency test
            **Type:** action
            """);

        var registry     = new SkillRegistry();
        var bootstrapper = new SkillRegistryBootstrapper(registry, dir);

        await bootstrapper.BootstrapAsync();
        await bootstrapper.BootstrapAsync(); // must not throw or duplicate

        var all = await registry.ListSkillsAsync();
        all.Should().HaveCount(1);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SkillRegistryBootstrapper — SkillsDirectory property reflects resolved path
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Bootstrapper_SkillsDirectory_ReflectsProvidedPath()
    {
        var dir          = CreateDir("boot03");
        var registry     = new SkillRegistry();
        var bootstrapper = new SkillRegistryBootstrapper(registry, dir);

        bootstrapper.SkillsDirectory.Should().Be(dir);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // MarkdownSkillParser + Registry — memory type accepted
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Registry_MemoryTypeSkill_LoadsSuccessfully()
    {
        var dir = CreateDir("type01");
        await WriteFile(dir, "mem-skill.md", """
            # Skill ID: recall-memory
            **Version:** 1.0
            **Description:** Recalls the active session memory context
            **Type:** memory
            """);

        var registry = new SkillRegistry();
        await registry.LoadFromDirectoryAsync(dir);

        var skill = await registry.GetSkillAsync("recall-memory");
        skill.Type.Should().Be("memory");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // MarkdownSkillParser + Registry — policy type accepted
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Registry_PolicyTypeSkill_LoadsSuccessfully()
    {
        var dir = CreateDir("type02");
        await WriteFile(dir, "rate-limit.md", """
            # Skill ID: rate-limit-policy
            **Version:** 1.0
            **Description:** Enforces per-profile rate limit on requests
            **Type:** policy

            ## Metadata
            - MaxRequestsPerMinute: 60
            """);

        var registry = new SkillRegistry();
        await registry.LoadFromDirectoryAsync(dir);

        var skill = await registry.GetSkillAsync("rate-limit-policy");
        skill.Type.Should().Be("policy");

        var metadata = await registry.GetSkillMetadataAsync("rate-limit-policy");
        metadata.Should().ContainKey("MaxRequestsPerMinute");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ListSkillsAsync includes directly registered skills
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ListSkillsAsync_IncludesDirectlyRegisteredSkills()
    {
        var registry = new SkillRegistry();

        await registry.RegisterSkillAsync(new SkillDescriptor
        {
            Name = "skill-a", Type = "tool", Description = "A"
        });
        await registry.RegisterSkillAsync(new SkillDescriptor
        {
            Name = "skill-b", Type = "action", Description = "B"
        });

        var all = await registry.ListSkillsAsync();
        all.Should().HaveCount(2);
        all.Select(s => s.Name).Should().BeEquivalentTo(["skill-a", "skill-b"]);
    }
}
