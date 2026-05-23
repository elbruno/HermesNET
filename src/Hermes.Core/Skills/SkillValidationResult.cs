namespace Hermes.Core.Skills;

/// <summary>
/// Result of a <see cref="ISkillRegistry.ValidateAsync"/> call.
/// <see cref="IsValid"/> is true when <see cref="Errors"/> is empty.
/// <see cref="Warnings"/> are non-blocking observations (e.g., forward-compat schema).
/// </summary>
public sealed class SkillValidationResult
{
    public static readonly SkillValidationResult Success = new([], []);

    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<string> Warnings { get; }

    public SkillValidationResult(IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
    {
        Errors   = errors;
        Warnings = warnings;
    }
}
