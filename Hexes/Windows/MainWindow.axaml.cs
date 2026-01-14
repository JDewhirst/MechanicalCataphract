using Avalonia.Controls;

namespace GUI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        DataContext = new MainWindowViewModel();
        InitializeComponent();

        // Wire HexMapView events to ViewModel commands
        // Events cannot be bound directly to ICommand in Avalonia XAML
        var vm = (MainWindowViewModel)DataContext;
        HexMapView.HexClicked += (s, hex) =>
            vm.HexMapViewModel.SelectHexCommand.Execute(hex);
        HexMapView.PanCompleted += (s, delta) =>
            vm.HexMapViewModel.CompletePanCommand.Execute(delta);
    }
}
