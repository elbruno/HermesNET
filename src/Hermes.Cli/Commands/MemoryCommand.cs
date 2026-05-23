using System.CommandLine;
using System.CommandLine.Invocation;
using Hermes.Core.Memory;
using Hermes.Core.Profiles;

namespace Hermes.Cli.Commands;

/// <summary>
/// hermes memory show [--profile &lt;profileId&gt;]
/// hermes memory update --profile &lt;profileId&gt; --content "..."
/// hermes memory profile show [--profile &lt;profileId&gt;]
/// </summary>
public static class MemoryCommand
{
    public static Command Build(
        CuratedMemoryLoader loader,
        MemoryUpdateHandler handler,
        IProfileService profileService)
    {
        var cmd = new Command("memory", "Manage curated profile memory (MEMORY.md / USER.md)");
        cmd.Add(BuildShow(loader, profileService));
        cmd.Add(BuildUpdate(handler, profileService));
        cmd.Add(BuildProfileShow(loader, profileService));
        return cmd;
    }

    // ── hermes memory show [--profile <id>] ────────────────────────────────

    private static Command BuildShow(CuratedMemoryLoader loader, IProfileService profileService)
    {
        var profileOpt = new Option<string?>("--profile", new[] { "-p" });
        profileOpt.Description = "Profile ID to show memory for (defaults to current profile)";

        var cmd = new Command("show", "Display MEMORY.md content for a profile");
        cmd.Add(profileOpt);
        cmd.Action = new ShowAction(profileOpt, loader, profileService);
        return cmd;
    }

    private sealed class ShowAction : AsynchronousCommandLineAction
    {
        private readonly Option<string?> _profileOpt;
        private readonly CuratedMemoryLoader _loader;
        private readonly IProfileService _profileSvc;

        public ShowAction(Option<string?> profileOpt, CuratedMemoryLoader loader, IProfileService profileSvc)
        {
            _profileOpt = profileOpt;
            _loader = loader;
            _profileSvc = profileSvc;
        }

        public override async Task<int> InvokeAsync(ParseResult pr, CancellationToken ct = default)
        {
            var profileId = await ResolveProfileId(pr.GetValue(_profileOpt), _profileSvc, ct);
            if (profileId is null) return 1;

            try
            {
                var memory = await _loader.LoadMemoryAsync(profileId, ct);
                Console.WriteLine(memory.ToDisplaySummary());
                if (!memory.IsEmpty)
                {
                    Console.WriteLine();
                    Console.WriteLine(memory.Content);
                }
                return 0;
            }
            catch (KeyNotFoundException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
    }

    // ── hermes memory update --profile <id> --content "..." ────────────────

    private static Command BuildUpdate(MemoryUpdateHandler handler, IProfileService profileService)
    {
        var profileOpt = new Option<string?>("--profile", new[] { "-p" });
        profileOpt.Description = "Profile ID to update memory for (defaults to current profile)";

        var contentOpt = new Option<string>("--content", new[] { "-c" }) { Required = true };
        contentOpt.Description = "New MEMORY.md content (Markdown)";

        var cmd = new Command("update", "Replace MEMORY.md content for a profile");
        cmd.Add(profileOpt);
        cmd.Add(contentOpt);
        cmd.Action = new UpdateAction(profileOpt, contentOpt, handler, profileService);
        return cmd;
    }

    private sealed class UpdateAction : AsynchronousCommandLineAction
    {
        private readonly Option<string?> _profileOpt;
        private readonly Option<string> _contentOpt;
        private readonly MemoryUpdateHandler _handler;
        private readonly IProfileService _profileSvc;

        public UpdateAction(Option<string?> profileOpt, Option<string> contentOpt,
            MemoryUpdateHandler handler, IProfileService profileSvc)
        {
            _profileOpt = profileOpt;
            _contentOpt = contentOpt;
            _handler = handler;
            _profileSvc = profileSvc;
        }

        public override async Task<int> InvokeAsync(ParseResult pr, CancellationToken ct = default)
        {
            var profileId = await ResolveProfileId(pr.GetValue(_profileOpt), _profileSvc, ct);
            if (profileId is null) return 1;

            try
            {
                var content = pr.GetValue(_contentOpt)!;
                await _handler.UpdateMemoryAsync(profileId, content, ct);
                Console.WriteLine($"Memory updated for profile '{profileId}'.");
                return 0;
            }
            catch (KeyNotFoundException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
            catch (MemoryParseException ex)
            {
                Console.Error.WriteLine($"Invalid content: {ex.Message}");
                return 1;
            }
        }
    }

    // ── hermes memory profile show [--profile <id>] ────────────────────────

    private static Command BuildProfileShow(CuratedMemoryLoader loader, IProfileService profileService)
    {
        var profileOpt = new Option<string?>("--profile", new[] { "-p" });
        profileOpt.Description = "Profile ID to show USER.md for (defaults to current profile)";

        var cmd = new Command("profile-show", "Display USER.md content for a profile");
        cmd.Add(profileOpt);
        cmd.Action = new ProfileShowAction(profileOpt, loader, profileService);
        return cmd;
    }

    private sealed class ProfileShowAction : AsynchronousCommandLineAction
    {
        private readonly Option<string?> _profileOpt;
        private readonly CuratedMemoryLoader _loader;
        private readonly IProfileService _profileSvc;

        public ProfileShowAction(Option<string?> profileOpt, CuratedMemoryLoader loader, IProfileService profileSvc)
        {
            _profileOpt = profileOpt;
            _loader = loader;
            _profileSvc = profileSvc;
        }

        public override async Task<int> InvokeAsync(ParseResult pr, CancellationToken ct = default)
        {
            var profileId = await ResolveProfileId(pr.GetValue(_profileOpt), _profileSvc, ct);
            if (profileId is null) return 1;

            try
            {
                var profile = await _loader.LoadUserProfileAsync(profileId, ct);
                Console.WriteLine($"[Profile: {profileId}] USER.md — v{profile.SchemaVersion}");
                Console.WriteLine();
                Console.WriteLine(profile.Data);
                return 0;
            }
            catch (KeyNotFoundException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
    }

    // ── Shared helpers ─────────────────────────────────────────────────────

    private static async Task<string?> ResolveProfileId(
        string? explicitId,
        IProfileService profileSvc,
        CancellationToken ct)
    {
        if (explicitId is not null) return explicitId;

        var current = await profileSvc.GetCurrentProfileAsync(ct);
        if (current is null)
        {
            Console.Error.WriteLine("No active profile. Use: hermes profile switch <name>");
            return null;
        }
        return current.Id;
    }
}
