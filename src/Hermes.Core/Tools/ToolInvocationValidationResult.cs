namespace Hermes.Core.Tools;

/// <summary>
/// Result returned by <see cref="IToolRegistry.ValidateToolInvocation"/>.
/// <see cref="IsValid"/> is <c>true</c> only when all sandbox checks pass.
/// </summary>
public sealed class ToolInvocationValidationResult
{
    public static readonly ToolInvocationValidationResult Success = new([], []);

    /// <summary>True when <see cref="Errors"/> is empty.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>Blocking errors that prevent invocation.</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>Non-blocking observations (e.g., parameter close to size limit).</summary>
    public IReadOnlyList<string> Warnings { get; }

    public ToolInvocationValidationResult(
        IReadOnlyList<string> errors,
        IReadOnlyList<string> warnings)
    {
        Errors   = errors;
        Warnings = warnings;
    }
}
