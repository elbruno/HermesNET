using System.ComponentModel.DataAnnotations;

namespace Hermes.Host.Models;

/// <summary>Request body for POST /api/chat.</summary>
public sealed class ChatRequest
{
    /// <summary>The user's message to the assistant.</summary>
    [Required]
    public string Message { get; init; } = string.Empty;

    /// <summary>Profile ID scoping this conversation.</summary>
    [Required]
    public string ProfileId { get; init; } = string.Empty;

    /// <summary>Session ID for conversation continuity.</summary>
    [Required]
    public string SessionId { get; init; } = string.Empty;
}
