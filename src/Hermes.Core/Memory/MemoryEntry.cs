using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hermes.Core.Memory;

/// <summary>
/// EF Core entity for profile-scoped curated memory (MEMORY.md and USER.md rows).
/// ProfileId is the hard isolation boundary — no query should cross it.
/// </summary>
[Table("Memory")]
public sealed class MemoryEntry
{
    [Key]
    [MaxLength(36)]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Profile that owns this memory entry. Hard FK boundary.</summary>
    [Required]
    [MaxLength(100)]
    public string ProfileId { get; init; } = string.Empty;

    /// <summary>"memory" = MEMORY.md, "user_profile" = USER.md</summary>
    [Required]
    [MaxLength(32)]
    public string Kind { get; init; } = MemoryKind.Memory;

    /// <summary>Full Markdown content of the memory file.</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>Content format — always "markdown" in MVP; reserved for future binary/JSON types.</summary>
    [MaxLength(32)]
    public string Format { get; init; } = "markdown";

    /// <summary>Monotonically increasing write counter for optimistic concurrency.</summary>
    public int Version { get; set; } = 1;

    [Required]
    public string CreatedAt { get; init; } = DateTime.UtcNow.ToString("O");

    [Required]
    public string UpdatedAt { get; set; } = DateTime.UtcNow.ToString("O");
}

/// <summary>Well-known values for <see cref="MemoryEntry.Kind"/>.</summary>
public static class MemoryKind
{
    public const string Memory = "memory";
    public const string UserProfile = "user_profile";
}
