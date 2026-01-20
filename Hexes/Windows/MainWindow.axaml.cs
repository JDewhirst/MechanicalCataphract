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
            viewModel.HexMapViewModel.SelectHexCommand.Execute(hex);
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
}
