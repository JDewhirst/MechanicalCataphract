using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using Weather = MechanicalCataphract.Data.Entities.Weather;

namespace GUI.ViewModels.HexMap;

public partial class MapEditingCoordinator : ObservableObject
{
    private readonly IMapService _mapService;
    private readonly Func<ObservableCollection<MapHex>> _getVisibleHexes;
    private readonly Func<ObservableCollection<TerrainType>> _getTerrainTypes;
    private readonly Func<ObservableCollection<LocationType>> _getLocationTypes;
    private readonly Func<Hex?> _getSelectedHex;
    private readonly Action<MapHex?> _setSelectedMapHex;
    private readonly Action<string> _setStatusMessage;

    [ObservableProperty]
    private string _currentTool = "Pan";

    [ObservableProperty]
    private int _brushSize = 1;

    [ObservableProperty]
    private int _selectedPopulationDensity = 20;

    [ObservableProperty]
    private TerrainType? _selectedTerrainType;

    [ObservableProperty]
    private LocationType? _selectedLocationType;

    [ObservableProperty]
    private Faction? _selectedFactionForPainting;

    [ObservableProperty]
    private Weather? _selectedWeatherForPainting;

    [ObservableProperty]
    private Hex? _roadStartHex;

    [ObservableProperty]
    private Hex? _riverStartHex;

    [ObservableProperty]
    private string _selectedOverlay = "None";

    public ObservableCollection<string> OverlayOptions { get; } = new()
    {
        "None",
        "Faction Control",
        "Population Density",
        "Times Foraged",
        "Weather"
    };

    public MapEditingCoordinator(
        IMapService mapService,
        Func<ObservableCollection<MapHex>> getVisibleHexes,
        Func<ObservableCollection<TerrainType>> getTerrainTypes,
        Func<ObservableCollection<LocationType>> getLocationTypes,
        Func<Hex?> getSelectedHex,
        Action<MapHex?> setSelectedMapHex,
        Action<string> setStatusMessage)
    {
        _mapService = mapService;
        _getVisibleHexes = getVisibleHexes;
        _getTerrainTypes = getTerrainTypes;
        _getLocationTypes = getLocationTypes;
        _getSelectedHex = getSelectedHex;
        _setSelectedMapHex = setSelectedMapHex;
        _setStatusMessage = setStatusMessage;
    }

    [RelayCommand]
    public void SelectTool() { CurrentTool = "Select"; _setStatusMessage("Tool: Select"); }

    [RelayCommand]
    public void PanTool() { CurrentTool = "Pan"; _setStatusMessage("Tool: Pan"); }

    [RelayCommand]
    public void TerrainPaintTool() { CurrentTool = "TerrainPaint"; _setStatusMessage($"Tool: Terrain Paint - {SelectedTerrainType?.Name ?? "None"}"); }

    [RelayCommand]
    public void RoadPaintTool() { CurrentTool = "RoadPaint"; RoadStartHex = null; _setStatusMessage("Tool: Road - Click first hex"); }

    [RelayCommand]
    public void RiverPaintTool() { CurrentTool = "RiverPaint"; RiverStartHex = null; _setStatusMessage("Tool: River -  Click first hex"); }

    [RelayCommand]
    public void EraseTool() { CurrentTool = "Erase"; RoadStartHex = null; _setStatusMessage("Tool: Erase - Click hex to clear roads/rivers"); }

    [RelayCommand]
    public void LocationPaintTool() { CurrentTool = "LocationPaint"; _setStatusMessage($"Tool: Location Paint - {SelectedLocationType?.Name ?? "None"}"); }

    [RelayCommand]
    public void PopulationPaintTool() { CurrentTool = "PopulationPaint"; SelectedOverlay = "Population Density"; _setStatusMessage($"Tool: Population Paint - Density {SelectedPopulationDensity}"); }

    [RelayCommand]
    public void FactionControlPaintTool() { CurrentTool = "FactionControlPaint"; SelectedOverlay = "Faction Control"; _setStatusMessage($"Tool: Faction Paint - {SelectedFactionForPainting?.Name ?? "None"}"); }

    [RelayCommand]
    public void WeatherPaintTool() { CurrentTool = "WeatherPaint"; SelectedOverlay = "Weather"; _setStatusMessage($"Tool: Weather Paint - {SelectedWeatherForPainting?.Name ?? "None"}"); }

