using CommunityToolkit.Mvvm.ComponentModel;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GUI.ViewModels.EntityViewModels;

/// <summary>
/// ViewModel wrapper for MapHex entity with auto-save on property change.
/// </summary>
public partial class MapHexViewModel : ObservableObject, IEntityViewModel
{
    private readonly MapHex _mapHex;
    private readonly IMapService _service;

    public string EntityTypeName => "Hex";

    /// <summary>
    /// The underlying entity (for bindings that need direct access).
    /// </summary>
    public MapHex Entity => _mapHex;

    public int Q => _mapHex.Q;
    public int R => _mapHex.R;

    public TerrainType? TerrainType => _mapHex.TerrainType;
    public Faction? ControllingFaction
    {
        get => _mapHex.ControllingFaction;
        set { if (_mapHex.ControllingFaction != value) { _mapHex.ControllingFaction = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }
    public LocationType? LocationType => _mapHex.LocationType;

    private readonly IEnumerable<Faction> _availableFactions;
    public IEnumerable<Faction> AvailableFactions => _availableFactions;

    public string? LocationName
    {
        get => _mapHex.LocationName;
        set { if (_mapHex.LocationName != value) { _mapHex.LocationName = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int PopulationDensity
    {
        get => _mapHex.PopulationDensity;
        set { if (_mapHex.PopulationDensity != value) { _mapHex.PopulationDensity = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int TimesForaged
    {
        get => _mapHex.TimesForaged;
        set { if (_mapHex.TimesForaged != value) { _mapHex.TimesForaged = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    private async Task SaveAsync()
    {
        await _service.UpdateHexAsync(_mapHex);
    }

    public MapHexViewModel(MapHex mapHex, IMapService service, IEnumerable<Faction> availableFactions)
    {
        _mapHex = mapHex;
        _service = service;
        _availableFactions = availableFactions;
    }
}
