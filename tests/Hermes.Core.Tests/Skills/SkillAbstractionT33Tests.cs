using System.Text;
using Hermes.Core.Skills;
using Moq;

namespace Hermes.Core.Tests.Skills;

/// <summary>
/// T33 — ISkillDefinition / ISkillProvider contract tests.
///
/// Covers:
///   - ISkillDefinition: SkillDescriptor implements the interface correctly
///   - ISkillProvider:   SkillRegistry implements DiscoverAsync, LoadAsync, ValidateAsync
///   - ISkillProvider:   SkillRegistryBootstrapper works with a mock ISkillProvider
/// </summary>
public sealed class SkillAbstractionT33Tests : IDisposable
{
    private readonly string _testRoot;

    public SkillAbstractionT33Tests()
    {
        _testRoot = Path.Combine(
            AppContext.BaseDirectory, "t33-test-dirs", Guid.NewGuid().ToString("N"));
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

    private const string MinimalSkillMd = """
        # Skill ID: minimal-skill
        **Version:** 1.0
        **Description:** Minimal skill for contract tests
        **Type:** tool
        """;

    // ═════════════════════════════════════════════════════════════════════════
    // Contract 1 — SkillDescriptor implements ISkillDefinition
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SkillDescriptor_ImplementsISkillDefinition()
    {
        var descriptor = new SkillDescriptor
        {
            Name        = "test-skill",
            Type        = "tool",
            Description = "A test skill"
        };

        descriptor.Should().BeAssignableTo<ISkillDefinition>();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Contract 2 — ISkillDefinition core properties match SkillDescriptor
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ISkillDefinition_CoreProperties_MatchDescriptorValues()
    {
        ISkillDefinition def = new SkillDescriptor
        {
            Name        = "prop-check",
            Type        = "action",
            Description = "Verifies property mapping",
            Id          = "prop-check-id",
            Category    = "testing"
        };

        def.Name.Should().Be("prop-check");
        def.Type.Should().Be("action");
        def.Description.Should().Be("Verifies property mapping");
        def.Id.Should().Be("prop-check-id");
        def.Category.Should().Be("testing");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Contract 3 — Inputs/Outputs default to null when not provided
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ISkillDefinition_InputsOutputs_DefaultToNull()
    {
        ISkillDefinition def = new SkillDescriptor
        {
            Name        = "no-io-skill",
            Type        = "tool",
            Description = "Skill without explicit I/O"
        };

        def.Inputs.Should().BeNull();
        def.Outputs.Should().BeNull();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Contract 4 — Inputs/Outputs are surfaced when explicitly set
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ISkillDefinition_WithInputsOutputs_ReturnsProvidedDictionaries()
    {
        var inputs  = new Dictionary<string, string> { ["a"] = "number" };
        var outputs = new Dictionary<string, string> { ["result"] = "number" };

        ISkillDefinition def = new SkillDescriptor
        {
            Name        = "io-skill",
            Type        = "action",
            Description = "Skill with explicit I/O",
            Inputs      = inputs,
            Outputs     = outputs
        };

        def.Inputs.Should().ContainKey("a").WhoseValue.Should().Be("number");
        def.Outputs.Should().ContainKey("result").WhoseValue.Should().Be("number");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Contract 5 — SkillRegistry implements ISkillProvider
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SkillRegistry_ImplementsISkillProvider()
    {
        var registry = new SkillRegistry();
        registry.Should().BeAssignableTo<ISkillProvider>();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Contract 6 — ISkillProvider.DiscoverAsync returns .md files
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ISkillProvider_DiscoverAsync_ReturnsMdFilePaths()
    {
        var dir = CreateDir("discover01");
        await WriteFile(dir, "skill-a.md", MinimalSkillMd);
        await WriteFile(dir, "skill-b.md", MinimalSkillMd.Replace("minimal-skill", "skill-b").Replace("Minimal skill", "Skill B"));
        await WriteFile(dir, "readme.txt", "not a skill");

        ISkillProvider provider = new SkillRegistry();
        var paths = await provider.DiscoverAsync(dir);

        paths.Should().HaveCount(2);
        paths.Should().AllSatisfy(p => p.Should().EndWith(".md"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Contract 7 — ISkillProvider.DiscoverAsync empty directory returns empty list
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ISkillProvider_DiscoverAsync_EmptyDirectory_ReturnsEmpty()
    {
        var dir = CreateDir("discover02");

        ISkillProvider provider = new SkillRegistry();
        var paths = await provider.DiscoverAsync(dir);

        paths.Should().BeEmpty();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Contract 8 — ISkillProvider.DiscoverAsync non-existent dir throws
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ISkillProvider_DiscoverAsync_NonExistentDirectory_ThrowsDirectoryNotFoundException()
    {
        ISkillProvider provider = new SkillRegistry();

        var act = async () => await provider.DiscoverAsync(
            Path.Combine(_testRoot, "does-not-exist"));

        await act.Should().ThrowAsync<DirectoryNotFoundException>();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Contract 9 — ISkillProvider.LoadAsync returns ISkillDefinition
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ISkillProvider_LoadAsync_ValidFile_ReturnsISkillDefinition()
    {
        var dir = CreateDir("load01");
        var path = Path.Combine(dir, "skill.md");
        await WriteFile(dir, "skill.md", MinimalSkillMd);

        ISkillProvider provider = new SkillRegistry();
        var definition = await provider.LoadAsync(path);

        definition.Should().NotBeNull();
        definition.Id.Should().Be("minimal-skill");
        definition.Type.Should().Be("tool");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Contract 10 — ISkillProvider.LoadAsync invalid file throws SkillParseException
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ISkillProvider_LoadAsync_InvalidFile_ThrowsSkillParseException()
    {
        var dir = CreateDir("load02");
        await WriteFile(dir, "bad.md", "This is not a valid skill file at all.");

        ISkillProvider provider = new SkillRegistry();

        var act = async () => await provider.LoadAsync(Path.Combine(dir, "bad.md"));

        await act.Should().ThrowAsync<SkillParseException>();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Contract 11 — ISkillProvider.ValidateAsync valid definition returns Success
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ISkillProvider_ValidateAsync_ValidDefinition_ReturnsSuccess()
    {
        ISkillDefinition def = new SkillDescriptor
        {
            Name          = "valid-def",
            Type          = "tool",
            Description   = "All fields valid",
            SchemaVersion = new Version(1, 0)
        };

        ISkillProvider provider = new SkillRegistry();
        var result = await provider.ValidateAsync(def);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Contract 12 — ISkillProvider.ValidateAsync wrong schema version yields error
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ISkillProvider_ValidateAsync_WrongSchemaMajorVersion_ReturnsErrors()
    {
        ISkillDefinition def = new SkillDescriptor
        {
            Name          = "future-def",
            Type          = "tool",
            Description   = "Schema v2 — not yet supported",
            SchemaVersion = new Version(2, 0)
        };

        ISkillProvider provider = new SkillRegistry();
        var result = await provider.ValidateAsync(def);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("2.0"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Contract 13 — ISkillProvider.ValidateAsync missing schema version yields error
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ISkillProvider_ValidateAsync_NullSchemaVersion_ReturnsErrors()
    {
        ISkillDefinition def = new SkillDescriptor
        {
            Name          = "no-version-def",
            Type          = "action",
            Description   = "Missing schema version"
            // SchemaVersion intentionally omitted (null)
        };

        ISkillProvider provider = new SkillRegistry();
        var result = await provider.ValidateAsync(def);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("schema version"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Contract 14 — SkillRegistryBootstrapper works with mock ISkillProvider
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Bootstrapper_WithMockProvider_CallsDiscoverAndLoad()
    {
        var dir = CreateDir("boot-mock01");
        var fakePath = Path.Combine(dir, "mocked-skill.md");

        // Write a placeholder file so the directory exists (bootstrapper checks Directory.Exists).
        await File.WriteAllTextAsync(fakePath, "placeholder");

        ISkillDefinition fakeDefinition = new SkillDescriptor
        {
            Id            = "mocked-skill",
            Name          = "mocked-skill",
            Type          = "tool",
            Description   = "Injected by mock provider",
            SchemaVersion = new Version(1, 0)
        };

        var mockProvider = new Mock<ISkillProvider>();
        mockProvider
            .Setup(p => p.DiscoverAsync(dir, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { fakePath });
        mockProvider
            .Setup(p => p.LoadAsync(fakePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeDefinition);

        var registry     = new SkillRegistry();
        var bootstrapper = new SkillRegistryBootstrapper(registry, mockProvider.Object, dir);
        await bootstrapper.BootstrapAsync();

        var skills = await registry.ListSkillsAsync();
        skills.Should().HaveCount(1);
        skills[0].Id.Should().Be("mocked-skill");

        mockProvider.Verify(p => p.DiscoverAsync(dir, It.IsAny<CancellationToken>()), Times.Once);
        mockProvider.Verify(p => p.LoadAsync(fakePath, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Contract 15 — Metadata is accessible via ISkillDefinition interface
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ISkillDefinition_Metadata_NullWhenNotProvided()
    {
        ISkillDefinition def = new SkillDescriptor
        {
            Name        = "bare-skill",
            Type        = "action",
            Description = "No metadata"
        };

        def.Metadata.Should().BeNull();
    }
}
