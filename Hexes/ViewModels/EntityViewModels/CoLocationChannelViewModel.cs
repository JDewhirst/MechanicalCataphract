using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GUI.ViewModels;
using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Discord;
using MechanicalCataphract.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GUI.ViewModels.EntityViewModels;

public partial class CoLocationChannelViewModel : ObservableObject, IEntityViewModel
{
    private readonly CoLocationChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDiscordChannelManager? _discordChannelManager;

    private CancellationTokenSource? _discordDebounceCts;

    public string EntityTypeName => "Co-Location Channel";

    public event Action? Saved;

    public IEnumerable<Army> AvailableArmies { get; }
    public IEnumerable<Commander> AvailableCommanders { get; }

    public CoLocationChannel Entity => _channel;
    public int Id => _channel.Id;

    public ObservableCollection<Commander> Commanders { get; }

    public string Name
    {
        get => _channel.Name;
        set
        {
            if (_channel.Name != value)
            {
                _channel.Name = value;
                OnPropertyChanged();
                _ = SaveAsync();
                ScheduleDiscordChannelRename();
            }
        }
    }

    public Army? FollowingArmy
    {
        get => _channel.FollowingArmyId == null
            ? null
            : AvailableArmies.FirstOrDefault(a => a.Id == _channel.FollowingArmyId.Value) ?? _channel.FollowingArmy;
        set
        {
            if (_channel.FollowingArmyId == value?.Id && _channel.FollowingArmy == value) return;

            _channel.FollowingArmy = value;
            _channel.FollowingArmyId = value?.Id;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FollowingArmyId));

            // Mutual exclusivity: following an army clears hex
            if (value != null)
            {
                _channel.FollowingHexQ = null;
                _channel.FollowingHexR = null;
                _channel.FollowingHex = null;
                OnPropertyChanged(nameof(FollowingHexQ));
                OnPropertyChanged(nameof(FollowingHexR));
                OnPropertyChanged(nameof(FollowingCol));
                OnPropertyChanged(nameof(FollowingRow));
            }

