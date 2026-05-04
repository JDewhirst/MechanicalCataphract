using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GUI.ViewModels;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Discord;
using Microsoft.Extensions.DependencyInjection;

namespace GUI.ViewModels.HexMap;

public partial class DiscordConnectionViewModel : ObservableObject
{
    private readonly IDiscordBotService _discordBotService;
    private readonly IDiscordChannelManager _discordChannelManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Func<Task> _refreshAfterConnectAsync;

    [ObservableProperty]
    private string _botToken = string.Empty;

    [ObservableProperty]
    private string _guildId = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _statusMessage = "Not configured";

    public DiscordConnectionViewModel(
        IDiscordBotService discordBotService,
        IDiscordChannelManager discordChannelManager,
        IServiceScopeFactory scopeFactory,
        Func<Task> refreshAfterConnectAsync)
    {
        _discordBotService = discordBotService;
        _discordChannelManager = discordChannelManager;
        _scopeFactory = scopeFactory;
        _refreshAfterConnectAsync = refreshAfterConnectAsync;
    }

    public async Task LoadAsync()
    {
        try
        {
            var config = await _scopeFactory.InScopeAsync(async sp =>
            {
                var db = sp.GetRequiredService<WargameDbContext>();
                return await db.DiscordConfigs.FindAsync(1);
            });
            if (config is not null)
            {
                BotToken = config.BotToken ?? string.Empty;
                GuildId = config.GuildId?.ToString() ?? string.Empty;
            }

            IsConnected = _discordBotService.IsConnected;
            StatusMessage = _discordBotService.StatusMessage;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ConnectAsync()
    {
        try
        {
            StatusMessage = "Connecting...";

            if (!ulong.TryParse(GuildId, out var parsedGuildId))
            {
                StatusMessage = "Invalid Guild ID";
                return;
            }

            await _scopeFactory.InScopeAsync(async sp =>
            {
                var db = sp.GetRequiredService<WargameDbContext>();
                var config = await db.DiscordConfigs.FindAsync(1);
                if (config is null)
                {
                    config = new DiscordConfig { Id = 1 };
                    db.DiscordConfigs.Add(config);
                }

                config.BotToken = BotToken;
                config.GuildId = parsedGuildId;
                await db.SaveChangesAsync();
            });

            await _discordBotService.StartBotAsync();
            IsConnected = _discordBotService.IsConnected;
            StatusMessage = _discordBotService.StatusMessage;

            if (IsConnected)
            {
                await _discordChannelManager.SyncExistingEntitiesAsync();
                await _refreshAfterConnectAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsConnected = false;
        }
    }

    [RelayCommand]
    public async Task DisconnectAsync()
    {
        try
        {
            await _discordBotService.StopBotAsync();
            IsConnected = false;
            StatusMessage = "Disconnected";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
}
