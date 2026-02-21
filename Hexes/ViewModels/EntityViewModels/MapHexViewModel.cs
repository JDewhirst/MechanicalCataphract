using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Hexes;
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

    public event Action? Saved;

    /// <summary>
    /// The underlying entity (for bindings that need direct access).
    /// </summary>
    public MapHex Entity => _mapHex;

    public int Q => _mapHex.Q;
    public int R => _mapHex.R;

    public int Col => OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, new Hex(_mapHex.Q, _mapHex.R, -_mapHex.Q - _mapHex.R)).col;
    public int Row => OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, new Hex(_mapHex.Q, _mapHex.R, -_mapHex.Q - _mapHex.R)).row;

    public TerrainType? TerrainType => _mapHex.TerrainType;
    public Faction? ControllingFaction
    {
        get => _mapHex.ControllingFaction;
        set { if (_mapHex.ControllingFaction != value) { _mapHex.ControllingFaction = value; _mapHex.ControllingFactionId = value?.Id; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public LocationType? LocationType
    {
        get => _mapHex.LocationType;
        set
        {
            if (_mapHex.LocationType != value)
            {
                // Sentinel "No Location" (Id=1) clears the location
                if (value != null && value.Id == 1)
                {
                    _mapHex.LocationType = null;
                    _mapHex.LocationTypeId = null;
                    _mapHex.LocationName = null;
                    _mapHex.LocationFactionId = null;
                }
                else
                {
                    _mapHex.LocationType = value;
                    _mapHex.LocationTypeId = value?.Id;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(LocationName));
                _ = SaveAsync();
            }
        }
    }

    private readonly IEnumerable<Faction> _availableFactions;
    public IEnumerable<Faction> AvailableFactions => _availableFactions;

    private readonly IEnumerable<LocationType> _availableLocationTypes;
    public IEnumerable<LocationType> AvailableLocationTypes => _availableLocationTypes;

    private readonly IEnumerable<Weather> _availableWeatherTypes;
    public IEnumerable<Weather> AvailableWeatherTypes => _availableWeatherTypes;

    public Weather? Weather
    {
        get => _mapHex.Weather;
        set
        {
            if (_mapHex.Weather != value)
            {
                _mapHex.Weather = value;
                _mapHex.WeatherId = value?.Id;
                OnPropertyChanged();
                _ = SaveAsync();
            }
        }
    }

    public string? LocationName
    {
        get => _mapHex.LocationName;
        set { if (_mapHex.LocationName != value) { _mapHex.LocationName = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int? LocationSupply
    {
        get => _mapHex.LocationSupply;
        set { if (_mapHex.LocationSupply != value) { _mapHex.LocationSupply = value; OnPropertyChanged(); _ = SaveAsync(); } }
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
        Saved?.Invoke();
    }

    public MapHexViewModel(MapHex mapHex, IMapService service, IEnumerable<Faction> availableFactions, IEnumerable<LocationType> availableLocationTypes, IEnumerable<Weather> availableWeatherTypes)
    {
        _mapHex = mapHex;
        _service = service;
        _availableFactions = availableFactions;
        _availableLocationTypes = availableLocationTypes;
        _availableWeatherTypes = availableWeatherTypes.Where(w => !string.IsNullOrEmpty(w.IconPath));
    }
}
