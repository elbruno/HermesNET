namespace Hermes.Core.Memory;

/// <summary>
/// Snapshot returned by <see cref="IMemoryService.LoadMemoryAsync"/>.
/// Represents the current MEMORY.md content for a profile.
/// </summary>
public sealed record MemoryContext(
    string ProfileId,
    string Content,
    string Format,
    int Version,
    DateTime UpdatedAt)
{
    /// <summary>An empty context — returned when no memory has been written yet.</summary>
    public static MemoryContext Empty(string profileId) =>
        new(profileId, string.Empty, "markdown", 0, DateTime.MinValue);

    public bool IsEmpty => string.IsNullOrEmpty(Content);
}

/// <summary>
/// Snapshot returned by <see cref="IMemoryService.LoadUserProfileAsync"/>.
/// Represents the current USER.md content for a profile.
/// </summary>
public sealed record UserProfileData(
    string ProfileId,
    string Data,
    int SchemaVersion,
    DateTime UpdatedAt)
{
    public static UserProfileData Empty(string profileId) =>
        new(profileId, string.Empty, 0, DateTime.MinValue);

    public bool IsEmpty => string.IsNullOrEmpty(Data);
}

/// <summary>
/// Validation contract for memory writes.
/// Defines size limits and format constraints.
/// </summary>
public sealed record MemorySchema(
    int MaxContentBytes,
    IReadOnlyList<string> SupportedFormats,
    int CurrentSchemaVersion)
{
    /// <summary>Default schema: 64 KB cap, Markdown only.</summary>
    public static MemorySchema Default { get; } = new(
        MaxContentBytes: 65_536,
        SupportedFormats: new[] { "markdown" },
        CurrentSchemaVersion: 1);

    public bool IsContentValid(string content) =>
        System.Text.Encoding.UTF8.GetByteCount(content) <= MaxContentBytes;

    public bool IsFormatSupported(string format) =>
        SupportedFormats.Contains(format, StringComparer.OrdinalIgnoreCase);
}
