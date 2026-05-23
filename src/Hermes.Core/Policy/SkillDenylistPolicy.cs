namespace Hermes.Core.Policy;

/// <summary>
/// Loads and evaluates skill denylist rules from <c>config/policies/skill-denylist.yaml</c>.
///
/// <para>YAML format (flat key-value + list sections):</para>
/// <code>
/// # Skill IDs to deny (exact match, case-insensitive)
/// denied_ids:
///   - system-shell-execute
///   - network-fetch-raw
///
/// # Deny skills whose Metadata["tags"] contains any of these (case-insensitive)
/// denied_tags:
///   - dangerous
///   - privileged
///
/// # Deny skills whose Metadata["author"] matches any of these (case-insensitive)
/// denied_authors:
///   - untrusted-author
/// </code>
/// </summary>
public sealed class SkillDenylistPolicy
{
    private readonly HashSet<string> _deniedIds;
    private readonly HashSet<string> _deniedTags;
    private readonly HashSet<string> _deniedAuthors;

    private SkillDenylistPolicy(
        IEnumerable<string> deniedIds,
        IEnumerable<string> deniedTags,
        IEnumerable<string> deniedAuthors)
    {
        _deniedIds     = new HashSet<string>(deniedIds,     StringComparer.OrdinalIgnoreCase);
        _deniedTags    = new HashSet<string>(deniedTags,    StringComparer.OrdinalIgnoreCase);
        _deniedAuthors = new HashSet<string>(deniedAuthors, StringComparer.OrdinalIgnoreCase);
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a denylist from the given YAML file path.
    /// Returns an empty (permissive) policy if the file does not exist.
    /// </summary>
    public static SkillDenylistPolicy LoadFromFile(string yamlPath)
    {
        if (!File.Exists(yamlPath))
            return new SkillDenylistPolicy([], [], []);

        var yaml = File.ReadAllText(yamlPath);
        return Parse(yaml);
    }

    /// <summary>Parses a denylist from a YAML string (for testing).</summary>
    public static SkillDenylistPolicy Parse(string yaml)
    {
        var deniedIds     = new List<string>();
        var deniedTags    = new List<string>();
        var deniedAuthors = new List<string>();

        List<string>? currentList = null;

        foreach (var rawLine in yaml.Split('\n'))
        {
            var line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            // Section headers
            if (line.Equals("denied_ids:", StringComparison.OrdinalIgnoreCase))
            {
                currentList = deniedIds;
                continue;
            }
            if (line.Equals("denied_tags:", StringComparison.OrdinalIgnoreCase))
            {
                currentList = deniedTags;
                continue;
            }
            if (line.Equals("denied_authors:", StringComparison.OrdinalIgnoreCase))
            {
                currentList = deniedAuthors;
                continue;
            }

            // List items (- value)
            if (line.StartsWith("- ") && currentList is not null)
            {
                var value = line[2..].Trim().Trim('"', '\'');
                if (!string.IsNullOrWhiteSpace(value))
                    currentList.Add(value);
            }
        }

        return new SkillDenylistPolicy(deniedIds, deniedTags, deniedAuthors);
    }

    // ── Exposed for tests ─────────────────────────────────────────────────────

    /// <summary>IDs explicitly denied by the denylist.</summary>
    public IReadOnlySet<string> DeniedIds     => _deniedIds;

    /// <summary>Tags that trigger denial when present in skill metadata.</summary>
    public IReadOnlySet<string> DeniedTags    => _deniedTags;

    /// <summary>Authors that trigger denial when present in skill metadata.</summary>
    public IReadOnlySet<string> DeniedAuthors => _deniedAuthors;

    // ── Evaluation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates the skill against denylist rules.
    /// Returns the first Deny match, or Allow if no rule fires.
    /// </summary>
    public PolicyResult Evaluate(Skills.ISkillDefinition skill)
    {
        var effectiveId = skill.Id ?? skill.Name;

        // 1 — ID denylist
        if (_deniedIds.Contains(effectiveId))
            return PolicyResult.Deny(
                $"Skill '{effectiveId}' is on the denylist",
                new Dictionary<string, string> { ["matched_rule"] = $"skill-id:{effectiveId}" });

        // 2 — Tag denylist
        if (skill.Metadata is not null
            && skill.Metadata.TryGetValue("tags", out var tagsRaw)
            && !string.IsNullOrWhiteSpace(tagsRaw))
        {
            foreach (var tag in tagsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (_deniedTags.Contains(tag))
                    return PolicyResult.Deny(
                        $"Skill '{effectiveId}' has denied tag '{tag}'",
                        new Dictionary<string, string>
                        {
                            ["matched_rule"] = $"tag:{tag}",
                            ["skill_id"]     = effectiveId,
                        });
            }
        }

        // 3 — Author denylist
        if (skill.Metadata is not null
            && skill.Metadata.TryGetValue("author", out var author)
            && !string.IsNullOrWhiteSpace(author)
            && _deniedAuthors.Contains(author))
        {
            return PolicyResult.Deny(
                $"Skill '{effectiveId}' is authored by denied author '{author}'",
                new Dictionary<string, string>
                {
                    ["matched_rule"] = $"author:{author}",
                    ["skill_id"]     = effectiveId,
                });
        }

        return PolicyResult.Allow($"Skill '{effectiveId}' is not on the denylist");
    }
}
