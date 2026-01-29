using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MechanicalCataphract.Data.Entities;

namespace GUI.Windows;

public partial class TransferBrigadeDialog : Window
{
    private readonly List<Army> _availableArmies;

    public Army? SelectedArmy { get; private set; }

    public TransferBrigadeDialog()
    {
        InitializeComponent();
        _availableArmies = new List<Army>();
    }

    public TransferBrigadeDialog(IEnumerable<Army> allArmies, int excludeArmyId, string brigadeName)
        : this()
    {
        // Filter out the source army
        _availableArmies = allArmies.Where(a => a.Id != excludeArmyId).ToList();

        // Set up UI
        BrigadeNameText.Text = $"Moving: {brigadeName}";
        ArmyListBox.ItemsSource = _availableArmies;

        if (_availableArmies.Count == 0)
        {
            ArmyListBox.IsVisible = false;
            NoArmiesText.IsVisible = true;
        }
        else
        {
            ArmyListBox.SelectionChanged += OnSelectionChanged;
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        OkButton.IsEnabled = ArmyListBox.SelectedItem != null;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        SelectedArmy = ArmyListBox.SelectedItem as Army;
        Close(SelectedArmy);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
