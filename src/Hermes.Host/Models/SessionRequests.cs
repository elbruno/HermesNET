using System.ComponentModel.DataAnnotations;

namespace Hermes.Host.Models;

/// <summary>Request body for POST /api/sessions.</summary>
public sealed class CreateSessionRequest
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public string ProfileId { get; init; } = string.Empty;
}

/// <summary>Request body for PUT /api/sessions/{id}.</summary>
public sealed class UpdateSessionRequest
{
    [Required]
    public string Name { get; init; } = string.Empty;
}
