using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GUI.Windows;

public partial class NewMapDialog : Window
{
    public int Rows { get; private set; } = 30;
    public int Columns { get; private set; } = 30;

    public NewMapDialog()
    {
        InitializeComponent();
    }

    private void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        Rows = (int)(RowsInput.Value ?? 30);
        Columns = (int)(ColsInput.Value ?? 30);
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
