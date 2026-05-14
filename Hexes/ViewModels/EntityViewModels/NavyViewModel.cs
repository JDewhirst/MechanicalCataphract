using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GUI.ViewModels;
using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace GUI.ViewModels.EntityViewModels;

/// <summary>
/// ViewModel wrapper for Navy entity with Ships management and auto-save.
/// </summary>
public partial class NavyViewModel : ObservableObject, IEntityViewModel, IPathSelectableViewModel
{
    private readonly Navy _navy;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly int _mapRows;
    private readonly int _mapCols;

    public string EntityTypeName => "Navy";
    public Navy Entity => _navy;
    public int Id => _navy.Id;

    private readonly IEnumerable<Commander> _availableCommanders;
    public IEnumerable<Commander> AvailableCommanders => _availableCommanders;

    private readonly IEnumerable<Army> _availableArmies;
    public IEnumerable<Army> AvailableArmies => _availableArmies;

    [ObservableProperty]
    private Army? _selectedEmbarkArmy;

    private readonly IEnumerable<Faction> _availableFactions;
    public IEnumerable<Faction> AvailableFactions => _availableFactions;

    public string Name
    {
        get => _navy.Name;
        set { if (_navy.Name != value) { _navy.Name = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int? CoordinateQ
    {
        get => _navy.CoordinateQ;
        set { if (_navy.CoordinateQ != value) { _navy.CoordinateQ = value; OnPropertyChanged(); OnPropertyChanged(nameof(Col)); _ = SaveAsync(); } }
    }

    public int? CoordinateR
    {
        get => _navy.CoordinateR;
        set { if (_navy.CoordinateR != value) { _navy.CoordinateR = value; OnPropertyChanged(); OnPropertyChanged(nameof(Row)); _ = SaveAsync(); } }
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

    public Commander? Commander
    {
        get => _navy.CommanderId == null
            ? null
            : AvailableCommanders.FirstOrDefault(c => c.Id == _navy.CommanderId.Value) ?? _navy.Commander;
        set
        {
            if (_navy.CommanderId == value?.Id && _navy.Commander == value) return;

            _navy.Commander = value;
            _navy.CommanderId = value?.Id;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CommanderId));
            _ = SaveAsync();
        }
    }

    public int? TargetCoordinateQ
    {
        get => _navy.TargetCoordinateQ;
        set { if (_navy.TargetCoordinateQ != value) { _navy.TargetCoordinateQ = value; OnPropertyChanged(); OnPropertyChanged(nameof(TargetCol)); _ = SaveAsync(); } }
    }

    public int? TargetCoordinateR
    {
        get => _navy.TargetCoordinateR;
        set { if (_navy.TargetCoordinateR != value) { _navy.TargetCoordinateR = value; OnPropertyChanged(); OnPropertyChanged(nameof(TargetRow)); _ = SaveAsync(); } }
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
        get => _navy.Path;
        set { if (_navy.Path != value) { _navy.Path = value; OnPropertyChanged(); OnPropertyChanged(nameof(PathLength)); _ = SaveAsync(); } }
    }

    public int PathLength => _navy.Path?.Count ?? 0;

    [ObservableProperty]
    private bool _isPathSelectionActive;

    [ObservableProperty]
    private int _pathSelectionCount;

    [ObservableProperty]
    private string? _pathComputeStatus;

    public int? CommanderId
    {
        get => _navy.CommanderId;
        set
        {
            if (_navy.CommanderId == value) return;

            _navy.CommanderId = value;
            _navy.Commander = value == null
                ? null
                : AvailableCommanders.FirstOrDefault(c => c.Id == value.Value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Commander));
            _ = SaveAsync();
        }
    }

    public Faction? Faction
    {
        get => AvailableFactions.FirstOrDefault(f => f.Id == _navy.FactionId) ?? _navy.Faction;
        set
        {
            var newFactionId = value?.Id ?? 1;
            if (_navy.FactionId == newFactionId && _navy.Faction == value) return;

            _navy.Faction = value;
            _navy.FactionId = newFactionId;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FactionId));
            _ = SaveAsync();
        }
    }

    public int? FactionId
    {
        get => _navy.FactionId;
        set
        {
            var newFactionId = value ?? 1;
            if (_navy.FactionId == newFactionId) return;

            _navy.FactionId = newFactionId;
            _navy.Faction = AvailableFactions.FirstOrDefault(f => f.Id == newFactionId);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Faction));
            _ = SaveAsync();
        }
    }

    public int CarriedSupply
    {
        get => _navy.CarriedSupply;
        set { if (_navy.CarriedSupply != value) { _navy.CarriedSupply = value; OnPropertyChanged(); NotifyComputedPropertiesChanged(); _ = SaveAsync(); } }
    }

    public bool IsRowing
    {
        get => _navy.IsRowing;
        set { if (_navy.IsRowing != value) { _navy.IsRowing = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    // Computed properties (delegate to entity)
    public int TransportCount  => _navy.TransportCount;
    public int WarshipCount    => _navy.WarshipCount;
    public int MaxCarryUnits  => _navy.MaxCarryUnits;
    public double TotalCarryUnits => _navy.TotalCarryUnits;
    public int DailySupplyConsumption => _navy.DailySupplyConsumption;
    public double DaysOfSupply => _navy.DaysOfSupply;

    public bool HasCarriedArmies => CarriedArmies.Count > 0;

    /// <summary>
    /// Observable collection of armies for UI binding.
    /// </summary>
    public ObservableCollection<Army> CarriedArmies { get; }
    public string CarriedArmyNames =>
        CarriedArmies.Count == 0
            ? "(Empty)"
            : string.Join(", ", CarriedArmies.Select(a => a.Name));

    /// <summary>
    /// Observable collection of ships for UI binding.
    /// </summary>
    public ObservableCollection<Ship> Ships { get; }

    public event Action? Saved;

    /// <summary>
    /// Event raised when user requests a navy status embed be sent to Discord.
    /// </summary>
    public event Func<Navy, Task>? NavyReportRequested;

    /// <summary>
    /// Event raised when user wants to select a path for this navy.
    /// HexMapViewModel subscribes to this to enter path selection mode.
    /// </summary>
    public event Action<Navy>? PathSelectionRequested;

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
        PathSelectionRequested?.Invoke(_navy);
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

        var result = await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<IPathfindingService>().FindPathAsync(start, end, TravelEntityType.Navy));

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

    [RelayCommand]
    private async Task SendNavyReport()
    {
        if (NavyReportRequested != null)
            await NavyReportRequested.Invoke(_navy);
    }


    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task AddShipAsync()
    {
        var ship = new Ship
        {
            NavyId = _navy.Id,
            ShipType = ShipType.Transport,
            Count = 1
        };

        await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<INavyService>().AddShipAsync(ship));
        Ships.Add(ship);
        _navy.Ships.Add(ship);
        NotifyComputedPropertiesChanged();
    }

    [RelayCommand]
    private async Task DeleteShipAsync(Ship? ship)
    {
        if (ship == null) return;
        await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<INavyService>().DeleteShipAsync(ship.Id));
        _navy.Ships.Remove(ship);
        Ships.Remove(ship);
        NotifyComputedPropertiesChanged();
    }

    [RelayCommand]
    private async Task EmbarkArmyAsync(Army? army)
    {
        if (army == null) return;
        var refreshed = await _scopeFactory.InScopeAsync(async sp =>
        {
            var service = sp.GetRequiredService<INavyService>();
            await service.EmbarkArmyAsync(_navy.Id, army.Id);
            return await service.GetNavyWithShipsAsync(_navy.Id);
        });

        // Reload so CarriedArmies navigation and brigade data are populated.
        if (refreshed != null)
        {
            _navy.CarriedArmies = refreshed.CarriedArmies;
            ReplaceCarriedArmies(refreshed.CarriedArmies);
        }

        SelectedEmbarkArmy = null;
        NotifyComputedPropertiesChanged();
    }

    [RelayCommand]
    private async Task DisembarkArmyAsync(Army? army)
    {
        if (army == null) return;
        await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<INavyService>().DisembarkArmyAsync(army.Id));
        CarriedArmies.Remove(army);
        _navy.CarriedArmies.Remove(army);
        NotifyComputedPropertiesChanged();
    }

    private void ReplaceCarriedArmies(IEnumerable<Army> armies)
    {
        CarriedArmies.Clear();
        foreach (var carriedArmy in armies)
            CarriedArmies.Add(carriedArmy);
    }

    public IAsyncRelayCommand SaveCommand { get; }

    private async Task SaveAsync()
    {
        await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<INavyService>().UpdateAsync(_navy));
        NotifyComputedPropertiesChanged();
        Saved?.Invoke();
    }

    private void NotifyComputedPropertiesChanged()
    {
        OnPropertyChanged(nameof(TransportCount));
        OnPropertyChanged(nameof(WarshipCount));
        OnPropertyChanged(nameof(MaxCarryUnits));
        OnPropertyChanged(nameof(TotalCarryUnits));
        OnPropertyChanged(nameof(DailySupplyConsumption));
        OnPropertyChanged(nameof(DaysOfSupply));
        OnPropertyChanged(nameof(CarriedArmies));
        OnPropertyChanged(nameof(HasCarriedArmies));
        OnPropertyChanged(nameof(CarriedArmyNames));
    }

    public NavyViewModel(
        Navy navy,
        IServiceScopeFactory scopeFactory,
        IEnumerable<Commander> availableCommanders,
        IEnumerable<Army> availableArmies,
        IEnumerable<Faction> availableFactions,
        int mapRows = int.MaxValue,
        int mapCols = int.MaxValue)
    {
        _navy = navy;
        _scopeFactory = scopeFactory;
        _availableCommanders = availableCommanders;
        _availableArmies = availableArmies;
        _availableFactions = availableFactions;
        CarriedArmies = new ObservableCollection<Army>(_navy.CarriedArmies);
        _mapRows = mapRows;
        _mapCols = mapCols;
        Ships = new ObservableCollection<Ship>(_navy.Ships);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }
}
