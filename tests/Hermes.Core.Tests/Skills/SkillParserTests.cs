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
}
