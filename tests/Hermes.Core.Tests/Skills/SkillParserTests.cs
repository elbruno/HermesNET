using Hermes.Core.Skills;

namespace Hermes.Core.Tests.Skills;

/// <summary>
/// T9 — YAML Skill Parser validation tests (R5-B gate).
///
/// 5 malformed cases + 1 valid case = 6 tests required.
/// All malformed inputs must throw <see cref="SkillParseException"/> with a
/// non-empty descriptive message.  The valid case must return a populated
/// <see cref="SkillDescriptor"/> without throwing.
///
/// Unknown-key policy documented: SkillParser THROWS on unknown keys
/// (fail-fast prevents silent corrupt state propagating into M2+ skill logic).
/// </summary>
public sealed class SkillParserTests
{
    private readonly SkillParser _parser = new();

    // ── Malformed Case 1: Missing required field `name` ───────────────────────

    [Fact]
    public void SkillParser_WithMissingName_ThrowsSkillParseException()
    {
        var yaml = """
            type: action
            description: test skill with no name
            """;

        var act = () => _parser.Parse(yaml);

        act.Should().Throw<SkillParseException>()
           .WithMessage("*name*");
    }

    // ── Malformed Case 2: Invalid `type` value ────────────────────────────────

    [Fact]
    public void SkillParser_WithInvalidType_ThrowsSkillParseException()
    {
        var yaml = """
            name: test-skill
            type: invalid_type
            description: bad type value
            """;

        var act = () => _parser.Parse(yaml);

        act.Should().Throw<SkillParseException>()
           .WithMessage("*Invalid type*invalid_type*");
    }

    // ── Malformed Case 3: `description` present but null (YAML `~`) ──────────

    [Fact]
    public void SkillParser_WithNullDescription_ThrowsSkillParseException()
    {
        // description: ~ is YAML null
        var yaml = """
            name: test-skill
            description: ~
            """;

        var act = () => _parser.Parse(yaml);

        act.Should().Throw<SkillParseException>()
           .WithMessage("*Description cannot be null*");
    }

    // ── Malformed Case 4: Extra unknown key present ───────────────────────────
    // Policy: THROW on unknown keys (documented in SkillParser.cs header).

    [Fact]
    public void SkillParser_WithUnknownField_ThrowsSkillParseException()
    {
        var yaml = """
            name: test-skill
            type: action
            description: does something
            unknown_key: extra field not in schema
            """;

        var act = () => _parser.Parse(yaml);

        act.Should().Throw<SkillParseException>()
           .WithMessage("*Unknown field*unknown_key*");
    }

    // ── Malformed Case 5: Empty YAML document ────────────────────────────────

    [Fact]
    public void SkillParser_WithEmptyYaml_ThrowsSkillParseException()
    {
        var act = () => _parser.Parse(string.Empty);

        act.Should().Throw<SkillParseException>()
           .WithMessage("*Empty YAML*");
    }

    // ── Valid Case: Minimal well-formed skill YAML ────────────────────────────

    [Fact]
    public void SkillParser_WithValidMinimalSkill_ReturnsSkillDescriptor()
    {
        var yaml = """
            name: echo-test
            type: action
            description: A minimal valid skill for testing
            """;

        var result = _parser.Parse(yaml);

        result.Should().NotBeNull();
        result.Name.Should().Be("echo-test");
        result.Type.Should().Be("action");
        result.Description.Should().Be("A minimal valid skill for testing");
    }

    // ── T17 Front Matter: Valid YAML front matter ─────────────────────────────

    [Fact]
    public void SkillParser_WithFrontMatter_ReturnsSkillDescriptor()
    {
        var input = """
            ---
            name: fm-skill
            description: A skill using YAML front matter
            version: 1.2
            type: tool
            ---
            # Body Content

            Markdown body here.
            """;

        var result = _parser.Parse(input);

        result.Should().NotBeNull();
        result.Name.Should().Be("fm-skill");
        result.Type.Should().Be("tool");
        result.Description.Should().Be("A skill using YAML front matter");
        result.SchemaVersion.Should().Be(new Version(1, 2));
        result.Content.Should().Contain("Body Content");
    }

    // ── T17 Front Matter: Memory type ─────────────────────────────────────────

    [Fact]
    public void SkillParser_WithMemoryType_ReturnsDescriptor()
    {
        var input = """
            ---
            name: context-recall
            description: Recalls the active memory context
            type: memory
            ---
            """;

        var result = _parser.Parse(input);
        result.Type.Should().Be("memory");
    }

    // ── T17 Front Matter: Policy type ─────────────────────────────────────────

    [Fact]
    public void SkillParser_WithPolicyType_ReturnsDescriptor()
    {
        var input = """
            ---
            name: access-control
            description: Enforces access control rules
            type: policy
            ---
            """;

        var result = _parser.Parse(input);
        result.Type.Should().Be("policy");
    }

    // ── T17 Front Matter: Extra fields stored as metadata ─────────────────────

    [Fact]
    public void SkillParser_WithFrontMatterExtraFields_StoredAsMetadata()
    {
        var input = """
            ---
            name: metadata-skill
            description: Skill with extra metadata fields
            type: tool
            author: dallas
            scope: profile
            ---
            """;

        var result = _parser.Parse(input);

        result.Metadata.Should().NotBeNull();
        result.Metadata!["author"].Should().Be("dallas");
        result.Metadata["scope"].Should().Be("profile");
    }

    // ── T17 Front Matter: Missing closing delimiter ───────────────────────────

    [Fact]
    public void SkillParser_WithFrontMatterMissingClosingDelimiter_Throws()
    {
        var input = """
            ---
            name: incomplete
            description: Missing closing delimiter
            type: tool
            """;

        var act = () => _parser.Parse(input);
        act.Should().Throw<SkillParseException>()
           .WithMessage("*---*");
    }

    // ── T17 Flat YAML: Version field is allowed ────────────────────────────────

    [Fact]
    public void SkillParser_FlatYaml_WithVersionField_DoesNotThrow()
    {
        var yaml = """
            name: versioned-skill
            type: action
            description: Flat YAML with version field
            version: 1.0
            """;

        var result = _parser.Parse(yaml);
        result.Name.Should().Be("versioned-skill");
    }
}
