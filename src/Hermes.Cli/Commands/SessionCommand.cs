using System.CommandLine;
using System.CommandLine.Invocation;
using Hermes.Core.Profiles;

namespace Hermes.Cli.Commands;

/// <summary>
/// hermes session create "Chat Session" [--profile &lt;profileId&gt;]
/// hermes session list [--profile &lt;profileId&gt;]
/// hermes session switch &lt;id&gt;
/// hermes session current
/// </summary>
public static class SessionCommand
{
    public static Command Build(IProfileService profileService, ISessionService sessionService)
    {
        var cmd = new Command("session", "Manage Hermes sessions");
        cmd.Add(BuildCreate(profileService, sessionService));
        cmd.Add(BuildList(profileService, sessionService));
        cmd.Add(BuildSwitch(sessionService));
        cmd.Add(BuildCurrent(sessionService));
        return cmd;
    }

    // ── hermes session create ────────────────────────────────────────────────

    private static Command BuildCreate(IProfileService profileService, ISessionService sessionService)
    {
        var nameArg = new Argument<string>("name") { Description = "Session name" };
        var profileOpt = new Option<string?>("--profile", new[] { "-p" });
        profileOpt.Description = "Profile ID to create session under (defaults to current profile)";

        var cmd = new Command("create", "Create a new session under the current profile");
        cmd.Add(nameArg);
        cmd.Add(profileOpt);
        cmd.Action = new CreateAction(nameArg, profileOpt, profileService, sessionService);
        return cmd;
    }

    private sealed class CreateAction : AsynchronousCommandLineAction
    {
        private readonly Argument<string> _name;
        private readonly Option<string?> _profileOpt;
        private readonly IProfileService _profileSvc;
        private readonly ISessionService _sessionSvc;

        public CreateAction(Argument<string> name, Option<string?> profileOpt,
            IProfileService profileSvc, ISessionService sessionSvc)
        {
            _name = name; _profileOpt = profileOpt;
            _profileSvc = profileSvc; _sessionSvc = sessionSvc;
        }

        public override async Task<int> InvokeAsync(ParseResult pr, CancellationToken ct = default)
        {
            var profileId = pr.GetValue(_profileOpt);
            if (profileId is null)
            {
                var current = await _profileSvc.GetCurrentProfileAsync(ct);
                if (current is null)
                {
                    Console.Error.WriteLine("No active profile. Use: hermes profile switch <name>");
                    return 1;
                }
                profileId = current.Id;
            }

            var session = await _sessionSvc.CreateSessionAsync(profileId, pr.GetValue(_name)!, ct);
            Console.WriteLine($"Created session '{session.Name}' ({session.Id}) under profile {session.ProfileId}");
            return 0;
        }
    }

    // ── hermes session list ──────────────────────────────────────────────────

    private static Command BuildList(IProfileService profileService, ISessionService sessionService)
    {
        var profileOpt = new Option<string?>("--profile", new[] { "-p" });
        profileOpt.Description = "Profile ID to list sessions for (defaults to current profile)";

        var cmd = new Command("list", "List sessions for the current profile");
        cmd.Add(profileOpt);
        cmd.Action = new ListAction(profileOpt, profileService, sessionService);
        return cmd;
    }

    private sealed class ListAction : AsynchronousCommandLineAction
    {
        private readonly Option<string?> _profileOpt;
        private readonly IProfileService _profileSvc;
        private readonly ISessionService _sessionSvc;

        public ListAction(Option<string?> profileOpt, IProfileService profileSvc, ISessionService sessionSvc)
        {
            _profileOpt = profileOpt; _profileSvc = profileSvc; _sessionSvc = sessionSvc;
        }

        public override async Task<int> InvokeAsync(ParseResult pr, CancellationToken ct = default)
        {
            var profileId = pr.GetValue(_profileOpt);
            if (profileId is null)
            {
                var current = await _profileSvc.GetCurrentProfileAsync(ct);
                if (current is null)
                {
                    Console.Error.WriteLine("No active profile. Use: hermes profile switch <name>");
                    return 1;
                }
                profileId = current.Id;
            }

            var currentSession = await _sessionSvc.GetCurrentSessionAsync(ct);
            await foreach (var s in _sessionSvc.ListSessionsByProfileAsync(profileId, ct))
            {
                var marker = currentSession?.Id == s.Id ? " *" : "";
                Console.WriteLine($"{s.Id}  {s.Name}{marker}  (accessed: {s.LastAccessed:u})");
            }
            return 0;
        }
    }

    // ── hermes session switch ────────────────────────────────────────────────

    private static Command BuildSwitch(ISessionService sessionService)
    {
        var idArg = new Argument<string>("id") { Description = "Session ID to switch to" };
        var cmd = new Command("switch", "Set the active session");
        cmd.Add(idArg);
        cmd.Action = new SwitchAction(idArg, sessionService);
        return cmd;
    }

    private sealed class SwitchAction : AsynchronousCommandLineAction
    {
        private readonly Argument<string> _id;
        private readonly ISessionService _svc;
        public SwitchAction(Argument<string> id, ISessionService svc) { _id = id; _svc = svc; }

        public override async Task<int> InvokeAsync(ParseResult pr, CancellationToken ct = default)
        {
            var id = pr.GetValue(_id)!;
            await _svc.SwitchSessionAsync(id, ct);
            var session = await _svc.GetSessionAsync(id, ct);
            Console.WriteLine($"Switched to session '{session?.Name}' ({id})");
            return 0;
        }
    }

    // ── hermes session current ───────────────────────────────────────────────

    private static Command BuildCurrent(ISessionService sessionService)
    {
        var cmd = new Command("current", "Show the active session");
        cmd.Action = new CurrentAction(sessionService);
        return cmd;
    }

    private sealed class CurrentAction : AsynchronousCommandLineAction
    {
        private readonly ISessionService _svc;
        public CurrentAction(ISessionService svc) => _svc = svc;

        public override async Task<int> InvokeAsync(ParseResult pr, CancellationToken ct = default)
        {
            var session = await _svc.GetCurrentSessionAsync(ct);
            if (session is null)
            {
                Console.WriteLine("No active session. Use: hermes session switch <id>");
                return 0;
            }
            Console.WriteLine($"{session.Id}  {session.Name}  (profile: {session.ProfileId})");
            return 0;
        }
    }
}
