using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;
namespace BetaTornBot.Services;

public class DiscordBotService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly IServiceProvider _services;
    private readonly ILogger<DiscordBotService> _logger;

    private bool _commandsRegistered;

    public DiscordBotService(
        DiscordSocketClient client,
        InteractionService interactions,
        IServiceProvider services,
        ILogger<DiscordBotService> logger)
    {
        _client = client;
        _interactions = interactions;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _client.Log += msg =>
        {
            _logger.LogInformation("{Message}", msg);
            return Task.CompletedTask;
        };

        _interactions.Log += msg =>
        {
            _logger.LogInformation("{Message}", msg);
            return Task.CompletedTask;
        };


        // Load slash command modules
        await _interactions.AddModulesAsync(
            Assembly.GetEntryAssembly()!,
            _services);

        // Register slash commands when Discord is ready
        _client.Ready += async () =>
        {
            if (_commandsRegistered)
                return;

            var guildId = ulong.Parse(
                Environment.GetEnvironmentVariable("DEV_GUILD_ID")!);

            await _interactions.RegisterCommandsToGuildAsync(guildId);

            _commandsRegistered = true;
            _logger.LogInformation("Slash commands registered.");
        };

        // Handle slash commands
        _client.InteractionCreated += async interaction =>
        {
            var ctx = new SocketInteractionContext(
                _client,
                interaction);

            await _interactions.ExecuteCommandAsync(
                ctx,
                _services);
        };

        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN")
            ?? throw new InvalidOperationException(
                "DISCORD_TOKEN is missing.");

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        _logger.LogInformation("Discord bot started.");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    public override async Task StopAsync(
        CancellationToken cancellationToken)
    {
        await _client.StopAsync();
        await _client.LogoutAsync();

        await base.StopAsync(cancellationToken);
    }
}