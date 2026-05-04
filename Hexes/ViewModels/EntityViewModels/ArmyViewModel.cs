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

public enum BrigadeSortMode
{
    ManualOrder,
    Name,
    UnitType,
    Number
}

/// <summary>
/// ViewModel wrapper for Army entity with Brigades management and auto-save.
/// </summary>
public partial class ArmyViewModel : ObservableObject, IEntityViewModel, IPathSelectableViewModel
{
    private readonly Army _army;
    private readonly IArmyService _service;
    private readonly IPathfindingService? _pathfindingService;
    private readonly int _mapRows;
    private readonly int _mapCols;
    private readonly IFactionRuleService? _factionRuleService;
    private double _wagonCarryCapacity;

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
        get => HexCoordinateHelper.GetCol(CoordinateQ, CoordinateR);
        set
        {
            if (value == null) { CoordinateQ = null; CoordinateR = null; return; }
            var result = HexCoordinateHelper.SetCol(value, Row, _mapCols, _mapRows);
            if (result == null) return;
            CoordinateQ = result.Value.q; CoordinateR = result.Value.r;
            OnPropertyChanged();
        }
    }

    public int? Row
    {
        get => HexCoordinateHelper.GetRow(CoordinateQ, CoordinateR);
        set
        {
            if (value == null) { CoordinateQ = null; CoordinateR = null; return; }
            var result = HexCoordinateHelper.SetRow(value, Col, _mapCols, _mapRows);
            if (result == null) return;
            CoordinateQ = result.Value.q; CoordinateR = result.Value.r;
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
        get => HexCoordinateHelper.GetCol(TargetCoordinateQ, TargetCoordinateR);
        set
        {
            if (value == null) { TargetCoordinateQ = null; TargetCoordinateR = null; return; }
            var result = HexCoordinateHelper.SetCol(value, TargetRow, _mapCols, _mapRows);
            if (result == null) return;
            TargetCoordinateQ = result.Value.q; TargetCoordinateR = result.Value.r;
            OnPropertyChanged();
        }
    }

    public int? TargetRow
    {
        get => HexCoordinateHelper.GetRow(TargetCoordinateQ, TargetCoordinateR);
        set
        {
            if (value == null) { TargetCoordinateQ = null; TargetCoordinateR = null; return; }
            var result = HexCoordinateHelper.SetRow(value, TargetCol, _mapCols, _mapRows);
            if (result == null) return;
            TargetCoordinateQ = result.Value.q; TargetCoordinateR = result.Value.r;
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
    /// Event raised when user requests a scouting report for this army.
    /// HexMapViewModel subscribes to this to render and send via Discord.
    /// </summary>
    public event Func<Army, Task>? ScoutingReportRequested;

    /// <summary>
    /// Event raised when user requests an army status embed be sent to Discord.
    /// </summary>
    public event Func<Army, Task>? ArmyReportRequested;

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
    private async Task SendScoutingReport()
    {
        if (ScoutingReportRequested != null)
            await ScoutingReportRequested.Invoke(_army);
    }

    [RelayCommand]
    private async Task SendArmyReport()
    {
        if (ArmyReportRequested != null)
            await ArmyReportRequested.Invoke(_army);
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
        set { if (_army.Faction != value) { _army.Faction = value; _army.FactionId = value?.Id ?? 1; RefreshWagonCarryCapacity(); OnPropertyChanged(); OnPropertyChanged(nameof(MaxCarry)); _ = SaveAsync(); } }
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
        set { if (_army.Wagons != value) { _army.Wagons = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaxCarry)); _ = SaveAsync(); } }
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
            int baseCapacity = (Brigades?.Sum(b => b.Number * b.UnitType.CarryCapacityPerMan()) ?? 0)
                + (UnitType.Infantry.CarryCapacityPerMan() * NonCombatants)
                + (int)(_wagonCarryCapacity * _army.Wagons);
            if (_army.IsSiegeEnginesLoaded)
                baseCapacity -= _army.SiegeEngines * 1000;
            return baseCapacity;
        }
    }

    public int DailySupplyConsumption => _army.DailySupplyConsumption;

    public double DaysOfSupply => _army.DaysOfSupply;

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

    public bool IsNightMarching
    {
        get => _army.IsNightMarching;
        set { if (_army.IsNightMarching != value) { _army.IsNightMarching = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public bool IsForcedMarch
    {
        get => _army.IsForcedMarch;
        set { if (_army.IsForcedMarch != value) { _army.IsForcedMarch = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int SiegeEngines
    {
        get => _army.SiegeEngines;
        set { if (_army.SiegeEngines != value) { _army.SiegeEngines = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaxCarry)); _ = SaveAsync(); } }
    }

    public bool IsSiegeEnginesLoaded
    {
        get => _army.IsSiegeEnginesLoaded;
        set { if (_army.IsSiegeEnginesLoaded != value) { _army.IsSiegeEnginesLoaded = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaxCarry)); _ = SaveAsync(); } }
    }

    public double ForcedMarchDays
    {
        get => _army.ForcedMarchHours / 24.0;
        set { var hours = (int)(value * 24); if (_army.ForcedMarchHours != hours) { _army.ForcedMarchHours = hours; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int MarchingColumnLength => _army.MarchingColumnLength;
    public int CombatStrength => Brigades.Sum(b => b.Number * b.UnitType.CombatPowerPerMan());

    /// <summary>
    /// Observable collection of brigades for UI binding.
    /// </summary>
    public ObservableCollection<Brigade> Brigades { get; }

    public ObservableCollection<Brigade> BrigadesView { get; } = new();

    public IReadOnlyList<string> BrigadeUnitTypeFilterOptions { get; } =
        new[] { "All" }.Concat(Enum.GetNames<UnitType>()).ToList();

    public IReadOnlyList<BrigadeSortMode> BrigadeSortModes { get; } =
        Enum.GetValues<BrigadeSortMode>().ToList();

    private string _brigadeSearchText = string.Empty;
    public string BrigadeSearchText
    {
        get => _brigadeSearchText;
        set
        {
            if (SetProperty(ref _brigadeSearchText, value ?? string.Empty))
                RebuildBrigadesView();
        }
    }

    private string _selectedBrigadeUnitTypeFilter = "All";
    public string SelectedBrigadeUnitTypeFilter
    {
        get => _selectedBrigadeUnitTypeFilter;
        set
        {
            if (SetProperty(ref _selectedBrigadeUnitTypeFilter, value ?? "All"))
                RebuildBrigadesView();
        }
    }

    private BrigadeSortMode _brigadeSortMode = BrigadeSortMode.ManualOrder;
    public BrigadeSortMode BrigadeSortMode
    {
        get => _brigadeSortMode;
        set
        {
            if (SetProperty(ref _brigadeSortMode, value))
                RebuildBrigadesView();
        }
    }

    private bool _brigadeSortDescending;
    public bool BrigadeSortDescending
    {
        get => _brigadeSortDescending;
        set
        {
            if (SetProperty(ref _brigadeSortDescending, value))
                RebuildBrigadesView();
        }
    }

    public bool HasNoDisplayedBrigades => BrigadesView.Count == 0;

    public event Action? Saved;

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
        RebuildBrigadesView();
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
            Number = 1000
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
        SortBrigadesByManualOrder();
        RebuildBrigadesView();
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
        RebuildBrigadesView();
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
        RenumberLocalBrigades();
        RebuildBrigadesView();
        NotifyComputedPropertiesChanged();
    }

    [RelayCommand(CanExecute = nameof(CanMoveBrigadeUp))]
    private async Task MoveBrigadeUpAsync(Brigade? brigade)
    {
        await MoveBrigadeAsync(brigade, -1);
    }

    [RelayCommand(CanExecute = nameof(CanMoveBrigadeDown))]
    private async Task MoveBrigadeDownAsync(Brigade? brigade)
    {
        await MoveBrigadeAsync(brigade, 1);
    }

    /// <summary>
    /// Saves the army to the database. Exposed as a command for child views to call.
    /// </summary>
    public IAsyncRelayCommand SaveCommand { get; }

    private bool CanMoveBrigadeUp(Brigade? brigade)
    {
        return BrigadeSortMode == BrigadeSortMode.ManualOrder
            && !BrigadeSortDescending
            && brigade != null
            && Brigades.IndexOf(brigade) > 0;
    }

    private bool CanMoveBrigadeDown(Brigade? brigade)
    {
        return BrigadeSortMode == BrigadeSortMode.ManualOrder
            && !BrigadeSortDescending
            && brigade != null
            && Brigades.IndexOf(brigade) >= 0
            && Brigades.IndexOf(brigade) < Brigades.Count - 1;
    }

    private async Task MoveBrigadeAsync(Brigade? brigade, int direction)
    {
        if (brigade == null || BrigadeSortMode != BrigadeSortMode.ManualOrder || BrigadeSortDescending)
            return;

        var oldIndex = Brigades.IndexOf(brigade);
        var newIndex = oldIndex + direction;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= Brigades.Count)
            return;

        Brigades.Move(oldIndex, newIndex);
        RenumberLocalBrigades();
        RebuildBrigadesView();

        await _service.UpdateBrigadeOrderAsync(_army.Id, Brigades.Select(b => b.Id).ToList());
        Saved?.Invoke();
    }

    private void SortBrigadesByManualOrder()
    {
        var sorted = Brigades.OrderBy(b => b.SortOrder).ThenBy(b => b.Id).ToList();
        Brigades.Clear();
        foreach (var brigade in sorted)
            Brigades.Add(brigade);
    }

    private void RenumberLocalBrigades()
    {
        for (var i = 0; i < Brigades.Count; i++)
            Brigades[i].SortOrder = i;
    }

    private void RebuildBrigadesView()
    {
        IEnumerable<Brigade> query = Brigades;

        if (!string.IsNullOrWhiteSpace(BrigadeSearchText))
        {
            query = query.Where(b => b.Name.Contains(BrigadeSearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (Enum.TryParse<UnitType>(SelectedBrigadeUnitTypeFilter, out var unitType))
        {
            query = query.Where(b => b.UnitType == unitType);
        }

        query = BrigadeSortMode switch
        {
            BrigadeSortMode.Name => BrigadeSortDescending
                ? query.OrderByDescending(b => b.Name).ThenBy(b => b.SortOrder).ThenBy(b => b.Id)
                : query.OrderBy(b => b.Name).ThenBy(b => b.SortOrder).ThenBy(b => b.Id),
            BrigadeSortMode.UnitType => BrigadeSortDescending
                ? query.OrderByDescending(b => b.UnitType).ThenBy(b => b.SortOrder).ThenBy(b => b.Id)
                : query.OrderBy(b => b.UnitType).ThenBy(b => b.SortOrder).ThenBy(b => b.Id),
            BrigadeSortMode.Number => BrigadeSortDescending
                ? query.OrderByDescending(b => b.Number).ThenBy(b => b.SortOrder).ThenBy(b => b.Id)
                : query.OrderBy(b => b.Number).ThenBy(b => b.SortOrder).ThenBy(b => b.Id),
            _ => BrigadeSortDescending
                ? query.OrderByDescending(b => b.SortOrder).ThenByDescending(b => b.Id)
                : query.OrderBy(b => b.SortOrder).ThenBy(b => b.Id)
        };

        BrigadesView.Clear();
        foreach (var brigade in query)
            BrigadesView.Add(brigade);

        OnPropertyChanged(nameof(HasNoDisplayedBrigades));
        MoveBrigadeUpCommand.NotifyCanExecuteChanged();
        MoveBrigadeDownCommand.NotifyCanExecuteChanged();
    }

    private async Task SaveAsync()
    {
        await _service.UpdateAsync(_army);
        SortBrigadesByManualOrder();
        RebuildBrigadesView();
        NotifyComputedPropertiesChanged();
        Saved?.Invoke();
    }

    private void RefreshWagonCarryCapacity()
    {
        _wagonCarryCapacity = _factionRuleService?.GetCachedRuleValue(
            _army.FactionId, FactionRuleKeys.WagonCarryCapacity,
            GameRules.Current.Supply.WagonCarryCapacity)
            ?? GameRules.Current.Supply.WagonCarryCapacity;
    }

    private void NotifyComputedPropertiesChanged()
      {
        OnPropertyChanged(nameof(CombatStrength));
        OnPropertyChanged(nameof(NonCombatantsPercentage));
        OnPropertyChanged(nameof(CurrentTotalCarry));
        OnPropertyChanged(nameof(MaxCarry));
        OnPropertyChanged(nameof(DailySupplyConsumption));
        OnPropertyChanged(nameof(DaysOfSupply));
        OnPropertyChanged(nameof(MarchingColumnLength));
      }

    public ArmyViewModel(Army army, IArmyService service, IEnumerable<Commander> availableCommanders, IEnumerable<Faction> availableFactions, int mapRows = int.MaxValue, int mapCols = int.MaxValue, IPathfindingService? pathfindingService = null, IFactionRuleService? factionRuleService = null)
    {
        _army = army;
        _service = service;
        _availableCommanders = availableCommanders;
        _availableFactions = availableFactions;
        _mapRows = mapRows;
        _mapCols = mapCols;
        _pathfindingService = pathfindingService;
        _factionRuleService = factionRuleService;
        RefreshWagonCarryCapacity();
        Brigades = new ObservableCollection<Brigade>(_army.Brigades.OrderBy(b => b.SortOrder).ThenBy(b => b.Id));
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        RebuildBrigadesView();
    }
}