    [RelayCommand]
    public void SetPopulationDensity(string densityStr)
    {
        if (int.TryParse(densityStr, out var density))
        {
            SelectedPopulationDensity = density;
            if (CurrentTool == "PopulationPaint")
                _setStatusMessage($"Tool: Population Paint - Density {SelectedPopulationDensity}");
        }
    }

    public async Task PaintTerrainAsync(Hex hex, int terrainTypeId)
    {
        var visibleHexes = _getVisibleHexes();
        var toPaint = GetHexesInRadius(hex, BrushSize)
            .Where(h => visibleHexes.Any(v => v.Q == h.q && v.R == h.r))
            .ToList();

        foreach (var h in toPaint)
            await _mapService.SetTerrainAsync(h, terrainTypeId);

        var terrainType = _getTerrainTypes().FirstOrDefault(t => t.Id == terrainTypeId);
        UpdateVisibleHexes(toPaint, mapHex =>
        {
            mapHex.TerrainTypeId = terrainTypeId;
            mapHex.TerrainType = terrainType;
        });

        var terrainName = terrainType?.Name ?? "Unknown";
        _setStatusMessage(BrushSize == 1
            ? $"Painted {terrainName} at ({hex.q}, {hex.r})"
            : $"Painted {terrainName} at ({hex.q}, {hex.r}) - brush {BrushSize} ({toPaint.Count} hexes)");
    }

    public async Task PaintFactionControlAsync(Hex hex)
    {
        if (SelectedFactionForPainting == null) return;
        var toPaint = GetPaintableLandHexes(hex);

        foreach (var h in toPaint)
            await _mapService.SetFactionControlAsync(h, SelectedFactionForPainting.Id);

        var faction = SelectedFactionForPainting;
        UpdateVisibleHexes(toPaint, mapHex =>
        {
            mapHex.ControllingFactionId = faction.Id;
            mapHex.ControllingFaction = faction;
        });

        _setStatusMessage(BrushSize == 1
            ? $"Painted {faction.Name} at ({hex.q}, {hex.r})"
            : $"Painted {faction.Name} at ({hex.q}, {hex.r}) - brush {BrushSize} ({toPaint.Count} hexes)");
    }

    public async Task PaintRoadAsync(Hex clickedHex)
    {
        if (!RoadStartHex.HasValue)
        {
            RoadStartHex = clickedHex;
            _setStatusMessage($"Road start: ({clickedHex.q}, {clickedHex.r}) - Click adjacent hex");
            return;
        }

        var startHex = RoadStartHex.Value;
        if (startHex.q == clickedHex.q && startHex.r == clickedHex.r)
        {
            RoadStartHex = null;
            _setStatusMessage("Road cancelled - Click first hex");
            return;
        }

        int? dirFromStart = GetNeighborDirection(startHex, clickedHex);
        if (!dirFromStart.HasValue)
        {
            RoadStartHex = clickedHex;
            _setStatusMessage($"Not adjacent. New start: ({clickedHex.q}, {clickedHex.r}) - Click adjacent hex");
            return;
        }

        int dirFromEnd = (dirFromStart.Value + 3) % 6;
        var startMapHex = _getVisibleHexes().FirstOrDefault(h => h.Q == startHex.q && h.R == startHex.r);
        bool hasRoad = startMapHex?.HasRoadInDirection(dirFromStart.Value) ?? false;

        await _mapService.SetRoadAsync(startHex, dirFromStart.Value, !hasRoad);
        await _mapService.SetRoadAsync(clickedHex, dirFromEnd, !hasRoad);
        await RefreshHexInCollection(startHex);
        await RefreshHexInCollection(clickedHex);

        RoadStartHex = clickedHex;
        _setStatusMessage(hasRoad
            ? $"Removed road. Continue from ({clickedHex.q}, {clickedHex.r}) or click same hex to cancel"
            : $"Added road. Continue from ({clickedHex.q}, {clickedHex.r}) or click same hex to cancel");
    }

