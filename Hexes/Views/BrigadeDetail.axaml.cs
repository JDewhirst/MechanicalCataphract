using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using GUI.ViewModels.EntityViewModels;
using MechanicalCataphract.Data.Entities;

namespace GUI.Views;

public partial class BrigadeDetail : UserControl
{
    public BrigadeDetail()
    {
        InitializeComponent();

        // Populate the UnitType ComboBox with enum values
        UnitTypeComboBox.ItemsSource = Enum.GetValues<UnitType>();
    }

    private void OnFieldLostFocus(object? sender, RoutedEventArgs e)
    {
        SaveViaParent();
    }

    private void OnComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SaveViaParent();
    }

    private void OnNumericValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        SaveViaParent();
    }

    private void OnDeleteClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not Brigade brigade) return;

        // Find parent ArmyDetail and call its ViewModel's delete command
        var armyDetail = this.FindAncestorOfType<ArmyDetail>();
        if (armyDetail?.DataContext is ArmyViewModel armyVm)
        {
            armyVm.DeleteBrigadeCommand.Execute(brigade);
        }
    }

    private void OnTransferClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not Brigade brigade) return;

        // Find parent ArmyDetail and call its ViewModel's transfer command
        var armyDetail = this.FindAncestorOfType<ArmyDetail>();
        if (armyDetail?.DataContext is ArmyViewModel armyVm)
        {
            armyVm.TransferBrigadeCommand.Execute(brigade);
        }
    }

    private void SaveViaParent()
    {
        // Find parent ArmyDetail and call its ViewModel's save command
        var armyDetail = this.FindAncestorOfType<ArmyDetail>();
        if (armyDetail?.DataContext is ArmyViewModel armyVm)
        {
            armyVm.SaveCommand.Execute(null);
        }
    }
}
