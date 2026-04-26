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
            // Check if we're in path selection mode
            if (viewModel.HexMapViewModel.PathSelection.IsActive)
            {
                viewModel.HexMapViewModel.PathSelection.AddHex(hex);
                return;
            }

            // Check if we're in muster selection mode
            if (viewModel.HexMapViewModel.IsMusterModeActive)
            {
                viewModel.HexMapViewModel.ToggleMusterHexSelection(hex);
                return;
            }

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

        // Wire muster drag-painting (add-only, no toggle)
        HexMapView.MusterHexDragged += (s, hex) =>
        {
            viewModel.HexMapViewModel.AddMusterHexSelection(hex);
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

        HexMapView.MessageClicked += (s, message) =>
        {
            viewModel.HexMapViewModel.SelectedMessage = (message);
        };

        HexMapView.NavyClicked += (s, navy) =>
        {
            viewModel.HexMapViewModel.SelectedNavy = navy;
        };
        HexMapView.PanCompleted += (s, delta) =>
            viewModel.HexMapViewModel.CompletePanCommand.Execute(delta);
        HexMapView.TerrainPainted += (s, args) =>
            _ = viewModel.HexMapViewModel.Editing.PaintTerrainAsync(args.hex, args.terrainTypeId);
        HexMapView.RoadPainted += (s, hex) =>
            _ = viewModel.HexMapViewModel.Editing.PaintRoadAsync(hex);
        HexMapView.RiverPainted += (s, hex) =>
            _ = viewModel.HexMapViewModel.Editing.PaintRiverAsync(hex);
        HexMapView.EraseRequested += (s, hex) =>
            _ = viewModel.HexMapViewModel.Editing.EraseAsync(hex);
        HexMapView.LocationPainted += (s, args) =>
            _ = viewModel.HexMapViewModel.Editing.PaintLocationAsync(args.hex, args.locationName);
        HexMapView.PopulationPainted += (s, hex) =>
            _ = viewModel.HexMapViewModel.Editing.PaintPopulationAsync(hex);
        HexMapView.FactionControlPainted += (s, hex) =>
            _ = viewModel.HexMapViewModel.Editing.PaintFactionControlAsync(hex);
        HexMapView.WeatherPainted += (s, hex) =>
            _ = viewModel.HexMapViewModel.Editing.PaintWeatherAsync(hex);
        HexMapView.NewsDropRequested += (s, hex) =>
            _ = viewModel.HexMapViewModel.News.DropAsync(hex);

        // Initialize ViewModel when window loads
        Loaded += async (s, e) =>
        {
            await viewModel.HexMapViewModel.InitializeCommand.ExecuteAsync(null);
        };
    }

    private void Border_ActualThemeVariantChanged(object? sender, System.EventArgs e)
    {
    }

    private void HexMapView_ActualThemeVariantChanged(object? sender, System.EventArgs e)
    {
    }
}
