using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using GUI.ViewModels.EntityViewModels;
using MechanicalCataphract.Data.Entities;

namespace GUI.Views;

public partial class ShipDetail : UserControl
{
    public ShipDetail()
    {
        InitializeComponent();
        ShipTypeComboBox.ItemsSource = Enum.GetValues<ShipType>();
    }

    private void OnFieldLostFocus(object? sender, RoutedEventArgs e)
    {
        SaveViaParent();
    }

    private void OnComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SaveViaParent();
    }

    private void OnDeleteClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not Ship ship) return;

        var navyDetail = this.FindAncestorOfType<NavyDetail>();
        if (navyDetail?.DataContext is NavyViewModel navyVm)
        {
            navyVm.DeleteShipCommand.Execute(ship);
        }
    }

    private void SaveViaParent()
    {
        var navyDetail = this.FindAncestorOfType<NavyDetail>();
        if (navyDetail?.DataContext is NavyViewModel navyVm)
        {
            navyVm.SaveCommand.Execute(null);
        }
    }
}
