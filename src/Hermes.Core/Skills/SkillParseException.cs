namespace Hermes.Core.Skills;

/// <summary>
/// Thrown by <see cref="SkillParser"/> when a YAML document fails validation.
/// Carries a human-readable message describing exactly which field or rule
/// caused the rejection, so callers can report meaningful errors to users.
/// </summary>
public sealed class SkillParseException : Exception
{
    public SkillParseException(string message) : base(message) { }
    public SkillParseException(string message, Exception inner) : base(message, inner) { }
}
