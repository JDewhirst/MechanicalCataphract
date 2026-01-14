using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hexes;
using MechanicalCataphract.Services;

namespace GUI.ViewModels;

public partial class HexMapViewModel : ObservableObject
{
    private readonly IMapService _mapService;

    [ObservableProperty]
    private HexMapModel _mapModel;

    [ObservableProperty]
    private double _hexRadius = 20.0;

    [ObservableProperty]
    private Vector _panOffset = Vector.Zero;

    [ObservableProperty]
    private string _currentTool = "Pan";

    [ObservableProperty]
    private Hex? _selectedHex;

    [ObservableProperty]
    private Tile? _selectedTile;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public HexMapViewModel(IMapService mapService)
    {
        _mapService = mapService;
        // Keep in-memory model for now; Phase 2 will migrate to database-backed rendering
        _mapModel = new HexMapModel(100, 100);
    }

    partial void OnSelectedHexChanged(Hex? value)
    {
        if (value.HasValue)
        {
            var hex = value.Value;
            StatusMessage = $"Selected: ({hex.q}, {hex.r}, {hex.s})";
        }
        else
        {
            StatusMessage = string.Empty;
        }
    }

    [RelayCommand]
    private void SelectTool()
    {
        CurrentTool = "Select";
    }

    [RelayCommand]
    private void PanTool()
    {
        CurrentTool = "Pan";
    }

    [RelayCommand]
    private void SelectHex(Hex hex)
    {
        SelectedHex = hex;
        MapModel.TryGetTile(hex, out var tile);
        SelectedTile = tile;
    }

    [RelayCommand]
    private void CompletePan(Vector delta)
    {
        PanOffset = new Vector(PanOffset.X + delta.X, PanOffset.Y + delta.Y);
    }
}
