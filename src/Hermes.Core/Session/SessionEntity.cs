namespace Hermes.Core.Session;

public sealed class SessionEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProfileId { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("O");
    public string UpdatedAt { get; set; } = DateTime.UtcNow.ToString("O");
    public string? LastMessage { get; set; }
    public int MessageCount { get; set; }
}
