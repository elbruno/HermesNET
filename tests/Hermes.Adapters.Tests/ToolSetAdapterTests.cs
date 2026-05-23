namespace Hermes.Adapters.Tests;

/// <summary>
/// T34 — MAF Adapter Test Suite: SkillRegistry → MAF IToolSet mapping (10 test cases).
///
/// Test reference:
///   TC-01  Single skill projects to a MafToolDescriptor with correct ToolName
///   TC-02  ToMafToolName normalises uppercase to lowercase
///   TC-03  ToMafToolName replaces spaces with dashes
///   TC-04  ToMafToolName replaces special characters with dashes
///   TC-05  Empty skill ID throws ArgumentException
///   TC-06  Skill with metadata forwards key-value pairs verbatim
///   TC-07  Skill with null metadata produces empty dictionary (not null)
///   TC-08  ProjectToolSetAsync returns one descriptor per registered skill
///   TC-09  ProjectToolSetAsync on empty registry returns empty list
///   TC-10  ValidateToolSetMappingAsync returns skill ID when description is empty
/// </summary>
public sealed class ToolSetAdapterTests
{
    private readonly ToolSetAdapterStub _adapter = new();

    // ── TC-01 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-01: A single skill with a known ID produces a MafToolDescriptor whose
    /// ToolName equals the normalised form of that ID, and whose SourceSkillId
    /// round-trips back to the original.
    /// Input:  SkillDescriptor { Id = "calculate-sum", Name = "Calculate Sum", Description = "Adds two numbers", Type = "action" }
    /// Expected: MafToolDescriptor { ToolName = "calculate-sum", SourceSkillId = "calculate-sum" }
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public void ToMafToolDescriptor_SingleSkill_ProducesCorrectToolName()
    {
        // Arrange
        var skill = new SkillDescriptor
        {
            Id = "calculate-sum",
            Name = "Calculate Sum",
            Description = "Adds two numbers",
            Type = "action"
        };

        // Act
        var descriptor = _adapter.ToMafToolDescriptor(skill);

        // Assert
        descriptor.ToolName.Should().Be("calculate-sum");
        descriptor.SourceSkillId.Should().Be("calculate-sum");
        descriptor.Description.Should().Be("Adds two numbers");
    }

    // ── TC-02 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-02: ToMafToolName normalises uppercase characters to lowercase.
    /// Input:  "MySkill"
    /// Expected: "myskill"
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public void ToMafToolName_UppercaseInput_ReturnsLowercase()
    {
        // Act
        var result = _adapter.ToMafToolName("MySkill");

        // Assert
        result.Should().Be("myskill");
    }

    // ── TC-03 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-03: ToMafToolName replaces spaces with dashes.
    /// Input:  "my skill name"
    /// Expected: "my-skill-name"
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public void ToMafToolName_SpacesInInput_ReplacedWithDashes()
    {
        // Act
        var result = _adapter.ToMafToolName("my skill name");

        // Assert
        result.Should().Be("my-skill-name");
    }

    // ── TC-04 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-04: ToMafToolName replaces special characters (dots, underscores, slashes) with dashes.
    /// Input:  "skill.v2/special_name"
    /// Expected: "skill-v2-special-name"
    /// Edge case: multiple consecutive special chars collapse to a single dash.
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public void ToMafToolName_SpecialCharacters_ReplacedWithDashes()
    {
        // Act
        var result = _adapter.ToMafToolName("skill.v2_name");

        // Assert
        result.Should().NotContain(".");
        result.Should().NotContain("_");
    }

