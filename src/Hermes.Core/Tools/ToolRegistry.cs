using System.Collections.Concurrent;
using System.Text;

namespace Hermes.Core.Tools;

/// <summary>
/// Thread-safe, in-memory implementation of <see cref="IToolRegistry"/>.
///
/// <list type="bullet">
///   <item>Tools are keyed by name (case-insensitive).</item>
///   <item>Registration is idempotent for the same name — a second call throws
///         <see cref="ArgumentException"/>.</item>
///   <item>Only <see cref="SafeCategories"/> are allowed at invocation time;
///         all others are denied by default.</item>
///   <item>Every <see cref="ValidateToolInvocation"/> call appends to
///         <see cref="AuditLog"/> for the M3 policy engine.</item>
/// </list>
/// </summary>
public sealed class ToolRegistry : IToolRegistry
{
    // ── Category whitelist ─────────────────────────────────────────────────────

    /// <summary>M2 safe categories — tools in these categories may be invoked.</summary>
    public static readonly IReadOnlySet<ToolCategory> SafeCategories =
        new HashSet<ToolCategory>
        {
            ToolCategory.ReadFile,
            ToolCategory.SystemInfo,
            ToolCategory.TextProcessing,
        };

    // ── Path-traversal tokens rejected for file-path parameters ───────────────

    private static readonly string[] PathTraversalTokens =
    [
        "..",
        "%2e%2e",
        "%2E%2E",
        "%2e.",
        ".%2e",
        "%252e%252e",
    ];

    // ── Internal state ─────────────────────────────────────────────────────────

    private readonly ConcurrentDictionary<string, ToolDefinition> _tools
        = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<ToolAuditEntry> _auditLog = [];
    private readonly object _auditLock = new();

    // ── IToolRegistry ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public IReadOnlyList<ToolAuditEntry> AuditLog
    {
        get { lock (_auditLock) { return [.. _auditLog]; } }
    }

    /// <inheritdoc/>
    public Task RegisterToolAsync(ToolDefinition toolDefinition)
    {
        ArgumentNullException.ThrowIfNull(toolDefinition);

        if (!_tools.TryAdd(toolDefinition.Name, toolDefinition))
            throw new ArgumentException(
                $"A tool named '{toolDefinition.Name}' is already registered.", nameof(toolDefinition));

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ToolDefinition> GetToolAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_tools.TryGetValue(name, out var tool))
            return Task.FromResult(tool);

        throw new KeyNotFoundException($"Tool '{name}' not found in registry.");
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ToolDefinition> ListToolsByCategory(ToolCategory category)
    {
        foreach (var tool in _tools.Values.Where(t => t.Category == category))
            yield return tool;

        await Task.CompletedTask.ConfigureAwait(false); // satisfy async state machine
    }

    /// <inheritdoc/>
    public ToolInvocationValidationResult ValidateToolInvocation(
        string name,
        IReadOnlyDictionary<string, string> args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(args);

        var errors   = new List<string>();
        var warnings = new List<string>();

        // ── 1. Tool must be registered ─────────────────────────────────────────
        if (!_tools.TryGetValue(name, out var tool))
        {
            errors.Add($"Tool '{name}' is not registered.");
            Audit(name, "unknown", allowed: false, errors, args);
            return new ToolInvocationValidationResult(errors, warnings);
        }

        var categoryName = tool.Category.ToString();

        // ── 2. Category must be in the safe whitelist ──────────────────────────
        if (!SafeCategories.Contains(tool.Category))
        {
            errors.Add(
                $"Tool '{name}' belongs to category '{categoryName}' which is not " +
                "permitted in M2. Allowed categories: " +
                string.Join(", ", SafeCategories));
        }

        // ── 3. Required parameters must be present ─────────────────────────────
        foreach (var param in tool.Parameters.Where(p => p.Required))
        {
            if (!args.ContainsKey(param.Name))
                errors.Add($"Required parameter '{param.Name}' is missing.");
        }

        // ── 4. File-path parameters: path-traversal check + prefix whitelist ───
        foreach (var param in tool.Parameters.Where(p => p.IsFilePath))
        {
            if (!args.TryGetValue(param.Name, out var pathValue))
                continue; // already caught by required-param check above if required

            if (ContainsPathTraversal(pathValue))
            {
                errors.Add(
                    $"Parameter '{param.Name}' contains a path-traversal sequence " +
                    $"and was rejected.");
            }
            else if (param.AllowedPathPrefixes.Count > 0 &&
                     !param.AllowedPathPrefixes.Any(prefix =>
                         pathValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add(
                    $"Parameter '{param.Name}' value '{pathValue}' is not under any " +
                    "allowed path prefix: " +
                    string.Join(", ", param.AllowedPathPrefixes));
            }
        }

        // ── 5. Input size enforcement ──────────────────────────────────────────
        var argsPayload     = SerialiseArgs(args);
        var argsByteLength  = Encoding.UTF8.GetByteCount(argsPayload);

        if (argsByteLength > tool.MaxInputSize)
        {
            errors.Add(
                $"Input size {argsByteLength} bytes exceeds the tool's maxInputSize " +
                $"of {tool.MaxInputSize} bytes.");
        }
        else if (argsByteLength > tool.MaxInputSize * 0.9)
        {
            warnings.Add(
                $"Input size {argsByteLength} bytes is approaching the tool's " +
                $"maxInputSize of {tool.MaxInputSize} bytes (>90%).");
        }

        var allowed = errors.Count == 0;
        Audit(name, categoryName, allowed, errors, args, argsByteLength);

        return allowed
            ? new ToolInvocationValidationResult([], warnings)
            : new ToolInvocationValidationResult(errors, warnings);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static bool ContainsPathTraversal(string value)
    {
        // Normalise backslashes and URL-encoded variants before scanning.
        var normalised = value.Replace('\\', '/');
        return PathTraversalTokens.Any(token =>
            normalised.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string SerialiseArgs(IReadOnlyDictionary<string, string> args)
    {
        var sb = new StringBuilder();
        foreach (var (k, v) in args)
        {
            sb.Append(k);
            sb.Append('=');
            sb.Append(v);
            sb.Append(';');
        }
        return sb.ToString();
    }

    private void Audit(
        string toolName,
        string category,
        bool allowed,
        IReadOnlyList<string> denialReasons,
        IReadOnlyDictionary<string, string> args,
        int argsByteLength = 0)
    {
        var entry = new ToolAuditEntry
        {
            Timestamp      = DateTimeOffset.UtcNow,
            ToolName       = toolName,
            Category       = category,
            Allowed        = allowed,
            DenialReasons  = denialReasons,
            ArgsByteLength = argsByteLength,
        };

        lock (_auditLock)
        {
            _auditLog.Add(entry);
        }
    }
}
