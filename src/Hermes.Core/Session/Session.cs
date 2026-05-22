namespace Hermes.Core.Session;

/// <summary>
/// Represents a single Hermes chat session tied to a profile.
/// </summary>
public class Session
{
    public string Id { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public string? LastMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
