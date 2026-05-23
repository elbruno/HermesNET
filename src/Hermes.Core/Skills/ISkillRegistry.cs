namespace Hermes.Core.Skills;

/// <summary>
/// In-memory + file-backed skill registry.
/// Provides O(1) lookup by skill ID and idempotent directory loading.
/// </summary>
public interface ISkillRegistry
{
    /// <summary>
    /// Returns the descriptor for <paramref name="skillId"/>.
    /// Throws <see cref="KeyNotFoundException"/> with the ID in the message if not found.
    /// </summary>
    Task<SkillDescriptor> GetSkillAsync(string skillId);

    /// <summary>Returns all loaded skill descriptors.</summary>
    Task<IReadOnlyList<SkillDescriptor>> ListSkillsAsync();

    /// <summary>
    /// Searches by skill ID, Name, or Description (case-insensitive).
    /// Returns <c>null</c> if not found — never throws.
    /// </summary>
    Task<SkillDescriptor?> FindByNameAsync(string name);

    /// <summary>
    /// Validates the loaded skill identified by <paramref name="skillId"/>.
    /// Returns <see cref="SkillValidationResult.Success"/> when all checks pass.
    /// </summary>
    Task<SkillValidationResult> ValidateAsync(string skillId);

    /// <summary>
    /// Loads all <c>.md</c> files from <paramref name="skillsDirectory"/> into the registry.
    /// Idempotent: files already loaded are skipped. Non-.md files are ignored.
    /// Throws <see cref="DuplicateSkillException"/> if two files share a skill ID.
    /// </summary>
    Task LoadFromDirectoryAsync(string skillsDirectory);

    /// <summary>Non-blocking warnings raised during <see cref="LoadFromDirectoryAsync"/>.</summary>
    IReadOnlyList<string> LoadWarnings { get; }
}
