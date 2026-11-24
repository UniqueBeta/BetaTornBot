using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using BetaTornBot.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(new DiscordSocketClient(
    new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.Guilds
    }));

builder.Services.AddSingleton(sp =>
    new InteractionService(
        sp.GetRequiredService<DiscordSocketClient>()));

builder.Services.AddHostedService<DiscordBotService>();

var host = builder.Build();

await host.RunAsync();