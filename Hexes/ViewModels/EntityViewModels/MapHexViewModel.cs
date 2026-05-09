using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GUI.ViewModels;
using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GUI.ViewModels.EntityViewModels;

/// <summary>
/// ViewModel wrapper for MapHex entity with auto-save on property change.
/// </summary>
public partial class MapHexViewModel : ObservableObject, IEntityViewModel
{
    private readonly MapHex _mapHex;
    private readonly IServiceScopeFactory _scopeFactory;

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
        get => _mapHex.ControllingFactionId == null
            ? null
            : AvailableFactions.FirstOrDefault(f => f.Id == _mapHex.ControllingFactionId.Value) ?? _mapHex.ControllingFaction;
        set
        {
            if (_mapHex.ControllingFactionId == value?.Id && _mapHex.ControllingFaction == value) return;

            _mapHex.ControllingFaction = value;
            _mapHex.ControllingFactionId = value?.Id;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ControllingFactionId));
            _ = SaveAsync();
        }
    }

    public int? ControllingFactionId
    {
        get => _mapHex.ControllingFactionId;
        set
        {
            if (_mapHex.ControllingFactionId == value) return;

            _mapHex.ControllingFactionId = value;
            _mapHex.ControllingFaction = value == null
                ? null
                : AvailableFactions.FirstOrDefault(f => f.Id == value.Value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ControllingFaction));
            _ = SaveAsync();
        }
    }

    public LocationType? LocationType
    {
        get => _mapHex.LocationTypeId == null
            ? null
            : AvailableLocationTypes.FirstOrDefault(l => l.Id == _mapHex.LocationTypeId.Value) ?? _mapHex.LocationType;
        set
        {
            if ((_mapHex.LocationTypeId ?? 1) == (value?.Id ?? 1) && _mapHex.LocationType == value) return;

            if (value == null || value.Id == 1)
            {
                _mapHex.LocationType = null;
                _mapHex.LocationTypeId = null;
                _mapHex.LocationName = null;
                _mapHex.LocationFactionId = null;
            }
            else
            {
                _mapHex.LocationType = value;
                _mapHex.LocationTypeId = value.Id;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(LocationTypeId));
            OnPropertyChanged(nameof(LocationName));
            _ = SaveAsync();
        }
    }

    public int? LocationTypeId
    {
        get => _mapHex.LocationTypeId ?? 1;
        set
        {
            if ((_mapHex.LocationTypeId ?? 1) != (value ?? 1))
            {
                // Sentinel "No Location" (Id=1) clears the location
                if (value == null || value == 1)
                {
                    _mapHex.LocationType = null;
                    _mapHex.LocationTypeId = null;
                    _mapHex.LocationName = null;
                    _mapHex.LocationFactionId = null;
                }
                else
                {
                    _mapHex.LocationTypeId = value;
                    _mapHex.LocationType = AvailableLocationTypes.FirstOrDefault(l => l.Id == value.Value);
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(LocationType));
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
        get => _mapHex.WeatherId == null
            ? null
            : AvailableWeatherTypes.FirstOrDefault(w => w.Id == _mapHex.WeatherId.Value) ?? _mapHex.Weather;
        set
        {
            if (_mapHex.WeatherId == value?.Id && _mapHex.Weather == value) return;

            _mapHex.Weather = value;
            _mapHex.WeatherId = value?.Id;
            OnPropertyChanged();
            OnPropertyChanged(nameof(WeatherId));
            _ = SaveAsync();
        }
    }

    public int? WeatherId
    {
        get => _mapHex.WeatherId;
        set
        {
            if (_mapHex.WeatherId == value) return;

            _mapHex.WeatherId = value;
            _mapHex.Weather = value == null
                ? null
                : AvailableWeatherTypes.FirstOrDefault(w => w.Id == value.Value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Weather));
            _ = SaveAsync();
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

    public string? Notes
    {
        get => _mapHex.Notes;
        set { if (_mapHex.Notes != value) { _mapHex.Notes = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    private async Task SaveAsync()
    {
        await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<IMapService>().UpdateHexAsync(_mapHex));
        Saved?.Invoke();
    }

    public MapHexViewModel(MapHex mapHex, IServiceScopeFactory scopeFactory, IEnumerable<Faction> availableFactions, IEnumerable<LocationType> availableLocationTypes, IEnumerable<Weather> availableWeatherTypes)
    {
        _mapHex = mapHex;
        _scopeFactory = scopeFactory;
        _availableFactions = availableFactions;
        _availableLocationTypes = availableLocationTypes;
        _availableWeatherTypes = availableWeatherTypes.Where(w => !string.IsNullOrEmpty(w.IconPath));
    }
}
