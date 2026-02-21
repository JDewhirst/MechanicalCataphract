using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Hexes;
using MechanicalCataphract.Data.Entities;

namespace GUI.Windows;

public partial class NewsDropDialog : Window
{
    private readonly List<(int factionId, TextBox textBox)> _factionInputs = new();

    /// <summary>Result: factionId → message text (only non-empty entries).</summary>
    public Dictionary<int, string>? Result { get; private set; }

    /// <summary>The title entered by the referee.</summary>
    public string? ResultTitle { get; private set; }

    public NewsDropDialog()
    {
        InitializeComponent();
    }

    public NewsDropDialog(Hex originHex, IEnumerable<Faction> factions)
        : this()
    {
        TitleText.Text = $"Drop Event at ({originHex.q}, {originHex.r})";

        foreach (var faction in factions)
            AddFactionRow(faction, "");
    }

    public NewsDropDialog(NewsItem existing, IEnumerable<Faction> factions)
        : this()
    {
        TitleText.Text = $"Edit Event at ({existing.OriginQ}, {existing.OriginR})";
        TitleInputBox.Text = existing.Title;
        OkButton.Content = "Save Changes";

        foreach (var faction in factions)
        {
            string? existingMsg = null;
            existing.FactionMessages?.TryGetValue(faction.Id, out existingMsg);
            AddFactionRow(faction, existingMsg ?? "");
        }
    }

    private void AddFactionRow(Faction faction, string prefilledText)
    {
        var swatch = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new Avalonia.CornerRadius(2),
            Background = SolidColorBrush.Parse(faction.ColorHex ?? "#808080")
        };

        var label = new TextBlock
        {
            Text = faction.Name,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            FontSize = 12,
            Width = 90
        };

        var textBox = new TextBox
        {
            AcceptsReturn = true,
            MaxHeight = 80,
            Watermark = "Message for this faction (leave blank to skip)…",
            TextWrapping = TextWrapping.Wrap,
            Text = prefilledText,
            [Grid.ColumnProperty] = 2
        };
        textBox.TextChanged += (_, _) => UpdateOkButton();

        _factionInputs.Add((faction.Id, textBox));

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,8,*")
        };

        var swatchStack = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        swatchStack.Children.Add(swatch);
        swatchStack.Children.Add(label);

        row.Children.Add(swatchStack);
        row.Children.Add(textBox);

        FactionPanel.Children.Add(row);
    }

    private void UpdateOkButton()
    {
        OkButton.IsEnabled = _factionInputs.Any(f => !string.IsNullOrWhiteSpace(f.textBox.Text));
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        ResultTitle = TitleInputBox.Text?.Trim();
        Result = _factionInputs
            .Where(f => !string.IsNullOrWhiteSpace(f.textBox.Text))
            .ToDictionary(f => f.factionId, f => f.textBox.Text!.Trim());
        Close(Result);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
