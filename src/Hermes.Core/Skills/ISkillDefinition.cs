namespace Hermes.Core.Skills;

/// <summary>
/// Core contract for a skill definition.
/// Implemented by <see cref="SkillDescriptor"/> for the M3 file-backed registry.
/// Designed for forward-compatibility with M4 MAF <c>IToolSet</c> adapters —
/// swap the implementation without touching the registry API.
/// </summary>
public interface ISkillDefinition
{
    /// <summary>Skill display name. Always non-null.</summary>
    string Name { get; }

    /// <summary>Skill type (action, tool, skill, chat, memory, policy).</summary>
    string Type { get; }

    /// <summary>Human-readable description of what the skill does.</summary>
    string Description { get; }

    /// <summary>
    /// Unique skill identifier. Falls back to <see cref="Name"/> when absent
    /// (e.g., YAML-loaded skills without an explicit ID header).
    /// </summary>
    string? Id { get; }

    /// <summary>Schema version for the skill definition format.</summary>
    Version? SchemaVersion { get; }

    /// <summary>Organisational category (optional).</summary>
    string? Category { get; }

    /// <summary>
    /// Arbitrary key-value metadata attached to the skill.
    /// Returns <c>null</c> when no metadata was declared.
    /// </summary>
    IReadOnlyDictionary<string, string>? Metadata { get; }

    /// <summary>Raw content body of the skill file (optional).</summary>
    string? Content { get; }

    /// <summary>
    /// Structured inputs declared by the skill.
    /// <c>null</c> when the skill does not declare explicit inputs.
    /// </summary>
    IReadOnlyDictionary<string, string>? Inputs { get; }

    /// <summary>
    /// Structured outputs declared by the skill.
    /// <c>null</c> when the skill does not declare explicit outputs.
    /// </summary>
    IReadOnlyDictionary<string, string>? Outputs { get; }
}
