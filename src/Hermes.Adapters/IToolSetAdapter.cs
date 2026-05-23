using Hermes.Core.Skills;

namespace Hermes.Adapters;

/// <summary>
/// Adapter contract that bridges <see cref="ISkillRegistry"/> to the MAF IToolSet surface.
///
/// <para>
/// MAF (M4) will consume an IToolSet that enumerates named tools with typed parameters.
/// This adapter translates the Hermes skill registry — which stores <see cref="SkillDescriptor"/>
/// objects — into the flat tool-list contract expected by the orchestrator.
/// </para>
///
/// <para>
/// M3 status: stub only. No MAF dependency is present yet. All methods return
/// pass-through or placeholder results. The interface shape matches what M4 will require.
/// </para>
/// </summary>
public interface IToolSetAdapter
{
    /// <summary>
    /// Returns a MAF-compatible tool name for the given Hermes skill ID.
    /// Normalises casing and replaces characters that are illegal in MAF tool names.
    /// </summary>
    /// <param name="skillId">The skill ID from <see cref="ISkillRegistry"/>.</param>
    /// <returns>A sanitised MAF tool name string.</returns>
    string ToMafToolName(string skillId);

    /// <summary>
    /// Projects all skills currently registered in <paramref name="registry"/> into
    /// a list of lightweight tool descriptors suitable for MAF IToolSet initialisation.
    /// </summary>
    /// <param name="registry">The source skill registry.</param>
    /// <param name="cancellationToken">Propagates cancellation.</param>
    /// <returns>An ordered list of <see cref="MafToolDescriptor"/> instances.</returns>
    Task<IReadOnlyList<MafToolDescriptor>> ProjectToolSetAsync(
        ISkillRegistry registry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a single <see cref="SkillDescriptor"/> to a <see cref="MafToolDescriptor"/>.
    /// </summary>
    MafToolDescriptor ToMafToolDescriptor(SkillDescriptor skill);

    /// <summary>
    /// Validates that every skill in <paramref name="registry"/> can be represented
    /// as a valid MAF tool. Returns a list of skill IDs that fail validation.
    /// </summary>
    Task<IReadOnlyList<string>> ValidateToolSetMappingAsync(
        ISkillRegistry registry,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight DTO representing a MAF tool entry projected from a Hermes skill.
/// This is a stub type; M4 will replace it with the real MAF IToolSet descriptor.
/// </summary>
public sealed class MafToolDescriptor
{
    /// <summary>MAF-normalised tool name.</summary>
    public required string ToolName { get; init; }

    /// <summary>Human-readable description forwarded from <see cref="SkillDescriptor.Description"/>.</summary>
    public required string Description { get; init; }

    /// <summary>Original Hermes skill ID — preserved for round-trip traceability.</summary>
    public required string SourceSkillId { get; init; }

    /// <summary>Skill category, if available.</summary>
    public string? Category { get; init; }

    /// <summary>Key/value metadata forwarded verbatim from the skill descriptor.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();
}
