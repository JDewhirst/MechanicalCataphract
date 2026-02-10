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
    private bool _isReady;
    private TaskCompletionSource? _readyTcs;
    private CancellationTokenSource? _connectCts;
    private string _statusMessage = "Not configured";

    public GatewayClient? Client => _client;
    public bool IsConnected => _isReady;
    public string StatusMessage => _statusMessage;

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
            _statusMessage = $"Auto-start failed: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[DiscordBot] Auto-start failed: {ex.Message}");
        }
    }

    public async Task StartBotAsync()
    {
        // Cancel any in-flight connection attempt before acquiring the lock.
        // This prevents queued Connect clicks from waiting behind a stale
        // 15-second Ready timeout — the old attempt aborts immediately.
        _connectCts?.Cancel();

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
                _statusMessage = "No token or guild ID configured.";
                System.Diagnostics.Debug.WriteLine("[DiscordBot] No token or guild ID configured.");
                return;
            }

            var token = new BotToken(config.BotToken);
            var guildId = config.GuildId.Value;

            // Fresh cancellation token for this connection attempt
            _connectCts = new CancellationTokenSource();
            var ct = _connectCts.Token;

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

            // Subscribe to Ready event before starting — this fires when the
            // gateway handshake completes and the bot is actually online.
            _readyTcs = new TaskCompletionSource();
            _client.Ready += readyEventArgs =>
            {
                _isReady = true;
                _statusMessage = "Connected";
                _readyTcs.TrySetResult();
                System.Diagnostics.Debug.WriteLine($"[DiscordBot] Ready! User: {readyEventArgs.User.Username}");
                return default;
            };

            _statusMessage = "Connecting...";
            await _client.StartAsync();

            // Wait up to 15 seconds for the gateway handshake to complete,
            // but also abort if a new connection attempt cancels us.
            var timeoutTask = Task.Delay(15000, ct);
            var winner = await Task.WhenAny(_readyTcs.Task, timeoutTask);

            ct.ThrowIfCancellationRequested();

            if (winner != _readyTcs.Task)
            {
                throw new TimeoutException("Gateway handshake timed out after 15 seconds");
            }

            // Now safe to register slash commands — bot is confirmed online
            try
            {
                await _commandService.CreateCommandsAsync(_client.Rest, _client.Id);
                System.Diagnostics.Debug.WriteLine($"[DiscordBot] Commands registered. Guild: {guildId}");
            }
            catch (Exception ex)
            {
                // Command registration failure is non-fatal — bot is still connected
                System.Diagnostics.Debug.WriteLine($"[DiscordBot] Command registration failed (non-fatal): {ex.Message}");
            }
        }
        catch (OperationCanceledException)
        {
            // A newer StartBotAsync call cancelled this one — clean up silently
            _statusMessage = "Reconnecting...";
            _isReady = false;
            _client?.Dispose();
            _client = null;
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error: {ex.Message}";
            _isReady = false;
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
        _connectCts?.Cancel();
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
                _isReady = false;
                _statusMessage = "Disconnected";
                System.Diagnostics.Debug.WriteLine("[DiscordBot] Disconnected.");
            }
        }
    }
}
