using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace GUI.ViewModels;

public partial class HexMapViewModel : ObservableObject
{
    private readonly IMapService _mapService;

    // Database-backed hex data
    [ObservableProperty]
    private ObservableCollection<MapHex> _visibleHexes = new();

    [ObservableProperty]
    private ObservableCollection<TerrainType> _terrainTypes = new();

    [ObservableProperty]
    private TerrainType? _selectedTerrainType;

    [ObservableProperty]
    private ObservableCollection<LocationType> _locationTypes = new();

    [ObservableProperty]
    private LocationType? _selectedLocationType;

    // Overlay options for map visualization
    public ObservableCollection<string> OverlayOptions { get; } = new()
    {
        "None",
        "Faction Control",
        "Population Density",
        "Times Foraged",
        "Weather"
    };

    [ObservableProperty]
    private string _selectedOverlay = "None";

    // Map dimensions
    private int _mapRows;
    private int _mapColumns;

    [ObservableProperty]
    private double _hexRadius = 20.0;

    [ObservableProperty]
    private Vector _panOffset = Vector.Zero;

    [ObservableProperty]
    private string _currentTool = "Pan";

    [ObservableProperty]
    private Hex? _selectedHex;

    [ObservableProperty]
    private MapHex? _selectedMapHex;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // Road painting state - tracks first hex in two-click road creation
    [ObservableProperty]
    private Hex? _roadStartHex;

    // River Painting state - tracks first hex in two-click river creation
    [ObservableProperty]
    private Hex? _riverStartHex;

    public HexMapViewModel(IMapService mapService)
    {
        _mapService = mapService;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        // Load terrain types
        var terrainTypes = await _mapService.GetTerrainTypesAsync();
        TerrainTypes = new ObservableCollection<TerrainType>(terrainTypes);

        if (TerrainTypes.Count > 0)
            SelectedTerrainType = TerrainTypes[0];

        // Load location types
        var locationTypes = await _mapService.GetLocationTypesAsync();
        LocationTypes = new ObservableCollection<LocationType>(locationTypes);

        if (LocationTypes.Count > 0)
            SelectedLocationType = LocationTypes[0];

        // Check if map exists, if not create one
        if (!await _mapService.MapExistsAsync())
        {
            await _mapService.InitializeMapAsync(50, 50, TerrainTypes.Count > 0 ? TerrainTypes[0].Id : 1);
        }

        // Get map dimensions
        var (rows, cols) = await _mapService.GetMapDimensionsAsync();
        _mapRows = rows;
        _mapColumns = cols;

        // Load all hexes (for small maps; for large maps would use viewport-based loading)
        await LoadVisibleHexesAsync();

        StatusMessage = $"Loaded {VisibleHexes.Count} hexes, {TerrainTypes.Count} terrain types";
    }

    private async Task LoadVisibleHexesAsync()
    {
        var hexes = await _mapService.GetAllHexesAsync();
        VisibleHexes = new ObservableCollection<MapHex>(hexes);
    }

    partial void OnSelectedHexChanged(Hex? value)
    {
        if (value.HasValue)
        {
            var hex = value.Value;
            StatusMessage = $"Selected: ({hex.q}, {hex.r}, {hex.s})";

            // Find the MapHex for this hex
            foreach (var mapHex in VisibleHexes)
            {
                if (mapHex.Q == hex.q && mapHex.R == hex.r)
                {
                    SelectedMapHex = mapHex;
                    if (mapHex.TerrainType != null)
                    {
                        StatusMessage += $" - {mapHex.TerrainType.Name}";
                    }
                    break;
                }
            }
        }
        else
        {
            StatusMessage = string.Empty;
            SelectedMapHex = null;
        }
    }

    [RelayCommand]
    private void SelectTool()
    {
        CurrentTool = "Select";
        StatusMessage = "Tool: Select";
    }

    [RelayCommand]
    private void PanTool()
    {
        CurrentTool = "Pan";
        StatusMessage = "Tool: Pan";
    }

    [RelayCommand]
    private void TerrainPaintTool()
    {
        CurrentTool = "TerrainPaint";
        StatusMessage = $"Tool: Terrain Paint - {SelectedTerrainType?.Name ?? "None"}";
    }

