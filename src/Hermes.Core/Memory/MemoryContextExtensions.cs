namespace Hermes.Core.Memory;

/// <summary>
/// Projection helpers for <see cref="MemoryContext"/> and <see cref="UserProfileData"/>.
/// Used by agents and skills to format curated memory for system-prompt injection.
/// </summary>
public static class MemoryContextExtensions
{
    /// <summary>
    /// Formats the memory context as a labelled Markdown block for agent system-prompt injection.
    /// Returns an empty string when the context contains no content.
    /// </summary>
    public static string ToMemoryBlock(this MemoryContext context)
    {
        if (context.IsEmpty) return string.Empty;
        return $"## Curated Memory (Profile: {context.ProfileId})\n\n{context.Content}\n";
    }

    /// <summary>Returns the raw content string, suitable for display or logging.</summary>
    public static string GetMemoryAsText(this MemoryContext context) => context.Content;

    /// <summary>
    /// Returns a single-line summary for CLI display (profile, version, byte size, timestamp).
    /// </summary>
    public static string ToDisplaySummary(this MemoryContext context)
    {
        if (context.IsEmpty)
            return $"[Profile: {context.ProfileId}] No memory recorded.";

        var bytes = System.Text.Encoding.UTF8.GetByteCount(context.Content);
        return $"[Profile: {context.ProfileId}] v{context.Version} — {bytes:N0} bytes — updated {context.UpdatedAt:u}";
    }

    /// <summary>
    /// Formats the user profile data as a labelled Markdown block for agent injection.
    /// Returns an empty string when the profile contains no data.
    /// </summary>
    public static string ToMemoryBlock(this UserProfileData profile)
    {
        if (profile.IsEmpty) return string.Empty;
        return $"## User Profile (Profile: {profile.ProfileId})\n\n{profile.Data}\n";
    }

    /// <summary>Returns the raw profile data string.</summary>
    public static string GetMemoryAsText(this UserProfileData profile) => profile.Data;
}
