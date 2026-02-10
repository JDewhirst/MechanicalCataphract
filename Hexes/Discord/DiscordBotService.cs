using System;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using MechanicalCataphract.Data;

namespace MechanicalCataphract.Discord;

public class DiscordBotService : BackgroundService, IDiscordBotService
{
    private readonly IServiceProvider _serviceProvider;
    private GatewayClient? _client;
    private ApplicationCommandService<ApplicationCommandContext>? _commandService;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public GatewayClient? Client => _client;
    public bool IsConnected => _client is not null;

    public DiscordBotService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The bot doesn't auto-start — it waits for StartBotAsync to be called
        // (triggered by UI or on startup if a token is already configured).
        // We just keep the BackgroundService alive until cancellation.
        await TryAutoStartAsync();

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        finally
        {
            await StopBotAsync();
        }
    }

    /// <summary>
    /// Attempts to start the bot if a token is already configured in the database.
    /// </summary>
    private async Task TryAutoStartAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
            var config = await db.DiscordConfigs.FindAsync(1);

            if (config is not null && !string.IsNullOrWhiteSpace(config.BotToken) && config.GuildId.HasValue)
            {
                await StartBotAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordBot] Auto-start failed: {ex.Message}");
        }
    }

    public async Task StartBotAsync()
    {
        await _lock.WaitAsync();
        try
        {
            // Stop existing client if running
            if (_client is not null)
            {
                await StopBotInternalAsync();
            }

            // Read config from DB
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
            var config = await db.DiscordConfigs.FindAsync(1);

            if (config is null || string.IsNullOrWhiteSpace(config.BotToken) || !config.GuildId.HasValue)
            {
                System.Diagnostics.Debug.WriteLine("[DiscordBot] No token or guild ID configured.");
                return;
            }

            var token = new BotToken(config.BotToken);
            var guildId = config.GuildId.Value;

            _client = new GatewayClient(token, new GatewayClientConfiguration
            {
                Intents = GatewayIntents.Guilds
                        | GatewayIntents.GuildMessages
                        | GatewayIntents.DirectMessages
                        | GatewayIntents.MessageContent,
            });

            // Set up slash commands
            _commandService = new ApplicationCommandService<ApplicationCommandContext>();
            _commandService.AddModules(Assembly.GetExecutingAssembly());

            _client.InteractionCreate += async interaction =>
            {
                if (interaction is ApplicationCommandInteraction appCmdInteraction)
                {
                    var context = new ApplicationCommandContext(appCmdInteraction, _client);
                    var result = await _commandService.ExecuteAsync(context);

                    if (result is IFailResult failResult)
                    {
                        await interaction.SendResponseAsync(
                            InteractionCallback.Message(failResult.Message));
                    }
                }
            };

            _client.Log += message =>
            {
                System.Diagnostics.Debug.WriteLine($"[DiscordBot] {message}");
                return default;
            };

            await _client.StartAsync();
            // Register slash commands with Discord (guild-scoped for fast updates)
            await _commandService.CreateCommandsAsync(_client.Rest, _client.Id);


            System.Diagnostics.Debug.WriteLine($"[DiscordBot] Connected. Guild: {guildId}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordBot] Start failed: {ex.Message}");
            _client?.Dispose();
            _client = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task StopBotAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await StopBotInternalAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task StopBotInternalAsync()
    {
        if (_client is not null)
        {
            try
            {
                await _client.CloseAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DiscordBot] Close error: {ex.Message}");
            }
            finally
            {
                _client.Dispose();
                _client = null;
                _commandService = null;
                System.Diagnostics.Debug.WriteLine("[DiscordBot] Disconnected.");
            }
        }
    }
}