    [RelayCommand]
    private void RoadPaintTool()
    {
        CurrentTool = "RoadPaint";
        RoadStartHex = null;
        StatusMessage = "Tool: Road - Click first hex";
    }

    [RelayCommand]
    private void RiverPaintTool()
    {
        CurrentTool = "RiverPaint";
        RiverStartHex = null;
        StatusMessage = "Tool: River -  Click first hex";
    }

    [RelayCommand]
    private void EraseTool()
    {
        CurrentTool = "Erase";
        RoadStartHex = null;
        StatusMessage = "Tool: Erase - Click hex to clear roads/rivers";
    }

    [RelayCommand]
    private void LocationPaintTool()
    {
        CurrentTool = "LocationPaint";
        StatusMessage = $"Tool: Location Paint - {SelectedLocationType?.Name ?? "None"}";
    }

    [RelayCommand]
    private void SelectHex(Hex hex)
    {
        SelectedHex = hex;
    }

    [RelayCommand]
    private void CompletePan(Vector delta)
    {
        PanOffset = new Vector(PanOffset.X + delta.X, PanOffset.Y + delta.Y);
    }

    [RelayCommand]
    private async Task PaintTerrainAsync((Hex hex, int terrainTypeId) args)
    {
        await _mapService.SetTerrainAsync(args.hex, args.terrainTypeId);

        // Update local hex in collection
        for (int i = 0; i < VisibleHexes.Count; i++)
        {
            var mapHex = VisibleHexes[i];
            if (mapHex.Q == args.hex.q && mapHex.R == args.hex.r)
            {
                // Reload the hex from database to get updated TerrainType
                var updatedHex = await _mapService.GetHexAsync(args.hex);
                if (updatedHex != null)
                {
                    VisibleHexes[i] = updatedHex;
                }
                break;
            }
        }

        var terrainName = TerrainTypes.FirstOrDefault(t => t.Id == args.terrainTypeId)?.Name ?? "Unknown";
        StatusMessage = $"Painted {terrainName} at ({args.hex.q}, {args.hex.r})";
    }

    [RelayCommand]
    private async Task PaintRoadAsync(Hex clickedHex)
    {
        if (!RoadStartHex.HasValue)
        {
            // First click - set start hex
            RoadStartHex = clickedHex;
            StatusMessage = $"Road start: ({clickedHex.q}, {clickedHex.r}) - Click adjacent hex";
            return;
        }

        var startHex = RoadStartHex.Value;

        // Check if same hex clicked - cancel
        if (startHex.q == clickedHex.q && startHex.r == clickedHex.r)
        {
            RoadStartHex = null;
            StatusMessage = "Road cancelled - Click first hex";
            return;
        }

        // Find direction from start to clicked hex
        int? dirFromStart = GetNeighborDirection(startHex, clickedHex);
        if (!dirFromStart.HasValue)
        {
            // Not adjacent - start over with this hex
            RoadStartHex = clickedHex;
            StatusMessage = $"Not adjacent. New start: ({clickedHex.q}, {clickedHex.r}) - Click adjacent hex";
            return;
        }

        // Get opposite direction for the other hex
        int dirFromEnd = (dirFromStart.Value + 3) % 6;

        // Check if road already exists - toggle off if so
        var startMapHex = VisibleHexes.FirstOrDefault(h => h.Q == startHex.q && h.R == startHex.r);
        bool hasRoad = startMapHex?.HasRoadInDirection(dirFromStart.Value) ?? false;

        // Set road on both hexes
        await _mapService.SetRoadAsync(startHex, dirFromStart.Value, !hasRoad);
        await _mapService.SetRoadAsync(clickedHex, dirFromEnd, !hasRoad);

        // Update local hexes in collection
        await RefreshHexInCollection(startHex);
        await RefreshHexInCollection(clickedHex);

        // Reset for next road
        RoadStartHex = null;
        StatusMessage = hasRoad
            ? $"Removed road between ({startHex.q}, {startHex.r}) and ({clickedHex.q}, {clickedHex.r})"
            : $"Added road between ({startHex.q}, {startHex.r}) and ({clickedHex.q}, {clickedHex.r})";
    }

    /// <summary>
    /// Gets the direction index (0-5) from hex A to hex B, or null if not adjacent.
    /// </summary>
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

