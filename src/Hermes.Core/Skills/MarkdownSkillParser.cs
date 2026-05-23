using System.Text;

namespace Hermes.Core.Skills;

/// <summary>
/// Parses markdown <c>.md</c> skill files into <see cref="SkillDescriptor"/>.
///
/// Expected format:
/// <code>
/// # Skill ID: &lt;id&gt;
/// **Version:** &lt;major.minor&gt;
/// **Description:** &lt;description&gt;
/// **Type:** action|tool|skill|chat
/// **Category:** &lt;optional&gt;
///
/// ## Metadata
/// - Key: Value
///
/// ## Implementation Notes
/// Free-form content body …
/// </code>
///
/// Validation rules (applied after parsing):
///   1. Empty / whitespace-only document → SkillParseException("Empty markdown")
///   2. First non-blank line must be <c># Skill ID: &lt;id&gt;</c> → else SkillParseException("Missing required field: id")
///   3. <c>**Version:**</c> absent or unparseable → SkillParseException("Missing required field: version")
///   4. <c>**Description:**</c> absent or blank → SkillParseException("Missing required field: description")
///   5. <c>**Type:**</c> absent or blank → SkillParseException("Missing required field: type")
///   6. Type not in valid set → SkillParseException("Invalid type: …")
///   7. Version string not parseable by <see cref="Version.TryParse"/> → SkillParseException
///
/// UTF-8 BOM is stripped before parsing if present.
/// </summary>
public sealed class MarkdownSkillParser
{
    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "action", "tool", "skill", "chat", "memory", "policy"
    };

    /// <summary>
    /// Parse <paramref name="markdown"/> and return a validated <see cref="SkillDescriptor"/>.
    /// Throws <see cref="SkillParseException"/> on any validation failure.
    /// </summary>
    public SkillDescriptor Parse(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            throw new SkillParseException("Empty markdown");

        // Strip UTF-8 BOM (\uFEFF) so files written with BOM are handled correctly.
        if (markdown[0] == '\uFEFF')
            markdown = markdown[1..];

        var lines = markdown.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        string? id          = null;
        string? version     = null;
        string? description = null;
        string? type        = null;
        string? category    = null;
        var metadata        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var contentLines    = new List<string>();

        bool headerParsed   = false; // true once the "# Skill ID:" line has been consumed
        bool inContent      = false; // true once we have left the header area
        string? currentSection = null;

        foreach (var line in lines)
        {
            if (!inContent)
            {
                // Skip blank lines before the # Skill ID header.
                if (!headerParsed && string.IsNullOrWhiteSpace(line))
                    continue;

                // Expect the first non-blank line to be "# Skill ID: <id>".
                if (!headerParsed)
                {
                    if (line.StartsWith("# Skill ID:", StringComparison.OrdinalIgnoreCase))
                    {
                        id = line["# Skill ID:".Length..].Trim();
                        headerParsed = true;
                        continue;
                    }
                    throw new SkillParseException("Missing required field: id");
                }

                // Blank line between header and content body transitions to content mode.
                if (string.IsNullOrWhiteSpace(line))
                {
                    inContent = true;
                    continue;
                }

                // Section header ("## ...") also transitions to content mode.
                if (line.StartsWith("## "))
                {
                    inContent = true;
                    currentSection = line[3..].Trim();
                    contentLines.Add(line);
                    continue;
                }

                // Bold markdown header field: **Key:** Value
                if (line.StartsWith("**"))
                {
                    var parsed = TryParseBoldField(line);
                    if (parsed.HasValue)
                    {
                        var (key, value) = parsed.Value;
                        switch (key.ToLowerInvariant())
                        {
                            case "version":     version     = value; break;
                            case "description": description = value; break;
                            case "type":        type        = value; break;
                            case "category":    category    = value; break;
                        }
                        continue;
                    }
                }

                // Unrecognised line in header area — ignore silently.
            }
            else
            {
                // Content body: track current section and collect lines.
                if (line.StartsWith("## "))
                {
                    currentSection = line[3..].Trim();
                    contentLines.Add(line);
                    continue;
                }

                // Inside a Metadata section, parse "- Key: Value" pairs.
                if (currentSection?.Equals("Metadata", StringComparison.OrdinalIgnoreCase) == true
                    && line.TrimStart().StartsWith("- "))
                {
                    var metaLine  = line.TrimStart()[2..]; // strip leading "- "
                    var colonIdx  = metaLine.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        var mKey   = metaLine[..colonIdx].Trim();
                        var mValue = metaLine[(colonIdx + 1)..].Trim();
                        metadata[mKey] = mValue;
                    }
                }

                contentLines.Add(line);
            }
        }

        // ── Validate required fields ───────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(id))
            throw new SkillParseException("Missing required field: id");

        if (string.IsNullOrWhiteSpace(version))
            throw new SkillParseException("Missing required field: version");

        if (string.IsNullOrWhiteSpace(description))
            throw new SkillParseException("Missing required field: description");

        if (string.IsNullOrWhiteSpace(type))
            throw new SkillParseException("Missing required field: type");

        if (!ValidTypes.Contains(type))
            throw new SkillParseException(
                $"Invalid type: {type}. Valid types: action, tool, skill, chat, memory, policy");

        if (!Version.TryParse(version, out var schemaVersion))
            throw new SkillParseException(
                $"Invalid version format: '{version}'. Expected format: major.minor (e.g., 1.0)");

        return new SkillDescriptor
        {
            Id            = id,
            Name          = id, // markdown format has no separate display-name field
            Description   = description,
            Type          = type.ToLowerInvariant(),
            Category      = string.IsNullOrWhiteSpace(category) ? null : category,
            SchemaVersion = schemaVersion,
            Metadata      = metadata.Count > 0
                                ? (IReadOnlyDictionary<string, string>)metadata
                                : null,
            Content       = contentLines.Count > 0
                                ? string.Join("\n", contentLines).TrimEnd()
                                : null
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Matches lines of the form <c>**Key:** Value</c> and returns (key, value).
    /// Returns null if the line does not match the bold-field pattern.
    /// </summary>
    private static (string key, string value)? TryParseBoldField(string line)
    {
        if (!line.StartsWith("**"))
            return null;

        var closeBold = line.IndexOf(":**", 2, StringComparison.Ordinal);
        if (closeBold < 0)
            return null;

        var key   = line[2..closeBold];
        var value = line[(closeBold + 3)..].Trim();
        return (key, value);
    }
}
