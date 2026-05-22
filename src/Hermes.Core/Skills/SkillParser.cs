namespace Hermes.Core.Skills;

/// <summary>
/// Parses flat-key-value YAML documents into <see cref="SkillDescriptor"/>.
///
/// Validation rules (applied in order):
///   1. Empty / whitespace-only document → SkillParseException("Empty YAML")
///   2. Any key not in the known set      → SkillParseException("Unknown field: &lt;key&gt;")
///   3. `name` absent or null             → SkillParseException("Missing required field: name")
///   4. `description` present but null   → SkillParseException("Description cannot be null")
///   5. `description` absent             → SkillParseException("Missing required field: description")
///   6. `type` absent or null             → SkillParseException("Missing required field: type")
///   7. `type` not in valid set           → SkillParseException("Invalid type: &lt;value&gt;. Valid types: action, tool, skill, chat")
///
/// Unknown-key policy: THROW (fail-fast prevents silent corrupt state in M2+).
/// YAML subset supported: flat block-style key:value only (no nested mappings).
/// </summary>
public sealed class SkillParser
{
    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "action", "tool", "skill", "chat"
    };

    private static readonly HashSet<string> KnownFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "type", "description"
    };

    /// <summary>
    /// Parse <paramref name="yaml"/> and return a validated <see cref="SkillDescriptor"/>.
    /// Throws <see cref="SkillParseException"/> on any validation failure.
    /// </summary>
    public SkillDescriptor Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            throw new SkillParseException("Empty YAML");

        var fields = ParseFlatYaml(yaml);

        if (fields.Count == 0)
            throw new SkillParseException("Empty YAML");

        // Rule 2: reject unknown keys before validating required ones.
        foreach (var key in fields.Keys)
        {
            if (!KnownFields.Contains(key))
                throw new SkillParseException($"Unknown field: {key}");
        }

        // Rule 3: name
        if (!fields.TryGetValue("name", out var name) || name is null)
            throw new SkillParseException("Missing required field: name");

        // Rules 4 + 5: description
        if (fields.TryGetValue("description", out var description))
        {
            if (description is null)
                throw new SkillParseException("Description cannot be null");
        }
        else
        {
            throw new SkillParseException("Missing required field: description");
        }

        // Rule 6: type
        if (!fields.TryGetValue("type", out var type) || type is null)
            throw new SkillParseException("Missing required field: type");

        // Rule 7: type value
        if (!ValidTypes.Contains(type))
            throw new SkillParseException(
                $"Invalid type: {type}. Valid types: action, tool, skill, chat");

        return new SkillDescriptor
        {
            Name        = name,
            Type        = type.ToLowerInvariant(),
            Description = description
        };
    }

    // ── Flat YAML parser ──────────────────────────────────────────────────────
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