    public async Task PaintRiverAsync(Hex clickedHex)
    {
        if (!RiverStartHex.HasValue)
        {
            RiverStartHex = clickedHex;
            _setStatusMessage($"River start: ({clickedHex.q}, {clickedHex.r}) - Click adjacent hex");
            return;
        }

        var startHex = RiverStartHex.Value;
        if (startHex.q == clickedHex.q && startHex.r == clickedHex.r)
        {
            RiverStartHex = null;
            _setStatusMessage("River cancelled - Click first hex");
            return;
        }

        int? dirFromStart = GetNeighborDirection(startHex, clickedHex);
        if (!dirFromStart.HasValue)
        {
            RiverStartHex = clickedHex;
            _setStatusMessage($"Not adjacent. New start: ({clickedHex.q}, {clickedHex.r}) - Click adjacent hex");
            return;
        }

        int dirFromEnd = (dirFromStart.Value + 3) % 6;
        var startMapHex = _getVisibleHexes().FirstOrDefault(h => h.Q == startHex.q && h.R == startHex.r);
        bool hasRiver = startMapHex?.HasRiverOnEdge(dirFromStart.Value) ?? false;

        await _mapService.SetRiverAsync(startHex, dirFromStart.Value, !hasRiver);
        await _mapService.SetRiverAsync(clickedHex, dirFromEnd, !hasRiver);
        await RefreshHexInCollection(startHex);
        await RefreshHexInCollection(clickedHex);

        RiverStartHex = null;
        _setStatusMessage(hasRiver
            ? "Removed river. Click first hex for next segment"
            : "Added river. Click first hex for next segment");
    }

    public async Task EraseAsync(Hex hex)
    {
        await _mapService.ClearRoadsAndRiversAsync(hex);
        UpdateVisibleHexes(new[] { hex }, mapHex =>
        {
            mapHex.RoadDirections = null;
            mapHex.RiverEdges = null;
        });
        _setStatusMessage($"Cleared roads/rivers at ({hex.q}, {hex.r})");
    }

    public async Task PaintLocationAsync(Hex hex, string? locationName)
    {
        if (SelectedLocationType == null)
        {
            _setStatusMessage("No location type selected");
            return;
        }

        if (SelectedLocationType.Id == 1)
        {
            await ClearLocationAsync(hex);
            return;
        }

        await _mapService.SetLocationAsync(hex, SelectedLocationType.Id, locationName);
        var locationType = _getLocationTypes().FirstOrDefault(l => l.Id == SelectedLocationType.Id);
        UpdateVisibleHexes(new[] { hex }, mapHex =>
        {
            mapHex.LocationTypeId = SelectedLocationType.Id;
            mapHex.LocationType = locationType;
            mapHex.LocationName = locationName;
        }, updateSelectedHex: true);

        var name = string.IsNullOrEmpty(locationName) ? SelectedLocationType.Name : locationName;
        _setStatusMessage($"Set location '{name}' ({SelectedLocationType.Name}) at ({hex.q}, {hex.r})");
    }

    public async Task ClearLocationAsync(Hex hex)
    {
        await _mapService.ClearLocationAsync(hex);
        UpdateVisibleHexes(new[] { hex }, mapHex =>
        {
            mapHex.LocationTypeId = null;
            mapHex.LocationType = null;
            mapHex.LocationName = null;
            mapHex.LocationFactionId = null;
        }, updateSelectedHex: true);
        _setStatusMessage($"Cleared location at ({hex.q}, {hex.r})");
    }

    public async Task PaintPopulationAsync(Hex hex)
    {
        var toPaint = GetPaintableLandHexes(hex);
        foreach (var h in toPaint)
            await _mapService.SetPopulationDensityAsync(h, SelectedPopulationDensity);

        UpdateVisibleHexes(toPaint, mapHex => mapHex.PopulationDensity = SelectedPopulationDensity);
        _setStatusMessage(BrushSize == 1
            ? $"Painted density {SelectedPopulationDensity} at ({hex.q}, {hex.r})"
            : $"Painted density {SelectedPopulationDensity} at ({hex.q}, {hex.r}) - brush {BrushSize} ({toPaint.Count} hexes)");
    }