    /// <summary>
    /// Reloads a hex from the database and updates the local collection.
    /// </summary>
    private async Task RefreshHexInCollection(Hex hex)
    {
        for (int i = 0; i < VisibleHexes.Count; i++)
        {
            var mapHex = VisibleHexes[i];
            if (mapHex.Q == hex.q && mapHex.R == hex.r)
            {
                var updatedHex = await _mapService.GetHexAsync(hex);
                if (updatedHex != null)
                {
                    VisibleHexes[i] = updatedHex;
                }
                break;
            }
        }
    }

    [RelayCommand]
    private async Task PaintRiverAsync(Hex clickedHex)
    {
        if (!RiverStartHex.HasValue)
        {
            // First click - set start hex
            RiverStartHex = clickedHex;
            StatusMessage = $"River start: ({clickedHex.q}, {clickedHex.r}) - Click adjacent hex";
            return;
        }

        var startHex = RiverStartHex.Value;

        // Check if same hex clicked - cancel
        if (startHex.q == clickedHex.q && startHex.r == clickedHex.r)
        {
            RiverStartHex = null;
            StatusMessage = "River cancelled - Click first hex";
            return;
        }

        // Find direction from start to clicked hex
        int? dirFromStart = GetNeighborDirection(startHex, clickedHex);
        if (!dirFromStart.HasValue)
        {
            // Not adjacent - start over with this hex
            RiverStartHex = clickedHex;
            StatusMessage = $"Not adjacent. New start: ({clickedHex.q}, {clickedHex.r}) - Click adjacent hex";
            return;
        }

        // Get opposite direction for the other hex
        int dirFromEnd = (dirFromStart.Value + 3) % 6;

        // Check if road already exists - toggle off if so
        var startMapHex = VisibleHexes.FirstOrDefault(h => h.Q == startHex.q && h.R == startHex.r);
        bool hasRiver = startMapHex?.HasRiverOnEdge(dirFromStart.Value) ?? false;

        // Set river on both hexes
        await _mapService.SetRiverAsync(startHex, dirFromStart.Value, !hasRiver);
        await _mapService.SetRiverAsync(clickedHex, dirFromEnd, !hasRiver);

        // Update local hexes in collection
        await RefreshHexInCollection(startHex);
        await RefreshHexInCollection(clickedHex);

        // Reset for next river
        RiverStartHex = null;
        StatusMessage = hasRiver
            ? $"Removed river between ({startHex.q}, {startHex.r}) and ({clickedHex.q}, {clickedHex.r})"
            : $"Added river between ({startHex.q}, {startHex.r}) and ({clickedHex.q}, {clickedHex.r})";
    }

    [RelayCommand]
    private async Task EraseAsync(Hex hex)
    {
        await _mapService.ClearRoadsAndRiversAsync(hex);
        await RefreshHexInCollection(hex);
        StatusMessage = $"Cleared roads/rivers at ({hex.q}, {hex.r})";
    }

    [RelayCommand]
    private async Task PaintLocationAsync((Hex hex, string? locationName) args)
    {
        if (SelectedLocationType == null)
        {
            StatusMessage = "No location type selected";
            return;
        }

        await _mapService.SetLocationAsync(args.hex, SelectedLocationType.Id, args.locationName);
        await RefreshHexInCollection(args.hex);

        var name = string.IsNullOrEmpty(args.locationName) ? SelectedLocationType.Name : args.locationName;
        StatusMessage = $"Set location '{name}' ({SelectedLocationType.Name}) at ({args.hex.q}, {args.hex.r})";
    }

    [RelayCommand]
    private async Task ClearLocationAsync(Hex hex)
    {
        await _mapService.ClearLocationAsync(hex);
        await RefreshHexInCollection(hex);
        StatusMessage = $"Cleared location at ({hex.q}, {hex.r})";
    }

    partial void OnSelectedTerrainTypeChanged(TerrainType? value)
    {
        if (CurrentTool == "TerrainPaint" && value != null)
        {
            StatusMessage = $"Tool: Terrain Paint - {value.Name}";
        }
    }

    partial void OnSelectedLocationTypeChanged(LocationType? value)
    {
        if (CurrentTool == "LocationPaint" && value != null)
        {
            StatusMessage = $"Tool: Location Paint - {value.Name}";
        }
    }
}
