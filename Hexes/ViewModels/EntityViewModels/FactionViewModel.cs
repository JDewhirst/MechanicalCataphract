using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace GUI.ViewModels.EntityViewModels;

/// <summary>
/// ViewModel wrapper for Faction entity with auto-save on property change.
/// </summary>
public partial class FactionViewModel : ObservableObject, IEntityViewModel
{
    private readonly Faction _faction;
    private readonly IFactionService _service;

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
                _faction.Name = value;
                OnPropertyChanged();
                _ = SaveAsync();
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
                _faction.ColorHex = value;
                OnPropertyChanged();
                _ = SaveAsync();
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
    }

    // Update constructor to initialize collections
    public FactionViewModel(Faction faction, IFactionService service)
    {
        _faction = faction;
        _service = service;
        Armies = new ObservableCollection<Army>(_faction.Armies);
        Commanders = new ObservableCollection<Commander>(_faction.Commanders);
    }
}
