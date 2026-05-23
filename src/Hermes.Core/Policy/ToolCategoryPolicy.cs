using System.Text.RegularExpressions;
using Hermes.Core.Tools;

namespace Hermes.Core.Policy;

/// <summary>
/// Evaluates tool requests against the category allowlist and scans inputs for
/// sensitive data patterns that must be redacted before execution.
///
/// <para>Safe categories (allow + optionally redact): ReadFile, SystemInfo, TextProcessing.</para>
/// <para>Denied categories: ExecuteCommand, Network, WriteFile, Delete, Unknown.</para>
/// </summary>
public sealed class ToolCategoryPolicy
{
    // ── Category sets ─────────────────────────────────────────────────────────

    private static readonly IReadOnlySet<ToolCategory> SafeCategories =
        new HashSet<ToolCategory>
        {
            ToolCategory.ReadFile,
            ToolCategory.SystemInfo,
            ToolCategory.TextProcessing,
        };

    private static readonly IReadOnlySet<ToolCategory> DeniedCategories =
        new HashSet<ToolCategory>
        {
            ToolCategory.ExecuteCommand,
            ToolCategory.Network,
            ToolCategory.WriteFile,
            ToolCategory.Delete,
            ToolCategory.Unknown,
        };

    // ── Redaction patterns ────────────────────────────────────────────────────
    // Each pattern is replaced with its [REDACTED:<type>] placeholder.

    private static readonly (Regex Pattern, string Label)[] RedactionRules =
    [
        // Bearer / Authorization tokens
        (new Regex(@"(?i)bearer\s+[A-Za-z0-9\-_\.~\+\/]+=*",
            RegexOptions.Compiled), "TOKEN"),

        // API keys (key=<value>, apikey=<value>, api_key=<value>)
        (new Regex(@"(?i)(api[_-]?key|apitoken|access[_-]?token)\s*[=:]\s*[^\s&'""\]},;]+",
            RegexOptions.Compiled), "API_KEY"),

        // Connection strings containing Password=
        (new Regex(@"(?i)password\s*[=:]\s*[^\s&'""\]},;]+",
            RegexOptions.Compiled), "PASSWORD"),

        // Secret / private key markers
        (new Regex(@"(?i)(secret|private[_-]?key)\s*[=:]\s*[^\s&'""\]},;]+",
            RegexOptions.Compiled), "SECRET"),

        // AWS access/secret key patterns
        (new Regex(@"(?:AKIA|AIPA|AROA|ASIA)[A-Z0-9]{16}",
            RegexOptions.Compiled), "AWS_KEY"),

        // Generic long hex tokens (32+ chars — likely session or auth tokens)
        (new Regex(@"\b[0-9a-fA-F]{32,}\b",
            RegexOptions.Compiled), "HEX_TOKEN"),
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    public PolicyResult Evaluate(ToolCategory category, string input)
    {
        if (DeniedCategories.Contains(category))
            return PolicyResult.Deny(
                $"Tool category '{category}' is not permitted",
                new Dictionary<string, string> { ["denied_category"] = category.ToString() });

        if (!SafeCategories.Contains(category))
            return PolicyResult.Deny(
                $"Unknown tool category '{category}' — not in safe allowlist",
                new Dictionary<string, string> { ["unknown_category"] = category.ToString() });

        // Safe category — check for sensitive data in input
        var (redacted, labels) = RedactSensitiveData(input);

        if (labels.Count > 0)
            return PolicyResult.Redact(
                $"Input contained sensitive data patterns: {string.Join(", ", labels)}",
                redacted,
                new Dictionary<string, string>
                {
                    ["redacted_patterns"] = string.Join(",", labels),
                    ["category"]          = category.ToString(),
                });

        return PolicyResult.Allow(
            $"Tool category '{category}' is permitted",
            new Dictionary<string, string> { ["category"] = category.ToString() });
    }

    // ── Internal redaction engine ─────────────────────────────────────────────

    public static (string Redacted, IReadOnlyList<string> MatchedLabels) RedactSensitiveData(string input)
    {
        if (string.IsNullOrEmpty(input))
            return (input, []);

        var result = input;
        var matched = new List<string>();

        foreach (var (pattern, label) in RedactionRules)
        {
            var replaced = pattern.Replace(result, $"[REDACTED:{label}]");
            if (!ReferenceEquals(replaced, result) && replaced != result)
                matched.Add(label);
            result = replaced;
        }

        return (result, matched);
    }
}
