namespace Hermes.Core.Skills;

/// <summary>
/// Parses skill definitions from two formats into <see cref="SkillDescriptor"/>:
///
/// <b>Format A — YAML front matter (M2/T17):</b>
/// <code>
/// ---
/// name: skill-name
/// description: What the skill does
/// version: 1.0
/// type: tool
/// extra-key: stored as metadata
/// ---
/// Body content (Markdown) follows here …
/// </code>
/// Extra fields beyond the known set are stored as Metadata (flexible key-value).
///
/// <b>Format B — Flat YAML (M1 legacy):</b>
/// Plain "key: value" pairs without front-matter delimiters.
///
/// Validation rules common to both formats (applied in order):
///   1. Empty / whitespace-only document → SkillParseException("Empty YAML")
///   2. `name` absent or null             → SkillParseException("Missing required field: name")
///   3. `description` present but null   → SkillParseException("Description cannot be null")
///   4. `description` absent             → SkillParseException("Missing required field: description")
///   5. `type` absent or null             → SkillParseException("Missing required field: type")
///   6. `type` not in valid set           → SkillParseException("Invalid type: ...")
///
/// Format B additional rule (fail-fast):
///   7. Any key not in the known set     → SkillParseException("Unknown field: ...")
///
/// YAML subset supported: flat block-style key:value only (no nested mappings).
/// Valid types: action, tool, skill, chat, memory, policy
/// </summary>
public sealed class SkillParser
{
    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "action", "tool", "skill", "chat", "memory", "policy"
    };

    // Fields that are "reserved" in Format B (flat YAML); unknown keys throw.
    private static readonly HashSet<string> KnownFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "type", "description", "version"
    };

    // Fields consumed as first-class properties in front-matter Format A.
    private static readonly HashSet<string> CoreFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "type", "description", "version"
    };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Parse <paramref name="yaml"/> and return a validated <see cref="SkillDescriptor"/>.
    /// Accepts both YAML front matter (with <c>---</c> delimiters) and flat YAML.
    /// Throws <see cref="SkillParseException"/> on any validation failure.
    /// </summary>
    public SkillDescriptor Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            throw new SkillParseException("Empty YAML");

        // Detect front-matter format by leading `---` delimiter.
        if (yaml.TrimStart().StartsWith("---", StringComparison.Ordinal))
            return ParseFrontMatter(yaml);

        return ParseFlatYamlDocument(yaml);
    }

    // ── Front-matter parser (Format A) ───────────────────────────────────────

    private SkillDescriptor ParseFrontMatter(string input)
    {
        // Normalise line endings.
        var normalized = input.Replace("\r\n", "\n").Replace("\r", "\n");

        // Find opening `---`
        var firstDelim = normalized.IndexOf("---", StringComparison.Ordinal);
        if (firstDelim < 0)
            throw new SkillParseException("Invalid YAML front matter: missing opening '---'");

        var afterFirstDelim = normalized.IndexOf('\n', firstDelim);
        if (afterFirstDelim < 0)
            throw new SkillParseException("Invalid YAML front matter: no content after '---'");

        // Find closing `---`
        var secondDelim = normalized.IndexOf("\n---", afterFirstDelim, StringComparison.Ordinal);
        if (secondDelim < 0)
            throw new SkillParseException("Invalid YAML front matter: missing closing '---'");

        var yamlBlock = normalized[(afterFirstDelim + 1)..secondDelim];
        var body      = normalized[(secondDelim + 4)..].TrimStart('\n');

        if (string.IsNullOrWhiteSpace(yamlBlock))
            throw new SkillParseException("Empty YAML");

        var fields = ParseFlatYaml(yamlBlock);

        if (fields.Count == 0)
            throw new SkillParseException("Empty YAML");

        // Validate required fields
        ValidateRequiredFields(fields);

        var name        = fields["name"]!;
        var description = fields["description"]!;
        var type        = fields["type"]!;

        fields.TryGetValue("version", out var versionStr);
        Version? version = null;
        if (versionStr is not null && !Version.TryParse(versionStr, out version))
            throw new SkillParseException(
                $"Invalid version format: '{versionStr}'. Expected format: major.minor (e.g., 1.0)");

        // Extra fields become metadata.
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, val) in fields)
        {
            if (!CoreFields.Contains(key) && val is not null)
                metadata[key] = val;
        }

        return new SkillDescriptor
        {
            Name          = name,
            Type          = type.ToLowerInvariant(),
            Description   = description,
            SchemaVersion = version,
            Metadata      = metadata.Count > 0
                                ? (IReadOnlyDictionary<string, string>)metadata
                                : null,
            Content       = string.IsNullOrWhiteSpace(body) ? null : body
        };
    }

    // ── Flat YAML document parser (Format B — M1 legacy) ────────────────────

    private SkillDescriptor ParseFlatYamlDocument(string yaml)
    {
        var fields = ParseFlatYaml(yaml);

        if (fields.Count == 0)
            throw new SkillParseException("Empty YAML");

        // Format B: reject unknown keys before validating required ones.
        foreach (var key in fields.Keys)
        {
            if (!KnownFields.Contains(key))
                throw new SkillParseException($"Unknown field: {key}");
        }

        ValidateRequiredFields(fields);

        return new SkillDescriptor
        {
            Name        = fields["name"]!,
            Type        = fields["type"]!.ToLowerInvariant(),
            Description = fields["description"]!
        };
    }

    // ── Shared validation ─────────────────────────────────────────────────────

    private static void ValidateRequiredFields(Dictionary<string, string?> fields)
    {
        if (!fields.TryGetValue("name", out var name) || name is null)
            throw new SkillParseException("Missing required field: name");

        if (fields.TryGetValue("description", out var description))
        {
            if (description is null)
                throw new SkillParseException("Description cannot be null");
        }
        else
        {
            throw new SkillParseException("Missing required field: description");
        }

        if (!fields.TryGetValue("type", out var type) || type is null)
            throw new SkillParseException("Missing required field: type");

        if (!ValidTypes.Contains(type))
            throw new SkillParseException(
                $"Invalid type: {type}. Valid types: action, tool, skill, chat, memory, policy");
    }

    // ── Flat YAML tokeniser ───────────────────────────────────────────────────
    // Handles simple block-style YAML: one "key: value" pair per line.
    // Null YAML values (~, null, or empty after colon) map to C# null.
    // Quoted string values have surrounding quotes stripped.

    private static Dictionary<string, string?> ParseFlatYaml(string yaml)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in yaml.Split('\n'))
        {
            var line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            var colonIdx = line.IndexOf(':');
            if (colonIdx <= 0)
                continue;

            var key       = line[..colonIdx].Trim();
            var valuePart = line[(colonIdx + 1)..].Trim();

            string? value = valuePart is "~" or "null" or ""
                ? null
                : valuePart.Trim('"', '\'');

            result[key] = value;
        }

        return result;
    }
}
