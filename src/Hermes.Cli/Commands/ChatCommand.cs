using System.CommandLine;
using System.CommandLine.Invocation;
using Hermes.Core.Services;
using Hermes.Core.Session;

namespace Hermes.Cli.Commands;

/// <summary>
/// Handles the `hermes chat` sub-command.
/// Usage: hermes chat --profile &lt;name&gt; --message &lt;text&gt;
/// Prints the model response to stdout and persists the session to SQLite.
/// </summary>
public static class ChatCommand
{
    public static Command Build(IHermesChatService chatService, ISessionStore sessionStore)
    {
        var profileOption = new Option<string>("--profile", new[] { "-p" });
        profileOption.Description = "Profile name to use for this chat session";
        profileOption.Required = true;

        var messageOption = new Option<string>("--message", new[] { "-m" });
        messageOption.Description = "Message to send to the model";
        messageOption.Required = true;

        var command = new Command("chat", "Send a chat message and receive a model response");
        command.Add(profileOption);
        command.Add(messageOption);
        command.Action = new ChatAction(profileOption, messageOption, chatService, sessionStore);

        return command;
    }

    private sealed class ChatAction : AsynchronousCommandLineAction
    {
        private readonly Option<string> _profileOption;
        private readonly Option<string> _messageOption;
        private readonly IHermesChatService _chatService;
        private readonly ISessionStore _sessionStore;

        public ChatAction(
            Option<string> profileOption,
            Option<string> messageOption,
            IHermesChatService chatService,
            ISessionStore sessionStore)
        {
            _profileOption = profileOption;
            _messageOption = messageOption;
            _chatService = chatService;
            _sessionStore = sessionStore;
        }

        public override async Task<int> InvokeAsync(
            ParseResult parseResult,
            CancellationToken cancellationToken = default)
        {
            var profile = parseResult.GetValue(_profileOption)!;
            var message = parseResult.GetValue(_messageOption)!;

            var session = await _sessionStore.CreateAsync(profile, message, cancellationToken);
            var response = await _chatService.ChatAsync(message, cancellationToken);
            await _sessionStore.UpdateAsync(session.Id, response, cancellationToken);

            Console.WriteLine(response);
            Console.Error.WriteLine($"session-id: {session.Id}");
            return 0;
        }
    }
}
