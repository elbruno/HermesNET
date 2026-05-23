namespace Hermes.Core.Skills;

/// <summary>
/// Thrown by <see cref="SkillRegistry.LoadFromDirectoryAsync"/> when two skill
/// files in the same directory declare the same skill ID.
/// </summary>
public sealed class DuplicateSkillException : Exception
{
    public string SkillId { get; }

    public DuplicateSkillException(string skillId)
        : base($"Duplicate skill ID: {skillId}")
    {
        SkillId = skillId;
    }
}
