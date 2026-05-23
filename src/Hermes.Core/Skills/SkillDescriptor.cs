namespace Hermes.Core.Skills;

/// <summary>
/// Immutable descriptor produced by skill parsers after all validation passes.
/// M1 YAML fields (Name, Type, Description) are always required.
/// T14 markdown fields (Id, SchemaVersion, Category, Metadata, Content) are
/// populated by <see cref="MarkdownSkillParser"/> and optional for YAML-loaded skills.
/// </summary>
public sealed class SkillDescriptor
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
}
