using System.CommandLine;
using System.CommandLine.Invocation;
using Hermes.Core.Profiles;

namespace Hermes.Cli.Commands;

/// <summary>
/// hermes profile create "My Profile" [--description "..."]
/// hermes profile list
/// hermes profile switch &lt;name-or-id&gt;
/// hermes profile current
/// </summary>
public static class ProfileCommand
{
    public static Command Build(IProfileService profileService)
    {
        var cmd = new Command("profile", "Manage Hermes profiles");
        cmd.Add(BuildCreate(profileService));
        cmd.Add(BuildList(profileService));
        cmd.Add(BuildSwitch(profileService));
        cmd.Add(BuildCurrent(profileService));
        return cmd;
    }

    // ── hermes profile create ────────────────────────────────────────────────

    private static Command BuildCreate(IProfileService profileService)
    {
        var nameArg = new Argument<string>("name") { Description = "Profile name (must be unique)" };
        var descOption = new Option<string?>("--description", new[] { "-d" });
        descOption.Description = "Optional profile description";

        var cmd = new Command("create", "Create a new profile");
        cmd.Add(nameArg);
        cmd.Add(descOption);
        cmd.Action = new CreateAction(nameArg, descOption, profileService);
        return cmd;
    }

    private sealed class CreateAction : AsynchronousCommandLineAction
    {
        private readonly Argument<string> _name;
        private readonly Option<string?> _desc;
        private readonly IProfileService _svc;

        public CreateAction(Argument<string> name, Option<string?> desc, IProfileService svc)
        {
            _name = name; _desc = desc; _svc = svc;
        }

        public override async Task<int> InvokeAsync(ParseResult pr, CancellationToken ct = default)
        {
            var profile = await _svc.CreateProfileAsync(pr.GetValue(_name)!, pr.GetValue(_desc), ct);
            Console.WriteLine($"Created profile '{profile.Name}' ({profile.Id})");
            return 0;
        }
    }

    // ── hermes profile list ──────────────────────────────────────────────────

    private static Command BuildList(IProfileService profileService)
    {
        var cmd = new Command("list", "List all profiles");
        cmd.Action = new ListAction(profileService);
        return cmd;
    }

    private sealed class ListAction : AsynchronousCommandLineAction
    {
        private readonly IProfileService _svc;
        public ListAction(IProfileService svc) => _svc = svc;

        public override async Task<int> InvokeAsync(ParseResult pr, CancellationToken ct = default)
        {
            var current = await _svc.GetCurrentProfileAsync(ct);
            await foreach (var p in _svc.ListProfilesAsync(ct))
            {
                var marker = current?.Id == p.Id ? " *" : "";
                Console.WriteLine($"{p.Id}  {p.Name}{marker}");
                if (!string.IsNullOrWhiteSpace(p.Description))
                    Console.WriteLine($"    {p.Description}");
            }
            return 0;
        }
    }

    // ── hermes profile switch ────────────────────────────────────────────────

    private static Command BuildSwitch(IProfileService profileService)
    {
        var nameArg = new Argument<string>("name") { Description = "Profile name or ID to switch to" };
        var cmd = new Command("switch", "Set the active profile");
        cmd.Add(nameArg);
        cmd.Action = new SwitchAction(nameArg, profileService);
        return cmd;
    }

    private sealed class SwitchAction : AsynchronousCommandLineAction
    {
        private readonly Argument<string> _name;
        private readonly IProfileService _svc;
        public SwitchAction(Argument<string> name, IProfileService svc) { _name = name; _svc = svc; }

        public override async Task<int> InvokeAsync(ParseResult pr, CancellationToken ct = default)
        {
            var nameOrId = pr.GetValue(_name)!;
            var profile = await _svc.GetProfileAsync(nameOrId, ct)
                       ?? await _svc.GetProfileByNameAsync(nameOrId, ct);

            if (profile is null)
            {
                Console.Error.WriteLine($"No profile found for '{nameOrId}'.");
                return 1;
            }

            await _svc.SwitchProfileAsync(profile.Id, ct);
            Console.WriteLine($"Switched to profile '{profile.Name}' ({profile.Id})");
            return 0;
        }
    }

    // ── hermes profile current ───────────────────────────────────────────────

    private static Command BuildCurrent(IProfileService profileService)
    {
        var cmd = new Command("current", "Show the active profile");
        cmd.Action = new CurrentAction(profileService);
        return cmd;
    }

    private sealed class CurrentAction : AsynchronousCommandLineAction
    {
        private readonly IProfileService _svc;
        public CurrentAction(IProfileService svc) => _svc = svc;

        public override async Task<int> InvokeAsync(ParseResult pr, CancellationToken ct = default)
        {
            var profile = await _svc.GetCurrentProfileAsync(ct);
            if (profile is null)
            {
                Console.WriteLine("No active profile. Use: hermes profile switch <name>");
                return 0;
            }
            Console.WriteLine($"{profile.Id}  {profile.Name}");
            return 0;
        }
    }
}
