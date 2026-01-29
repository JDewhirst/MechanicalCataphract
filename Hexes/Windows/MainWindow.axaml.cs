using Avalonia.Controls;
using Hexes;

namespace GUI;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        DataContext = viewModel;
        viewModel.MainWindow = this;
        InitializeComponent();

        // Wire HexMapView events to ViewModel commands
        // Events cannot be bound directly to ICommand in Avalonia XAML
        HexMapView.HexClicked += (s, hex) =>
        {
            // Check if we're in forage selection mode
            if (viewModel.HexMapViewModel.IsForageModeActive)
            {
                viewModel.HexMapViewModel.ToggleForageHexSelection(hex);
                return;
            }
            // Clear all entity selections when hex is selected
            viewModel.HexMapViewModel.SelectedFaction = null;
            viewModel.HexMapViewModel.SelectedArmy = null;
            viewModel.HexMapViewModel.SelectedCommander = null;
            viewModel.HexMapViewModel.SelectedOrder = null;
            viewModel.HexMapViewModel.SelectedMessage = null;
            viewModel.HexMapViewModel.SelectHexCommand.Execute(hex);
        };

        HexMapView.ArmyClicked += (s, army) =>
        {
            // SelectedArmy setter clears other selections
            viewModel.HexMapViewModel.SelectedArmy = army;
        };

        HexMapView.CommanderClicked += (s, commander) =>
        {
            // SelectedCommander setter clears other selections
            viewModel.HexMapViewModel.SelectedCommander = commander;
        };

        HexMapView.PanCompleted += (s, delta) =>
            viewModel.HexMapViewModel.CompletePanCommand.Execute(delta);
        HexMapView.TerrainPainted += (s, args) =>
            viewModel.HexMapViewModel.PaintTerrainCommand.Execute(args);
        HexMapView.RoadPainted += (s, hex) =>
            viewModel.HexMapViewModel.PaintRoadCommand.Execute(hex);
        HexMapView.RiverPainted += (s, hex) =>
            viewModel.HexMapViewModel.PaintRiverCommand.Execute(hex);
        HexMapView.EraseRequested += (s, hex) =>
            viewModel.HexMapViewModel.EraseCommand.Execute(hex);
        HexMapView.LocationPainted += (s, args) =>
            viewModel.HexMapViewModel.PaintLocationCommand.Execute(args);

        // Initialize ViewModel when window loads
        Loaded += async (s, e) =>
        {
            await viewModel.HexMapViewModel.InitializeCommand.ExecuteAsync(null);
        };
    }

    private void Border_ActualThemeVariantChanged(object? sender, System.EventArgs e)
    {
    }
}
