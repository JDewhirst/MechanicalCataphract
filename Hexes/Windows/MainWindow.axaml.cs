using Avalonia.Controls;

namespace GUI;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        // Wire HexMapView events to ViewModel commands
        // Events cannot be bound directly to ICommand in Avalonia XAML
        HexMapView.HexClicked += (s, hex) =>
            viewModel.HexMapViewModel.SelectHexCommand.Execute(hex);
        HexMapView.PanCompleted += (s, delta) =>
            viewModel.HexMapViewModel.CompletePanCommand.Execute(delta);
    }
}
