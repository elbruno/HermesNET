namespace Hermes.Core.Policy;

/// <summary>
/// Thrown when a policy evaluation returns <see cref="PolicyVerdict.Deny"/>
/// and the caller enforces the result (e.g., <see cref="IPolicyEngine.ValidateSkill"/>
/// returning Deny causes the skill execution path to throw this exception).
/// </summary>
public sealed class PolicyViolationException : Exception
{
    /// <summary>The policy result that triggered this violation.</summary>
    public PolicyResult PolicyResult { get; }

    public PolicyViolationException(PolicyResult result)
        : base($"Policy violation: {result.Reason}")
    {
        PolicyResult = result;
    }

    public PolicyViolationException(PolicyResult result, Exception inner)
        : base($"Policy violation: {result.Reason}", inner)
    {
        PolicyResult = result;
    }
}
