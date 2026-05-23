namespace Hermes.Core.Policy;

/// <summary>
/// Defines the scope of a memory access request.
/// Used by <see cref="IPolicyEngine.EvaluateMemoryAccess"/> to enforce R2 isolation.
/// </summary>
public enum MemoryScope
{
    /// <summary>Access is scoped to a single profile (read or write own memory).</summary>
    Profile,

    /// <summary>Access is scoped to a single session within a profile.</summary>
    Session,

    /// <summary>Cross-profile or global access — denied by default in M3.</summary>
    Global,
}
