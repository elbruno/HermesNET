namespace Hermes.Core.Skills;

/// <summary>
/// Abstracts skill discovery, loading, and validation from a concrete source
/// (filesystem, MAF <c>IToolSet</c>, remote registry, …).
///
/// <para>
/// Decouples <see cref="SkillRegistry"/> from file-system specifics and provides
/// the seam for the M3→M4 MAF boundary: replace this implementation without
/// changing any consumer of <see cref="ISkillRegistry"/>.
/// </para>
///
/// <list type="bullet">
///   <item><see cref="DiscoverAsync"/> returns addressable skill paths or IDs.</item>
///   <item><see cref="LoadAsync"/> parses a single skill from its path.</item>
///   <item><see cref="ValidateAsync"/> validates a parsed definition in isolation.</item>
/// </list>
/// </summary>
public interface ISkillProvider
{
    /// <summary>
    /// Discovers available skill paths within <paramref name="directory"/>.
    /// Returns only <c>.md</c> files for the default filesystem implementation.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when <paramref name="directory"/> does not exist.
    /// </exception>
    Task<IReadOnlyList<string>> DiscoverAsync(
        string directory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads and parses a single skill definition from <paramref name="skillPath"/>.
    /// </summary>
    /// <exception cref="SkillParseException">
    /// Thrown when the file content cannot be parsed or fails validation rules.
    /// </exception>
    Task<ISkillDefinition> LoadAsync(
        string skillPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates <paramref name="skill"/> in isolation (no registry lookup required).
    /// Returns <see cref="SkillValidationResult.Success"/> when all checks pass.
    /// </summary>
    Task<SkillValidationResult> ValidateAsync(ISkillDefinition skill);
}
