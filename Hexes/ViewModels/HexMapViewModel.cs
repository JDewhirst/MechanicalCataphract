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

    partial void OnSelectedTerrainTypeChanged(TerrainType? value)
    {
        if (CurrentTool == "TerrainPaint" && value != null)
        {
            StatusMessage = $"Tool: Terrain Paint - {value.Name}";
        }
    }
}
