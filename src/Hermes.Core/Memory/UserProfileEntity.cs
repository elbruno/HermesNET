using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hermes.Core.Memory;

/// <summary>
/// EF Core entity for the USER.md profile state (preferences, interaction norms).
/// One row per profile — enforced by the unique index on ProfileId.
/// </summary>
[Table("UserProfiles")]
public sealed class UserProfileEntity
{
    [Key]
    [MaxLength(36)]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Profile that owns this record. Hard isolation boundary.</summary>
    [Required]
    [MaxLength(100)]
    public string ProfileId { get; init; } = string.Empty;

    /// <summary>Raw Markdown or JSON content of USER.md.</summary>
    [Required]
    public string Data { get; set; } = string.Empty;

    /// <summary>Schema version for forward-compatible migrations.</summary>
    public int SchemaVersion { get; set; } = 1;

    [Required]
    public string CreatedAt { get; init; } = DateTime.UtcNow.ToString("O");

    [Required]
    public string UpdatedAt { get; set; } = DateTime.UtcNow.ToString("O");
}
