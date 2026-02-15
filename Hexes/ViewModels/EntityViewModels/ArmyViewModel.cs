using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace GUI.ViewModels.EntityViewModels;

/// <summary>
/// ViewModel wrapper for Army entity with Brigades management and auto-save.
/// </summary>
public partial class ArmyViewModel : ObservableObject, IEntityViewModel
{
    private readonly Army _army;
    private readonly IArmyService _service;
    private readonly IPathfindingService? _pathfindingService;
    private readonly int _mapRows;
    private readonly int _mapCols;

    public string EntityTypeName => "Army";

    /// <summary>
    /// The underlying entity (for bindings that need direct access).
    /// </summary>
    public Army Entity => _army;

    public int Id => _army.Id;

    private readonly IEnumerable<Commander> _availableCommanders;
    public IEnumerable<Commander> AvailableCommanders => _availableCommanders;

    private readonly IEnumerable<Faction> _availableFactions;
    public IEnumerable<Faction> AvailableFactions => _availableFactions;

    public string Name
    {
        get => _army.Name;
        set { if (_army.Name != value) { _army.Name = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int? CoordinateQ
    {
       get => _army.CoordinateQ;
       set { if (_army.CoordinateQ != value) { _army.CoordinateQ = value; OnPropertyChanged(); OnPropertyChanged(nameof(Col)); _ = SaveAsync(); }  }
    }
    public int? CoordinateR
    {
        get => _army.CoordinateR;
        set { if (_army.CoordinateR != value) { _army.CoordinateR = value; OnPropertyChanged(); OnPropertyChanged(nameof(Row)); _ = SaveAsync(); } }
    }

    public int? Col
    {
        get => CoordinateQ == null || CoordinateR == null ? null
             : OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, new Hex(CoordinateQ.Value, CoordinateR.Value, -CoordinateQ.Value - CoordinateR.Value)).col;
        set
        {
            if (value == null) { CoordinateQ = null; CoordinateR = null; return; }
            int row = Row ?? 0;
            if (!IsOffsetInBounds(value.Value, row)) return;
            var hex = OffsetCoord.QoffsetToCube(OffsetCoord.ODD, new OffsetCoord(value.Value, row));
            CoordinateQ = hex.q; CoordinateR = hex.r;
            OnPropertyChanged();
        }
    }

    public int? Row
    {
        get => CoordinateQ == null || CoordinateR == null ? null
             : OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, new Hex(CoordinateQ.Value, CoordinateR.Value, -CoordinateQ.Value - CoordinateR.Value)).row;
        set
        {
            if (value == null) { CoordinateQ = null; CoordinateR = null; return; }
            int col = Col ?? 0;
            if (!IsOffsetInBounds(col, value.Value)) return;
            var hex = OffsetCoord.QoffsetToCube(OffsetCoord.ODD, new OffsetCoord(col, value.Value));
            CoordinateQ = hex.q; CoordinateR = hex.r;
            OnPropertyChanged();
        }
    }

    public int? TargetCoordinateQ
    {
        get => _army.TargetCoordinateQ;
        set { if (_army.TargetCoordinateQ != value) { _army.TargetCoordinateQ = value; OnPropertyChanged(); OnPropertyChanged(nameof(TargetCol)); _ = SaveAsync(); } }
    }

    public int? TargetCoordinateR
    {
        get => _army.TargetCoordinateR;
        set { if (_army.TargetCoordinateR != value) { _army.TargetCoordinateR = value; OnPropertyChanged(); OnPropertyChanged(nameof(TargetRow)); _ = SaveAsync(); } }
    }

    public int? TargetCol
    {
        get => TargetCoordinateQ == null || TargetCoordinateR == null ? null
             : OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, new Hex(TargetCoordinateQ.Value, TargetCoordinateR.Value, -TargetCoordinateQ.Value - TargetCoordinateR.Value)).col;
        set
        {
            if (value == null) { TargetCoordinateQ = null; TargetCoordinateR = null; return; }
            int row = TargetRow ?? 0;
            if (!IsOffsetInBounds(value.Value, row)) return;
            var hex = OffsetCoord.QoffsetToCube(OffsetCoord.ODD, new OffsetCoord(value.Value, row));
            TargetCoordinateQ = hex.q; TargetCoordinateR = hex.r;
            OnPropertyChanged();
        }
    }

