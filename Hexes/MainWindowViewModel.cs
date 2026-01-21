using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GUI.ViewModels;

namespace GUI;

public partial class MainWindowViewModel : ObservableObject
{
    public HexMapViewModel HexMapViewModel { get; }

    [ObservableProperty]
    private string? _status;

    // Reference to MapEditorWindow to avoid opening multiple
    private MapEditorWindow? _mapEditorWindow;

    public MainWindowViewModel(HexMapViewModel hexMapViewModel)
    {
        HexMapViewModel = hexMapViewModel;
    }

    // Reference to main window for owned windows
    public Avalonia.Controls.Window? MainWindow { get; set; }

    [RelayCommand]
    private void OpenMapEditor()
    {
        // If window is already open, bring it to front
        if (_mapEditorWindow != null)
        {
            _mapEditorWindow.Activate();
            return;
        }

        _mapEditorWindow = new MapEditorWindow(HexMapViewModel);
        _mapEditorWindow.Closed += (_, _) => _mapEditorWindow = null;

        // Show as owned window so it stays on top of main window
        if (MainWindow != null)
            _mapEditorWindow.Show(MainWindow);
        else
            _mapEditorWindow.Show();
    }

    [RelayCommand]
    private void ZoomIn()
    {
        if (HexMapViewModel.HexRadius < 50)
            HexMapViewModel.HexRadius += 5;
    }

    [RelayCommand]
    private void ZoomOut()
    {
        if (HexMapViewModel.HexRadius > 10)
            HexMapViewModel.HexRadius -= 5;
    }
}
