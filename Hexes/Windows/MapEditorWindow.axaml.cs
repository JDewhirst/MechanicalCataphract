using Avalonia.Controls;
using GUI.ViewModels;

namespace GUI;

public partial class MapEditorWindow : Window
{
    public MapEditorWindow()
    {
        InitializeComponent();
    }

    public MapEditorWindow(HexMapViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
