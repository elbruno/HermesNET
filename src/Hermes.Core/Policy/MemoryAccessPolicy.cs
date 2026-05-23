namespace Hermes.Core.Policy;

/// <summary>
/// Enforces R2 memory isolation: a profile may only access its own memory.
/// Cross-profile access and global scope are denied.
/// </summary>
public sealed class MemoryAccessPolicy
{
    public PolicyResult Evaluate(
        string requestingProfileId,
        string targetProfileId,
        MemoryScope scope)
    {
        if (string.IsNullOrWhiteSpace(requestingProfileId))
            return PolicyResult.Deny(
                "Memory access denied: requesting profile ID is null or empty",
                new Dictionary<string, string> { ["rule"] = "empty-profile-id" });

        if (string.IsNullOrWhiteSpace(targetProfileId))
            return PolicyResult.Deny(
                "Memory access denied: target profile ID is null or empty",
                new Dictionary<string, string> { ["rule"] = "empty-target-id" });

        // Global scope is always denied in M3
        if (scope == MemoryScope.Global)
            return PolicyResult.Deny(
                "Memory scope 'Global' is not permitted in M3 — R2 isolation requires profile-scoped access",
                new Dictionary<string, string>
                {
                    ["rule"]       = "global-scope-denied",
                    ["scope"]      = scope.ToString(),
                    ["requesting"] = requestingProfileId,
                });

        // Cross-profile access is denied for Profile and Session scopes
        if (!string.Equals(requestingProfileId, targetProfileId, StringComparison.OrdinalIgnoreCase))
            return PolicyResult.Deny(
                $"Memory access denied: profile '{requestingProfileId}' cannot access memory of profile '{targetProfileId}' (R2 isolation)",
                new Dictionary<string, string>
                {
                    ["rule"]       = "cross-profile-denied",
                    ["requesting"] = requestingProfileId,
                    ["target"]     = targetProfileId,
                    ["scope"]      = scope.ToString(),
                });

        return PolicyResult.Allow(
            $"Memory access allowed: profile '{requestingProfileId}' accessing own memory (scope={scope})",
            new Dictionary<string, string>
            {
                ["requesting"] = requestingProfileId,
                ["scope"]      = scope.ToString(),
            });
    }
}
