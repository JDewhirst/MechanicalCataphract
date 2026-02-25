using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace GUI.ViewModels.EntityViewModels;

/// <summary>
/// ViewModel wrapper for Navy entity with Ships management and auto-save.
/// </summary>
public partial class NavyViewModel : ObservableObject, IEntityViewModel
{
    private readonly Navy _navy;
    private readonly INavyService _service;
    private readonly int _mapRows;
    private readonly int _mapCols;

    public string EntityTypeName => "Navy";
    public Navy Entity => _navy;
    public int Id => _navy.Id;

    private readonly IEnumerable<Commander> _availableCommanders;
    public IEnumerable<Commander> AvailableCommanders => _availableCommanders;

    private readonly IEnumerable<Army> _availableArmies;
    public IEnumerable<Army> AvailableArmies => _availableArmies;

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

    public Commander? Commander
    {
        get => _navy.Commander;
        set { if (_navy.Commander != value) { _navy.Commander = value; _navy.CommanderId = value?.Id; OnPropertyChanged(); _ = SaveAsync(); } }
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
    public int TransportCount => _navy.TransportCount;
    public int WarshipCount   => _navy.WarshipCount;
    public int MaxCarryUnits  => _navy.MaxCarryUnits;
    public double TotalCarryUnits => _navy.TotalCarryUnits;
    public int DailySupplyConsumption => _navy.DailySupplyConsumption;
    public double DaysOfSupply => _navy.DaysOfSupply;

    public string CarriedArmyName => _navy.CarriedArmy?.Name ?? "(Empty)";
    public bool HasCarriedArmy => _navy.CarriedArmy != null;

    /// <summary>
    /// Observable collection of ships for UI binding.
    /// </summary>
    public ObservableCollection<Ship> Ships { get; }

    public event Action? Saved;

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task AddShipAsync()
    {
        var ship = new Ship
        {
            NavyId = _navy.Id,
            ShipType = ShipType.Transport,
            Count = 1
        };

        await _service.AddShipAsync(ship);
        Ships.Add(ship);
        _navy.Ships.Add(ship);
        NotifyComputedPropertiesChanged();
    }

    [RelayCommand]
    private async Task DeleteShipAsync(Ship? ship)
    {
        if (ship == null) return;
        await _service.DeleteShipAsync(ship.Id);
        _navy.Ships.Remove(ship);
        Ships.Remove(ship);
        NotifyComputedPropertiesChanged();
    }

    [RelayCommand]
    private async Task EmbarkArmyAsync(Army? army)
    {
        if (army == null) return;
        await _service.EmbarkArmyAsync(_navy.Id, army.Id);
        // Reload so CarriedArmy navigation is populated
        var refreshed = await _service.GetNavyWithShipsAsync(_navy.Id);
        if (refreshed != null)
            _navy.CarriedArmy = refreshed.CarriedArmy;
        NotifyComputedPropertiesChanged();
    }

    [RelayCommand]
    private async Task DisembarkArmyAsync()
    {
        if (_navy.CarriedArmy == null) return;
        await _service.DisembarkArmyAsync(_navy.CarriedArmy.Id);
        _navy.CarriedArmy = null;
        NotifyComputedPropertiesChanged();
    }

    public IAsyncRelayCommand SaveCommand { get; }

    private async Task SaveAsync()
    {
        await _service.UpdateAsync(_navy);
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
        OnPropertyChanged(nameof(CarriedArmyName));
        OnPropertyChanged(nameof(HasCarriedArmy));
    }

    private bool IsOffsetInBounds(int col, int row)
        => col >= 0 && col < _mapCols && row >= 0 && row < _mapRows;

    public NavyViewModel(
        Navy navy,
        INavyService service,
        IEnumerable<Commander> availableCommanders,
        IEnumerable<Army> availableArmies,
        int mapRows = int.MaxValue,
        int mapCols = int.MaxValue)
    {
        _navy = navy;
        _service = service;
        _availableCommanders = availableCommanders;
        _availableArmies = availableArmies;
        _mapRows = mapRows;
        _mapCols = mapCols;
        Ships = new ObservableCollection<Ship>(_navy.Ships);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }
}
