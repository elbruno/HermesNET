namespace Hermes.Core.Skills;

/// <summary>
/// Immutable record produced by <see cref="SkillParser.Parse"/> after all
/// validation rules pass.  Consumers should treat this as the single source of
/// truth for a skill's identity and type.
/// </summary>
public sealed class SkillDescriptor
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Description { get; init; }
}
