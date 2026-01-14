using CommunityToolkit.Mvvm.ComponentModel;
using GUI.ViewModels;

namespace GUI;

public partial class MainWindowViewModel : ObservableObject
{
    public HexMapViewModel HexMapViewModel { get; }

    [ObservableProperty]
    private string? _status;

    public MainWindowViewModel(HexMapViewModel hexMapViewModel)
    {
        HexMapViewModel = hexMapViewModel;
    }
}