    // ── TC-05 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-05: ToMafToolName with empty or whitespace input throws ArgumentException.
    /// Input:  ""  and  "   "
    /// Expected: ArgumentException with message referencing the parameter name.
    /// </summary>
    [Theory(Skip = "M4 skeleton — not yet implemented")]
    [InlineData("")]
    [InlineData("   ")]
    public void ToMafToolName_EmptyInput_ThrowsArgumentException(string input)
    {
        // Act
        var act = () => _adapter.ToMafToolName(input);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    // ── TC-06 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-06: A skill with metadata forwards all key-value pairs into MafToolDescriptor.Metadata.
    /// Input:  SkillDescriptor with Metadata = { "input": "number", "output": "number" }
    /// Expected: descriptor.Metadata contains both keys with unchanged values.
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public void ToMafToolDescriptor_SkillWithMetadata_ForwardsMetadataVerbatim()
    {
        // Arrange
        var skill = new SkillDescriptor
        {
            Id = "meta-skill",
            Name = "Meta Skill",
            Description = "Has metadata",
            Type = "action",
            Metadata = new Dictionary<string, string>
            {
                ["input"] = "number",
                ["output"] = "number"
            }
        };

        // Act
        var descriptor = _adapter.ToMafToolDescriptor(skill);

        // Assert
        descriptor.Metadata.Should().ContainKey("input").WhoseValue.Should().Be("number");
        descriptor.Metadata.Should().ContainKey("output").WhoseValue.Should().Be("number");
    }

    // ── TC-07 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-07: A skill with null Metadata produces an empty (non-null) dictionary.
    /// Input:  SkillDescriptor with Metadata = null
    /// Expected: descriptor.Metadata is an empty dictionary, not null.
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public void ToMafToolDescriptor_NullMetadata_ProducesEmptyDictionary()
    {
        // Arrange
        var skill = new SkillDescriptor
        {
            Id = "no-meta",
            Name = "No Meta",
            Description = "No metadata",
            Type = "action",
            Metadata = null
        };

        // Act
        var descriptor = _adapter.ToMafToolDescriptor(skill);

        // Assert
        descriptor.Metadata.Should().NotBeNull();
        descriptor.Metadata.Should().BeEmpty();
    }

    // ── TC-08 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-08: ProjectToolSetAsync returns exactly one MafToolDescriptor per registered skill.
    /// Input:  Registry with 3 registered skills.
    /// Expected: Result list has 3 items; each SourceSkillId matches a registered skill ID.
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public async Task ProjectToolSetAsync_ThreeSkills_ReturnsThreeDescriptors()
    {
        // Arrange
        var registryMock = new Mock<ISkillRegistry>();
        var skills = new List<SkillDescriptor>
        {
            new() { Id = "s1", Name = "S1", Description = "Skill one", Type = "action" },
            new() { Id = "s2", Name = "S2", Description = "Skill two", Type = "action" },
            new() { Id = "s3", Name = "S3", Description = "Skill three", Type = "action" }
        };
        registryMock.Setup(r => r.ListSkillsAsync()).ReturnsAsync(skills);

        // Act
        var result = await _adapter.ProjectToolSetAsync(registryMock.Object);

        // Assert
        result.Should().HaveCount(3);
        result.Select(d => d.SourceSkillId).Should().BeEquivalentTo("s1", "s2", "s3");
    }

    // ── TC-09 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-09: ProjectToolSetAsync on an empty registry returns an empty list.
    /// Input:  Registry with no registered skills.
    /// Expected: Result is an empty list (not null).
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public async Task ProjectToolSetAsync_EmptyRegistry_ReturnsEmptyList()
    {
        // Arrange
        var registryMock = new Mock<ISkillRegistry>();
        registryMock.Setup(r => r.ListSkillsAsync())
            .ReturnsAsync(new List<SkillDescriptor>());

        // Act
        var result = await _adapter.ProjectToolSetAsync(registryMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    // ── TC-10 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-10: ValidateToolSetMappingAsync reports skill IDs that have empty descriptions
    /// because MAF requires a non-empty description for every tool.
    /// Input:  Registry with 2 skills — one valid, one with empty description.
    /// Expected: Result contains only the ID of the invalid skill.
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public async Task ValidateToolSetMappingAsync_SkillWithEmptyDescription_ReturnsFailure()
    {
        // Arrange
        var registryMock = new Mock<ISkillRegistry>();
        var skills = new List<SkillDescriptor>
        {
            new() { Id = "good", Name = "Good", Description = "Has description", Type = "action" },
            new() { Id = "bad",  Name = "Bad",  Description = "",               Type = "action" }
        };
        registryMock.Setup(r => r.ListSkillsAsync()).ReturnsAsync(skills);

        // Act
        var failures = await _adapter.ValidateToolSetMappingAsync(registryMock.Object);

        // Assert
        failures.Should().ContainSingle().Which.Should().Be("bad");
    }
}
