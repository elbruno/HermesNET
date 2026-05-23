namespace Hermes.Core.Skills;

/// <summary>
/// Immutable descriptor produced by skill parsers after all validation passes.
/// Implements <see cref="ISkillDefinition"/> for use with <see cref="ISkillProvider"/>
/// and <see cref="ISkillRegistry"/>.
///
/// <list type="bullet">
///   <item>M1 YAML fields (Name, Type, Description) are always required.</item>
///   <item>T14 markdown fields (Id, SchemaVersion, Category, Metadata, Content)
///         are populated by <see cref="MarkdownSkillParser"/> and optional for
///         YAML-loaded skills.</item>
///   <item>T33 abstraction fields (Inputs, Outputs) default to <c>null</c> and
///         are populated when a skill explicitly declares structured I/O.</item>
/// </list>
/// </summary>
public sealed class SkillDescriptor : ISkillDefinition
{
    // ── M1 YAML core fields (required for all skill sources) ──────────────────
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Description { get; init; }

    // ── T14 markdown-only fields ───────────────────────────────────────────────
    public string? Id { get; init; }
    public Version? SchemaVersion { get; init; }
    public string? Category { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
    public string? Content { get; init; }

    // ── T33 structured I/O fields (optional, null when not declared) ──────────
    /// <summary>
    /// Structured inputs declared by the skill.
    /// <c>null</c> when the skill does not declare explicit inputs.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Inputs { get; init; }

    /// <summary>
    /// Structured outputs declared by the skill.
    /// <c>null</c> when the skill does not declare explicit outputs.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Outputs { get; init; }
}
