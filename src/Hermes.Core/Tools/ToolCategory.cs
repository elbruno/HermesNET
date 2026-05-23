namespace Hermes.Core.Tools;

/// <summary>
/// Defines the category of a registered tool.
/// Safe categories are allowed in M2; all others are denied by default.
/// </summary>
public enum ToolCategory
{
    // ── M2 safe (read-only) categories ────────────────────────────────────────
    ReadFile,
    SystemInfo,
    TextProcessing,

    // ── M2 denied categories (registered for clarity; never executed in M2) ──
    WriteFile,
    ExecuteCommand,
    Network,
    Delete,

    // ── Fallback ───────────────────────────────────────────────────────────────
    Unknown
}
