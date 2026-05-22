using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Hermes.Host;
using Hermes.Core.Services;
using Hermes.Core.Session;
using Hermes.Cli.Commands;

var builder = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

var configuration = builder.Build();

var connectionString = configuration["Database:ConnectionString"] ?? "Data Source=hermes.db";

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddSingleton(sp =>
    new ChatClientFactory(sp.GetRequiredService<IConfiguration>()).CreateClient());
services.AddScoped<IHermesChatService, HermesChatService>();
services.AddSingleton<ISessionStore>(_ => new SessionStore(connectionString));

var serviceProvider = services.BuildServiceProvider();

// Initialize session store (idempotent — safe on every startup)
var sessionStore = serviceProvider.GetRequiredService<ISessionStore>();
await sessionStore.InitializeAsync();

// Build CLI root command
var root = new RootCommand("Hermes — local AI runtime CLI");
root.Add(
    ChatCommand.Build(
        serviceProvider.GetRequiredService<IHermesChatService>(),
        sessionStore));

var parseResult = root.Parse(args, new ParserConfiguration());
return await parseResult.InvokeAsync();


