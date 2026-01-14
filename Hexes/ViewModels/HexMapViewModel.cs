using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hexes;

namespace GUI.ViewModels;

public partial class HexMapViewModel : ObservableObject
{
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

    public HexMapViewModel()
    {
        _mapModel = new HexMapModel(10000, 1000);
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
