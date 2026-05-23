namespace Hermes.Core.Profiles;

/// <summary>
/// A named session scoped to a profile. Carries conversation state and metadata.
/// </summary>
public sealed class ProfileSession
{
    public string Id { get; init; } = string.Empty;
    public string ProfileId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastAccessed { get; init; }

    /// <summary>
    /// Arbitrary JSON blob — callers own the schema (Parker uses this for memory metadata).
    /// </summary>
    public string? Metadata { get; init; }
}
