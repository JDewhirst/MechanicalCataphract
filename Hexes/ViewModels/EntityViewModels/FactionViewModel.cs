using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Discord;
using MechanicalCataphract.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace GUI.ViewModels.EntityViewModels;

/// <summary>
/// ViewModel wrapper for Faction entity with auto-save on property change.
/// </summary>
public partial class FactionViewModel : ObservableObject, IEntityViewModel
{
    private readonly Faction _faction;
    private readonly IFactionService _service;
    private readonly IDiscordChannelManager? _discordChannelManager;

    // Debounce Discord API calls — channel rename has a 2-per-10min rate limit
    private CancellationTokenSource? _discordDebounceCts;
    private string? _pendingOldName;
    private string? _pendingOldColor;

    public string EntityTypeName => "Faction";

    /// <summary>
    /// The underlying entity (for bindings that need direct access).
    /// </summary>
    public Faction Entity => _faction;

    public int Id => _faction.Id;

    public string Name
    {
        get => _faction.Name;
        set
        {
            if (_faction.Name != value)
            {
                // Capture the original name only on the first keystroke of this burst
                _pendingOldName ??= _faction.Name;
                _faction.Name = value;
                OnPropertyChanged();
                _ = SaveAsync();
                ScheduleDiscordUpdate();
            }
        }
    }

    public string ColorHex
    {
        get => _faction.ColorHex;
        set
        {
            if (_faction.ColorHex != value)
            {
                _pendingOldColor ??= _faction.ColorHex;
                _faction.ColorHex = value;
                OnPropertyChanged();
                _ = SaveAsync();
                ScheduleDiscordUpdate();
            }
        }
    }

    public string? Rules
    {
        get => _faction.Rules;
        set
        {
            if (_faction.Rules != value)
            {
                _faction.Rules = value;
                OnPropertyChanged();
                _ = SaveAsync();
            }
        }
    }

    public bool IsPlayerFaction
    {
        get => _faction.IsPlayerFaction;
        set
        {
            if (_faction.IsPlayerFaction != value)
            {
                _faction.IsPlayerFaction = value;
                OnPropertyChanged();
                _ = SaveAsync();
            }
        }
    }

    public ObservableCollection<Army> Armies { get; }
    public ObservableCollection<Commander> Commanders { get; }

    public event Action? Saved;
    public event Action<Army>? ArmySelected;
    public event Action<Commander>? CommanderSelected;

    // Commands for list item clicks
    [RelayCommand]
    private void SelectArmy(Army? army)
    {
        if (army != null) ArmySelected?.Invoke(army);
    }

    [RelayCommand]
    private void SelectCommander(Commander? commander)
    {
        if (commander != null) CommanderSelected?.Invoke(commander);
    }

    private async Task SaveAsync()
    {
        await _service.UpdateAsync(_faction);
        Saved?.Invoke();
    }

    /// <summary>
    /// Debounces Discord API calls. Each call cancels the previous pending update
    /// and starts a new 1.5-second timer. Only the final update fires.
    /// This avoids hitting Discord's 2-per-10min channel rename rate limit.
    /// </summary>
    private void ScheduleDiscordUpdate()
    {
        if (_discordChannelManager == null) return;

        _discordDebounceCts?.Cancel();
        _discordDebounceCts = new CancellationTokenSource();
        var ct = _discordDebounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1500, ct);

                var oldName = Interlocked.Exchange(ref _pendingOldName, null);
                var oldColor = Interlocked.Exchange(ref _pendingOldColor, null);

                if (oldName != null || oldColor != null)
                {
                    await _discordChannelManager.OnFactionUpdatedAsync(_faction, oldName, oldColor);
                }
            }
            catch (OperationCanceledException)
            {
                // Another keystroke arrived — this attempt was superseded
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FactionViewModel] Discord update failed: {ex.Message}");
            }
        });
    }

    public FactionViewModel(Faction faction, IFactionService service, IDiscordChannelManager? discordChannelManager = null)
    {
        _faction = faction;
        _service = service;
        _discordChannelManager = discordChannelManager;
        Armies = new ObservableCollection<Army>(_faction.Armies);
        Commanders = new ObservableCollection<Commander>(_faction.Commanders);
    }
}