            _ = SaveAsync();
        }
    }

    public int? FollowingArmyId
    {
        get => _channel.FollowingArmyId;
        set
        {
            if (_channel.FollowingArmyId != value)
            {
                _channel.FollowingArmyId = value;
                _channel.FollowingArmy = value == null
                    ? null
                    : AvailableArmies.FirstOrDefault(a => a.Id == value.Value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(FollowingArmy));

                // Mutual exclusivity: following an army clears hex
                if (value != null)
                {
                    _channel.FollowingHexQ = null;
                    _channel.FollowingHexR = null;
                    _channel.FollowingHex = null;
                    OnPropertyChanged(nameof(FollowingHexQ));
                    OnPropertyChanged(nameof(FollowingHexR));
                    OnPropertyChanged(nameof(FollowingCol));
                    OnPropertyChanged(nameof(FollowingRow));
                }

                _ = SaveAsync();
            }
        }
    }

    public int? FollowingHexQ
    {
        get => _channel.FollowingHexQ;
        set
        {
            if (_channel.FollowingHexQ != value)
            {
                _channel.FollowingHexQ = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FollowingCol));

                // Mutual exclusivity: setting hex clears army
                if (value != null)
                {
                    _channel.FollowingArmy = null;
                    _channel.FollowingArmyId = null;
                    OnPropertyChanged(nameof(FollowingArmyId));
                    OnPropertyChanged(nameof(FollowingArmy));
                }

                _ = SaveAsync();
            }
        }
    }

    public int? FollowingHexR
    {
        get => _channel.FollowingHexR;
        set
        {
            if (_channel.FollowingHexR != value)
            {
                _channel.FollowingHexR = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FollowingRow));

                // Mutual exclusivity: setting hex clears army
                if (value != null)
                {
                    _channel.FollowingArmy = null;
                    _channel.FollowingArmyId = null;
                    OnPropertyChanged(nameof(FollowingArmyId));
                    OnPropertyChanged(nameof(FollowingArmy));
                }

                _ = SaveAsync();
            }
        }
    }

    public int? FollowingCol
    {
        get => FollowingHexQ == null || FollowingHexR == null ? null
             : OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, new Hex(FollowingHexQ.Value, FollowingHexR.Value, -FollowingHexQ.Value - FollowingHexR.Value)).col;
        set
        {
            if (value == null) { FollowingHexQ = null; FollowingHexR = null; return; }
            int row = FollowingRow is int r && r >= 0 ? r : 0;
            var hex = OffsetCoord.QoffsetToCube(OffsetCoord.ODD, new OffsetCoord(value.Value, row));
            FollowingHexQ = hex.q; FollowingHexR = hex.r;
            OnPropertyChanged();
        }
    }

    public int? FollowingRow
    {
        get => FollowingHexQ == null || FollowingHexR == null ? null
             : OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, new Hex(FollowingHexQ.Value, FollowingHexR.Value, -FollowingHexQ.Value - FollowingHexR.Value)).row;
        set
        {
            if (value == null) { FollowingHexQ = null; FollowingHexR = null; return; }
            int col = FollowingCol is int c && c >= 0 ? c : 0;
            var hex = OffsetCoord.QoffsetToCube(OffsetCoord.ODD, new OffsetCoord(col, value.Value));
            FollowingHexQ = hex.q; FollowingHexR = hex.r;
            OnPropertyChanged();
        }
    }

    public int CommanderCount => Commanders.Count;

    [ObservableProperty]
    private Commander? _selectedCommanderToAdd;

    [RelayCommand]
    private async Task AddCommanderAsync()
    {
        if (SelectedCommanderToAdd == null) return;
        var commander = SelectedCommanderToAdd;

        await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<ICoLocationChannelService>().AddCommanderAsync(_channel.Id, commander.Id));

        if (!Commanders.Any(c => c.Id == commander.Id))
        {
            Commanders.Add(commander);
            OnPropertyChanged(nameof(CommanderCount));
        }

        if (_discordChannelManager != null)
            await _discordChannelManager.OnCommanderAddedToCoLocationAsync(_channel, commander);

        SelectedCommanderToAdd = null;
    }

    [RelayCommand]
    private async Task RemoveCommanderAsync(Commander commander)
    {
        if (commander == null) return;

        await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<ICoLocationChannelService>().RemoveCommanderAsync(_channel.Id, commander.Id));
        Commanders.Remove(commander);
        OnPropertyChanged(nameof(CommanderCount));

        if (_discordChannelManager != null)
            await _discordChannelManager.OnCommanderRemovedFromCoLocationAsync(_channel, commander);
    }

    [RelayCommand]
    private void ClearFollowingArmy()
    {
        FollowingArmy = null;
    }

    private void ScheduleDiscordChannelRename()
    {
        if (_discordChannelManager == null) return;
        if (!_channel.DiscordChannelId.HasValue) return;

        _discordDebounceCts?.Cancel();
        _discordDebounceCts = new CancellationTokenSource();
        var ct = _discordDebounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1500, ct);
                await _discordChannelManager.OnCoLocationChannelUpdatedAsync(_channel);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CoLocChannelVM] Discord rename failed: {ex.Message}");
            }
        });
    }

    private async Task SaveAsync()
    {
        await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<ICoLocationChannelService>().UpdateAsync(_channel));
        Saved?.Invoke();
    }

    public CoLocationChannelViewModel(
        CoLocationChannel channel,
        IServiceScopeFactory scopeFactory,
        IEnumerable<Army> availableArmies,
        IEnumerable<Commander> availableCommanders,
        IDiscordChannelManager? discordChannelManager = null)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        AvailableArmies = availableArmies;
        AvailableCommanders = availableCommanders;
        _discordChannelManager = discordChannelManager;
        Commanders = new ObservableCollection<Commander>(channel.Commanders ?? Enumerable.Empty<Commander>());
    }
}
