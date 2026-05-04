using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GUI.ViewModels;
using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GUI.ViewModels.HexMap;

public partial class NewsDropCoordinator : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Func<ObservableCollection<Faction>> _getFactions;
    private readonly Func<long> _getCurrentWorldHour;
    private readonly Action<string> _setCurrentTool;
    private readonly Action<string> _setStatusMessage;

    [ObservableProperty]
    private ObservableCollection<NewsItem> _items = new();

    [ObservableProperty]
    private NewsItem? _selectedItem;

    [ObservableProperty]
    private ObservableCollection<Hex> _reachedHexes = new();

    public int ReachedHexCount => ReachedHexes.Count;

    public NewsDropCoordinator(
        IServiceScopeFactory scopeFactory,
        Func<ObservableCollection<Faction>> getFactions,
        Func<long> getCurrentWorldHour,
        Action<string> setCurrentTool,
        Action<string> setStatusMessage)
    {
        _scopeFactory = scopeFactory;
        _getFactions = getFactions;
        _getCurrentWorldHour = getCurrentWorldHour;
        _setCurrentTool = setCurrentTool;
        _setStatusMessage = setStatusMessage;
    }

    public async Task RefreshAsync()
    {
        var newsItems = await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<INewsService>().GetAllAsync());
        Items = new ObservableCollection<NewsItem>(newsItems);
    }

    [RelayCommand]
    public void ActivateDropTool()
    {
        _setCurrentTool("NewsDrop");
        _setStatusMessage("Tool: NewsDrop - click a hex to drop an event");
    }

    public async Task DropAsync(Hex hex)
    {
        if (App.MainWindow == null) return;
        var gameState = await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<IGameStateService>().GetGameStateAsync());

        var dialog = new GUI.Windows.NewsDropDialog(hex, _getFactions());
        var result = await dialog.ShowDialog<Dictionary<int, string>?>(App.MainWindow);

        _setCurrentTool("Pan");
        _setStatusMessage("Tool: Pan");

        if (result == null || result.Count == 0) return;

        await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<INewsService>().CreateEventAsync(dialog.ResultTitle ?? string.Empty, hex.q, hex.r, gameState.CurrentWorldHour, result));
        await RefreshAsync();
        _setStatusMessage($"Event dropped at ({hex.q}, {hex.r})");
    }

    [RelayCommand]
    public async Task DeleteAsync(NewsItem? item)
    {
        if (item == null) return;
        await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<INewsService>().DeleteEventAsync(item.Id));
        if (SelectedItem?.Id == item.Id)
            SelectedItem = null;
        await RefreshAsync();
        _setStatusMessage($"Deleted event {item.Id}");
    }

    [RelayCommand]
    public async Task EditAsync(NewsItem? item)
    {
        if (item == null || App.MainWindow == null) return;
        var factions = await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<IFactionService>().GetAllAsync());
        var dialog = new GUI.Windows.NewsDropDialog(item, factions);
        var result = await dialog.ShowDialog<Dictionary<int, string>?>(App.MainWindow);

        if (result == null) return;

        item.Title = dialog.ResultTitle ?? item.Title;
        item.FactionMessages = result;
        await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<INewsService>().UpdateAsync(item));
        await RefreshAsync();
        _setStatusMessage($"Updated event: {item.Title}");
    }

    [RelayCommand]
    public async Task DeactivateAsync(NewsItem? item)
    {
        if (item == null) return;
        await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<INewsService>().DeactivateEventAsync(item.Id));
        await RefreshAsync();
        _setStatusMessage($"Deactivated event {item.Id}");
    }

    [RelayCommand]
    public async Task ReactivateAsync(NewsItem? item)
    {
        if (item == null) return;
        await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<INewsService>().ReactivateEventAsync(item.Id));
        await RefreshAsync();
        _setStatusMessage($"Reactivated event: {item.Title}");
    }

    partial void OnSelectedItemChanged(NewsItem? value)
    {
        if (value == null || value.HexArrivals == null)
        {
            ReachedHexes = new ObservableCollection<Hex>();
            OnPropertyChanged(nameof(ReachedHexCount));
            return;
        }

        double elapsedHours = _getCurrentWorldHour() - value.CreatedAtWorldHour;
        var reached = value.HexArrivals
            .Where(a => a.Hours <= elapsedHours)
            .Select(a => new Hex(a.Q, a.R, -a.Q - a.R))
            .ToList();

        ReachedHexes = new ObservableCollection<Hex>(reached);
        OnPropertyChanged(nameof(ReachedHexCount));
    }
}
