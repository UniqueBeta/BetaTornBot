using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

var client = new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds   // slash commands need nothing more
});
var interactions = new InteractionService(client);

var services = new ServiceCollection()
    .AddSingleton(client)
    .AddSingleton(interactions)
    .BuildServiceProvider();

client.Log += msg => { Console.WriteLine(msg); return Task.CompletedTask; };
interactions.Log += msg => { Console.WriteLine(msg); return Task.CompletedTask; };

client.Ready += async () =>
{
    await interactions.AddModulesAsync(Assembly.GetEntryAssembly(), services);

#if DEBUG
    await interactions.RegisterCommandsToGuildAsync(ulong.Parse(Environment.GetEnvironmentVariable("DEV_GUILD_ID")!));
#else
    await interactions.RegisterCommandsGloballyAsync();
#endif
};

client.InteractionCreated += async interaction =>
{
    var ctx = new SocketInteractionContext(client, interaction);
    await interactions.ExecuteCommandAsync(ctx, services);
};

await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DISCORD_TOKEN"));
await client.StartAsync();
await Task.Delay(Timeout.Infinite);