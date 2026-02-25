using Avalonia.Controls;
using Avalonia.Interactivity;
using GUI.ViewModels.EntityViewModels;
using MechanicalCataphract.Data.Entities;

namespace GUI.Views;

public partial class NavyDetail : UserControl
{
    public NavyDetail()
    {
        InitializeComponent();
    }

    private void OnEmbarkClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NavyViewModel navyVm) return;
        if (EmbarkArmyComboBox.SelectedItem is Army army)
        {
            navyVm.EmbarkArmyCommand.Execute(army);
        }
    }
}
