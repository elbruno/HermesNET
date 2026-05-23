namespace Hermes.Core.Policy;

/// <summary>
/// The enforcement verdict returned by every policy evaluation.
/// </summary>
public enum PolicyVerdict
{
    /// <summary>Request is permitted as-is.</summary>
    Allow,

    /// <summary>Request is blocked; caller should throw <see cref="PolicyViolationException"/>.</summary>
    Deny,

    /// <summary>
    /// Request is permitted after sensitive data is removed from the input.
    /// Applicable only to <see cref="IPolicyEngine.ValidateToolRequest"/>.
    /// </summary>
    Redact,
}
