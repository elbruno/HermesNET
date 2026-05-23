using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Hermes.Host;
using Hermes.Core.Memory;
using Hermes.Core.Services;
using Hermes.Core.Session;
using Hermes.Core.Profiles;
using Hermes.Core.Skills;
using Hermes.Core.Configuration;
using Hermes.Cli.Commands;
using Hermes.Cli.Configuration;

var builder = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddHermesSettings(
        Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
        HermesSettingsPaths.GetDefaultUserConfigPath());

var configuration = builder.Build();

var connectionString = configuration["Database:ConnectionString"] ?? "Data Source=hermes.db";

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddSingleton(sp =>
    new ChatClientFactory(sp.GetRequiredService<IConfiguration>()).CreateClient());
services.AddScoped<IHermesChatService, HermesChatService>();
services.AddSingleton<ISessionStore>(_ => new SessionStore(connectionString));
services.AddSingleton<IProfileService>(_ => new ProfileService(connectionString));
services.AddSingleton<ISessionService>(sp =>
    new SessionService(connectionString, sp.GetRequiredService<IProfileService>()));
services.AddSingleton<ISkillRegistry, SkillRegistry>();
services.AddSingleton<SkillRegistryBootstrapper>();
services.AddSingleton<MemoryStore>(_ => new MemoryStore(connectionString));
services.AddSingleton<IMemoryService>(sp => sp.GetRequiredService<MemoryStore>());
services.AddSingleton<CuratedMemoryLoader>(sp =>
    new CuratedMemoryLoader(sp.GetRequiredService<IMemoryService>(), sp.GetRequiredService<IProfileService>()));
services.AddSingleton<MemoryUpdateHandler>(sp =>
    new MemoryUpdateHandler(sp.GetRequiredService<IMemoryService>(), sp.GetRequiredService<IProfileService>(), sp.GetRequiredService<CuratedMemoryLoader>()));


var serviceProvider = services.BuildServiceProvider();

// Initialize all stores (idempotent — safe on every startup)
var sessionStore = serviceProvider.GetRequiredService<ISessionStore>();
await sessionStore.InitializeAsync();

var profileService = serviceProvider.GetRequiredService<IProfileService>();
await profileService.InitializeAsync();

var sessionService = serviceProvider.GetRequiredService<ISessionService>();
await sessionService.InitializeAsync();

var memoryStore = serviceProvider.GetRequiredService<MemoryStore>();
await memoryStore.InitializeAsync();

// Bootstrap skill registry from config/skills/ directory
var skillBootstrapper = serviceProvider.GetRequiredService<SkillRegistryBootstrapper>();
await skillBootstrapper.BootstrapAsync();

// Build CLI root command
var root = new RootCommand("Hermes — local AI runtime CLI");
root.Add(
    ChatCommand.Build(
        serviceProvider.GetRequiredService<IHermesChatService>(),
        sessionStore));
root.Add(ProfileCommand.Build(profileService));
root.Add(SessionCommand.Build(profileService, sessionService));
root.Add(MemoryCommand.Build(
    serviceProvider.GetRequiredService<CuratedMemoryLoader>(),
    serviceProvider.GetRequiredService<MemoryUpdateHandler>(),
    profileService));
root.Add(SkillsCommand.Build(serviceProvider.GetRequiredService<ISkillRegistry>()));
root.Add(ConfigCommand.Build(configuration, new HermesCliConfigStore()));
root.Add(DoctorCommand.Build(configuration, new HermesCliConfigStore()));

var parseResult = root.Parse(args, new ParserConfiguration());
return await parseResult.InvokeAsync();
