using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Hermes.Host;
using Hermes.Core.Services;

var builder = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

var configuration = builder.Build();

var services = new ServiceCollection();
services.AddSingleton(configuration);
services.AddSingleton(sp => new ChatClientFactory(sp.GetRequiredService<IConfiguration>()).CreateClient());
services.AddScoped<IHermesChatService, HermesChatService>();

var serviceProvider = services.BuildServiceProvider();

if (args.Length == 0)
{
    Console.WriteLine("Usage: hermes chat <message>");
    Console.WriteLine("Example: hermes chat \"What is 2+2?\"");
    return 1;
}

if (args[0] == "chat" && args.Length > 1)
{
    var message = string.Join(" ", args.Skip(1));
    var chatService = serviceProvider.GetRequiredService<IHermesChatService>();
    try
    {
        var response = await chatService.ChatAsync(message);
        Console.WriteLine(response);
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
}

Console.WriteLine("Unknown command");
return 1;
