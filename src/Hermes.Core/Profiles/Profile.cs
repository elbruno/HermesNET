namespace Hermes.Core.Profiles;

/// <summary>
/// A named configuration profile. Provides isolation scope for sessions and memory.
/// </summary>
public sealed class Profile
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