    public async Task PaintWeatherAsync(Hex hex)
    {
        if (SelectedWeatherForPainting == null) return;
        var toPaint = GetHexesInRadius(hex, BrushSize)
            .Where(ExistsInVisibleHexes)
            .ToList();

        foreach (var h in toPaint)
            await _mapService.SetWeatherAsync(h, SelectedWeatherForPainting.Id);

        var weather = SelectedWeatherForPainting;
        UpdateVisibleHexes(toPaint, mapHex =>
        {
            mapHex.WeatherId = weather.Id;
            mapHex.Weather = weather;
        });

        _setStatusMessage(BrushSize == 1
            ? $"Painted {weather.Name} at ({hex.q}, {hex.r})"
            : $"Painted {weather.Name} at ({hex.q}, {hex.r}) - brush {BrushSize} ({toPaint.Count} hexes)");
    }

    partial void OnSelectedTerrainTypeChanged(TerrainType? value)
    {
        if (CurrentTool == "TerrainPaint" && value != null)
            _setStatusMessage($"Tool: Terrain Paint - {value.Name}");
    }

    partial void OnSelectedLocationTypeChanged(LocationType? value)
    {
        if (CurrentTool == "LocationPaint" && value != null)
            _setStatusMessage($"Tool: Location Paint - {value.Name}");
    }

    partial void OnSelectedFactionForPaintingChanged(Faction? value)
    {
        if (CurrentTool == "FactionControlPaint" && value != null)
            _setStatusMessage($"Tool: Faction Paint - {value.Name}");
    }

    private List<Hex> GetPaintableLandHexes(Hex center)
    {
        var hexLookup = _getVisibleHexes().ToDictionary(h => (h.Q, h.R));
        var waterTerrainIds = new HashSet<int>(_getTerrainTypes().Where(t => t.IsWater).Select(t => t.Id));

        return GetHexesInRadius(center, BrushSize)
            .Where(h => hexLookup.ContainsKey((h.q, h.r)))
            .Where(h =>
            {
                var mapHex = hexLookup[(h.q, h.r)];
                return !mapHex.TerrainTypeId.HasValue || !waterTerrainIds.Contains(mapHex.TerrainTypeId.Value);
            })
            .ToList();
    }

    private bool ExistsInVisibleHexes(Hex hex)
        => _getVisibleHexes().Any(h => h.Q == hex.q && h.R == hex.r);

    private void UpdateVisibleHexes(IEnumerable<Hex> hexes, Action<MapHex> update, bool updateSelectedHex = false)
    {
        var targetCoords = new HashSet<(int q, int r)>(hexes.Select(h => (h.q, h.r)));
        var visibleHexes = _getVisibleHexes();
        for (int i = 0; i < visibleHexes.Count; i++)
        {
            var mapHex = visibleHexes[i];
            if (!targetCoords.Contains((mapHex.Q, mapHex.R)))
                continue;

            update(mapHex);
            visibleHexes[i] = mapHex;
            if (updateSelectedHex && _getSelectedHex() is Hex selected && selected.q == mapHex.Q && selected.r == mapHex.R)
                _setSelectedMapHex(mapHex);
        }
    }

    private async Task RefreshHexInCollection(Hex hex)
    {
        var visibleHexes = _getVisibleHexes();
        for (int i = 0; i < visibleHexes.Count; i++)
        {
            var mapHex = visibleHexes[i];
            if (mapHex.Q == hex.q && mapHex.R == hex.r)
            {
                var updatedHex = await _mapService.GetHexAsync(hex);
                if (updatedHex != null)
                    visibleHexes[i] = updatedHex;
                break;
            }
        }
    }

    public static IEnumerable<Hex> GetHexesInRadius(Hex center, int brushSize)
    {
        int radius = Math.Max(0, brushSize - 1);
        for (int dq = -radius; dq <= radius; dq++)
        {
            int rMin = Math.Max(-radius, -dq - radius);
            int rMax = Math.Min(radius, -dq + radius);
            for (int dr = rMin; dr <= rMax; dr++)
                yield return new Hex(center.q + dq, center.r + dr, -(center.q + dq) - (center.r + dr));
        }
    }

    private static int? GetNeighborDirection(Hex from, Hex to)
    {
        for (int dir = 0; dir < 6; dir++)
        {
            var neighbor = from.Neighbor(dir);
            if (neighbor.q == to.q && neighbor.r == to.r)
                return dir;
        }

        return null;
    }
}
