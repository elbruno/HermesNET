using System.ComponentModel.DataAnnotations;

namespace Hermes.Host.Models;

/// <summary>Request body for POST /api/profiles.</summary>
public sealed class CreateProfileRequest
{
    [Required]
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }
}

/// <summary>Request body for PUT /api/profiles/{id}.</summary>
public sealed class UpdateProfileRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
}