    public int? TargetRow
    {
        get => TargetCoordinateQ == null || TargetCoordinateR == null ? null
             : OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, new Hex(TargetCoordinateQ.Value, TargetCoordinateR.Value, -TargetCoordinateQ.Value - TargetCoordinateR.Value)).row;
        set
        {
            if (value == null) { TargetCoordinateQ = null; TargetCoordinateR = null; return; }
            int col = TargetCol ?? 0;
            if (!IsOffsetInBounds(col, value.Value)) return;
            var hex = OffsetCoord.QoffsetToCube(OffsetCoord.ODD, new OffsetCoord(col, value.Value));
            TargetCoordinateQ = hex.q; TargetCoordinateR = hex.r;
            OnPropertyChanged();
        }
    }

    public List<Hex>? Path
    {
        get => _army.Path;
        set { if (_army.Path != value) { _army.Path = value; OnPropertyChanged(); OnPropertyChanged(nameof(PathLength)); _ = SaveAsync(); } }
    }

    public int PathLength => _army.Path?.Count ?? 0;

    // Path selection mode state
    [ObservableProperty]
    private bool _isPathSelectionActive;

    [ObservableProperty]
    private int _pathSelectionCount;

    [ObservableProperty]
    private string? _pathComputeStatus;

    /// <summary>
    /// Event raised when user wants to select a path for this army.
    /// HexMapViewModel subscribes to this to enter path selection mode.
    /// </summary>
    public event Action<Army>? PathSelectionRequested;

    /// <summary>
    /// Event raised when user confirms path selection.
    /// </summary>
    public event Func<Task>? PathSelectionConfirmRequested;

    /// <summary>
    /// Event raised when user cancels path selection.
    /// </summary>
    public event Action? PathSelectionCancelRequested;

    [RelayCommand]
    private void SelectPath()
    {
        PathSelectionRequested?.Invoke(_army);
    }

    [RelayCommand]
    private async Task ConfirmPathSelection()
    {
        if (PathSelectionConfirmRequested != null)
            await PathSelectionConfirmRequested.Invoke();
    }

    [RelayCommand]
    private void CancelPathSelection()
    {
        PathSelectionCancelRequested?.Invoke();
    }

    [RelayCommand]
    private async Task ComputePath()
    {
        if (_pathfindingService == null)
        {
            PathComputeStatus = "Pathfinding not available";
            return;
        }

        if (CoordinateQ == null || CoordinateR == null)
        {
            PathComputeStatus = "Current location not set";
            return;
        }

        if (TargetCoordinateQ == null || TargetCoordinateR == null)
        {
            PathComputeStatus = "Target location not set";
            return;
        }

        PathComputeStatus = "Computing...";

        var start = new Hex(CoordinateQ.Value, CoordinateR.Value, -CoordinateQ.Value - CoordinateR.Value);
        var end = new Hex(TargetCoordinateQ.Value, TargetCoordinateR.Value, -TargetCoordinateQ.Value - TargetCoordinateR.Value);

        var result = await _pathfindingService.FindPathAsync(start, end, TravelEntityType.Army);

        if (result.Success)
        {
            Path = result.Path.ToList();
            PathComputeStatus = $"Path found: {result.Path.Count} hexes, cost {result.TotalCost}";
        }
        else
        {
            PathComputeStatus = result.FailureReason ?? "Path computation failed";
        }
    }

    public Faction? Faction
    {
        get => _army.Faction;
        set { if (_army.Faction != value) { _army.Faction = value; _army.FactionId = value?.Id ?? 1; OnPropertyChanged(); _ = SaveAsync(); } }
    }
    public Commander? Commander
    {
        get => _army.Commander;
        set { if (_army.Commander != value) 
                { 
                    _army.Commander = value;
                    _army.CommanderId = value?.Id;
                    OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int Morale
    {
        get => _army.Morale;
        set { if (_army.Morale != value) { _army.Morale = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int Wagons
    {
        get => _army.Wagons;
        set { if (_army.Wagons != value) { _army.Wagons = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int CarriedSupply
    {
        get => _army.CarriedSupply;
        set { if (_army.CarriedSupply != value) { _army.CarriedSupply = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int CarriedLoot
    {
        get => _army.CarriedLoot;
        set { if (_army.CarriedLoot != value) { _army.CarriedLoot = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int CurrentTotalCarry
    {
        get => CarriedLoot + CarriedSupply;
    }

    public int MaxCarry
    {
        get
        {
           return Brigades.Sum(b => b.Number * GetUnitTypeCarry(b.UnitType) ) + (GetUnitTypeCarry(UnitType.Infantry)*NonCombatants) + Wagons * 1000;
        }
    }
    private static int GetUnitTypeCarry(UnitType unitType) => unitType switch
    {
        UnitType.Infantry => 15,
        UnitType.Cavalry => 75,
        UnitType.Skirmishers => 15,
        _ => 0
    };

    public int DailySupplyConsumption
    {
        get 
        { 
             return _service.GetDailySupplyConsumptionAsync(Id).Result;
        }

    }

    public double DaysOfSupply => (double)CarriedSupply / (double)DailySupplyConsumption;

    public int CarriedCoins
    {
        get => _army.CarriedCoins;
        set { if (_army.CarriedCoins != value) { _army.CarriedCoins = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int NonCombatants
    {
        get => _army.NonCombatants;
        set { if (_army.NonCombatants != value) { _army.NonCombatants = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public double NonCombatantsPercentage
    {
        get
        {
            var totalTroops = Brigades.Sum(b => b.Number);
            return totalTroops > 0 ? (double)NonCombatants / totalTroops : 0;
        }
     }

    public double BaseNoncombatantsPercentage
    {
        get => _army.BaseNoncombatantsPercentage;
        set { if (_army.BaseNoncombatantsPercentage != value) { _army.BaseNoncombatantsPercentage = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public bool IsGarrison
    {
        get => _army.IsGarrison;
        set { if (_army.IsGarrison != value) { _army.IsGarrison = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public bool IsResting
    {
        get => _army.IsResting;
        set { if (_army.IsResting != value) { _army.IsResting = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }
    public int CombatStrength => Brigades.Sum(b => b.Number * GetUnitTypeCombatPower(b.UnitType));
    private static int GetUnitTypeCombatPower(UnitType unitType) => unitType switch
    {
        UnitType.Infantry => 1,
        UnitType.Cavalry => 2,
        UnitType.Skirmishers => 1,
        _ => 1
    };

    /// <summary>
    /// Observable collection of brigades for UI binding.
    /// </summary>
    public ObservableCollection<Brigade> Brigades { get; }

    /// <summary>
    /// Event raised when a brigade transfer is requested. The handler should show a dialog
    /// and return the target Army, or null if cancelled.
    /// </summary>
    public event Func<Brigade, Task<Army?>>? TransferRequested;

    [RelayCommand]
    private async Task EatSupplies()
    {
        System.Diagnostics.Debug.WriteLine($"EatSupplies called for Army {_army.Id} ({_army.Name}");
        CarriedSupply -= DailySupplyConsumption;
        await SaveAsync();
    }

    [RelayCommand]
    private async Task UndoEatSupplies()
    {
        System.Diagnostics.Debug.WriteLine($"UndoEatSupplies called for Army {_army.Id} ({_army.Name}");
        CarriedSupply += DailySupplyConsumption;
        await SaveAsync();
    }

    private double _casualtyPercentage = 0;
    public double CasualtyPercentage
    {
        get => _casualtyPercentage;
        set { if (_casualtyPercentage != value) { _casualtyPercentage = value; OnPropertyChanged(); } }
    }

    [RelayCommand]
    async Task ApplyCasualties()
    {
        System.Diagnostics.Debug.WriteLine($"ApplyCasualities called for {CasualtyPercentage}% for Army {_army.Id} ({_army.Name}");
        foreach (Brigade brigade in Brigades)
        {
            brigade.Number = (int)((double)brigade.Number * (1.0 - CasualtyPercentage));
        }
        // Force UI refresh by resetting the collection
        var updated = Brigades.ToList();
        Brigades.Clear();
        foreach (var b in updated)
            Brigades.Add(b);
        await SaveAsync();
    }

    [RelayCommand]
    async Task ArmyForageHex()
    {
        
        CarriedSupply += 1;
        await SaveAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task AddBrigadeAsync()
    {
        System.Diagnostics.Debug.WriteLine($"AddBrigadeAsync called for Army {_army.Id} ({_army.Name})");

        var brigade = new Brigade
        {
            Name = "New Brigade",
            ArmyId = _army.Id,
            FactionId = _army.FactionId,
            UnitType = UnitType.Infantry,
            Number = 1000,
            ScoutingRange = 1
        };

        System.Diagnostics.Debug.WriteLine($"Created brigade with ArmyId={brigade.ArmyId}, FactionId={brigade.FactionId}");

        try
        {
            // Use service to properly persist the new brigade
            await _service.AddBrigadeAsync(brigade);
            System.Diagnostics.Debug.WriteLine($"Brigade saved with Id={brigade.Id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR saving brigade: {ex.Message}");
            System.Diagnostics.Debug.WriteLine(ex.ToString());
            return;
        }

        // Update local collections for UI
        Brigades.Add(brigade);
        NotifyComputedPropertiesChanged();
        System.Diagnostics.Debug.WriteLine($"Brigade added to local collections. Total brigades: {Brigades.Count}");
    }

    [RelayCommand]
    private async Task TransferBrigadeAsync(Brigade? brigade)
    {
        if (brigade == null || TransferRequested == null) return;

        // Ask parent to show dialog and get target army
        var targetArmy = await TransferRequested.Invoke(brigade);
        if (targetArmy == null) return;  // User cancelled

        // Transfer via service (updates brigade.ArmyId and FactionId in database)
        await _service.TransferBrigadeAsync(brigade.Id, targetArmy.Id);

        // Remove from local collection (UI updates)
        _army.Brigades.Remove(brigade);
        Brigades.Remove(brigade);
        NotifyComputedPropertiesChanged();
    }

    [RelayCommand]
    private async Task DeleteBrigadeAsync(Brigade? brigade)
    {
        if (brigade == null) return;

        // Use service to properly delete from database
        await _service.DeleteBrigadeAsync(brigade.Id);

        // Update local collections for UI
        _army.Brigades.Remove(brigade);
        Brigades.Remove(brigade);
        NotifyComputedPropertiesChanged();
    }

    /// <summary>
    /// Saves the army to the database. Exposed as a command for child views to call.
    /// </summary>
    public IAsyncRelayCommand SaveCommand { get; }

    private async Task SaveAsync()
    {
        await _service.UpdateAsync(_army);
        NotifyComputedPropertiesChanged();
    }

    private void NotifyComputedPropertiesChanged()
      {
        OnPropertyChanged(nameof(CombatStrength));
        OnPropertyChanged(nameof(NonCombatantsPercentage));
        OnPropertyChanged(nameof(CurrentTotalCarry));
        OnPropertyChanged(nameof(MaxCarry));
        OnPropertyChanged(nameof(DailySupplyConsumption));
        OnPropertyChanged(nameof(DaysOfSupply));
      }

    private bool IsOffsetInBounds(int col, int row)
        => col >= 0 && col < _mapCols && row >= 0 && row < _mapRows;

    public ArmyViewModel(Army army, IArmyService service, IEnumerable<Commander> availableCommanders, IEnumerable<Faction> availableFactions, int mapRows = int.MaxValue, int mapCols = int.MaxValue, IPathfindingService? pathfindingService = null)
    {
        _army = army;
        _service = service;
        _availableCommanders = availableCommanders;
        _availableFactions = availableFactions;
        _mapRows = mapRows;
        _mapCols = mapCols;
        _pathfindingService = pathfindingService;
        Brigades = new ObservableCollection<Brigade>(_army.Brigades);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }
}
