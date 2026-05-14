using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Hexes;
using GUI.ViewModels.EntityViewModels;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GUI.ViewModels.HexMap;

public partial class PathSelectionCoordinator : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Func<IEntityViewModel?> _getSelectedEntityViewModel;
    private readonly Action<IEntityViewModel?> _setSelectedEntityViewModel;
    private readonly Func<Message?> _getSelectedMessage;
    private readonly Func<Army?> _getSelectedArmy;
    private readonly Func<Navy?> _getSelectedNavy;
    private readonly Func<Commander?> _getSelectedCommander;
    private readonly Func<Message, MessageViewModel> _createMessageViewModel;
    private readonly Func<Army, ArmyViewModel> _createArmyViewModel;
    private readonly Func<Navy, NavyViewModel> _createNavyViewModel;
    private readonly Func<Commander, CommanderViewModel> _createCommanderViewModel;
    private readonly Func<Task> _refreshMessagesAsync;
    private readonly Action<string> _setStatusMessage;
    private readonly Action<string> _setCurrentTool;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private IPathMovable? _target;

    [ObservableProperty]
    private ObservableCollection<Hex> _hexes = new();

    public int Count => Hexes.Count;

    public PathSelectionCoordinator(
        IServiceScopeFactory scopeFactory,
        Func<IEntityViewModel?> getSelectedEntityViewModel,
        Action<IEntityViewModel?> setSelectedEntityViewModel,
        Func<Message?> getSelectedMessage,
        Func<Army?> getSelectedArmy,
        Func<Navy?> getSelectedNavy,
        Func<Commander?> getSelectedCommander,
        Func<Message, MessageViewModel> createMessageViewModel,
        Func<Army, ArmyViewModel> createArmyViewModel,
        Func<Navy, NavyViewModel> createNavyViewModel,
        Func<Commander, CommanderViewModel> createCommanderViewModel,
        Func<Task> refreshMessagesAsync,
        Action<string> setStatusMessage,
        Action<string> setCurrentTool)
    {
        _scopeFactory = scopeFactory;
        _getSelectedEntityViewModel = getSelectedEntityViewModel;
        _setSelectedEntityViewModel = setSelectedEntityViewModel;
        _getSelectedMessage = getSelectedMessage;
        _getSelectedArmy = getSelectedArmy;
        _getSelectedNavy = getSelectedNavy;
        _getSelectedCommander = getSelectedCommander;
        _createMessageViewModel = createMessageViewModel;
        _createArmyViewModel = createArmyViewModel;
        _createNavyViewModel = createNavyViewModel;
        _createCommanderViewModel = createCommanderViewModel;
        _refreshMessagesAsync = refreshMessagesAsync;
        _setStatusMessage = setStatusMessage;
        _setCurrentTool = setCurrentTool;
    }

    public void Start(Message message) => StartForEntity(message, "Message");
    public void Start(Army army) => StartForEntity(army, "Army");
    public void Start(Navy navy) => StartForEntity(navy, "Navy");
    public void Start(Commander commander) => StartForEntity(commander, "Commander");

    private void StartForEntity(IPathMovable entity, string entityName)
    {
        if (entity.CoordinateQ == null || entity.CoordinateR == null)
        {
            _setStatusMessage($"{entityName} must have a location before selecting a path");
            return;
        }

        Target = entity;
        Hexes.Clear();
        IsActive = true;
        _setCurrentTool("PathSelect");
        _setStatusMessage("Click hexes to build path (must be adjacent)");
        SyncSelectedPathViewModel(0, true);
        OnPropertyChanged(nameof(Count));
    }

    public void AddHex(Hex hex)
    {
        if (Target == null) return;

        Hex lastHex = Hexes.Count == 0
            ? new Hex(Target.CoordinateQ!.Value, Target.CoordinateR!.Value, -Target.CoordinateQ!.Value - Target.CoordinateR!.Value)
            : Hexes.Last();

        if (!IsAdjacent(lastHex, hex))
        {
            _setStatusMessage("Hex must be adjacent to previous hex in path");
            return;
        }

        Hexes.Add(hex);
        OnPropertyChanged(nameof(Count));
        _setStatusMessage($"Path: {Count} hex(es)");
        SyncSelectedPathViewModel(Hexes.Count, true);
    }

    public async Task ConfirmAsync()
    {
        if (Target == null)
        {
            Cancel();
            return;
        }

        int pathLength = Hexes.Count;
        Target.Path = pathLength == 0 ? null : Hexes.ToList();

        if (Target is Message msg)
        {
            await _scopeFactory.InScopeAsync(sp =>
                sp.GetRequiredService<IMessageService>().UpdateAsync(msg));
            await _refreshMessagesAsync();
            var selected = _getSelectedMessage();
            if (selected != null)
                _setSelectedEntityViewModel(_createMessageViewModel(selected));
        }
        else if (Target is Army army)
        {
            await _scopeFactory.InScopeAsync(sp =>
                sp.GetRequiredService<IArmyService>().UpdateAsync(army));
            var selected = _getSelectedArmy();
            if (selected != null)
                _setSelectedEntityViewModel(_createArmyViewModel(selected));
        }
        else if (Target is Navy navy)
        {
            await _scopeFactory.InScopeAsync(sp =>
                sp.GetRequiredService<INavyService>().UpdateAsync(navy));
            var selected = _getSelectedNavy();
            if (selected != null)
                _setSelectedEntityViewModel(_createNavyViewModel(selected));
        }
        else if (Target is Commander commander)
        {
            await _scopeFactory.InScopeAsync(sp =>
                sp.GetRequiredService<ICommanderService>().UpdateAsync(commander));
            var selected = _getSelectedCommander();
            if (selected != null)
                _setSelectedEntityViewModel(_createCommanderViewModel(selected));
        }

        Cancel();
        _setStatusMessage(pathLength > 0 ? $"Path set: {pathLength} hex(es)" : "Path cleared");
    }

    public void Cancel()
    {
        SyncSelectedPathViewModel(0, false);
        Hexes.Clear();
        Target = null;
        IsActive = false;
        _setCurrentTool("Pan");
        OnPropertyChanged(nameof(Count));
    }

    private void SyncSelectedPathViewModel(int count, bool isActive)
    {
        if (_getSelectedEntityViewModel() is IPathSelectableViewModel pathVm)
        {
            pathVm.IsPathSelectionActive = isActive;
            pathVm.PathSelectionCount = count;
        }
    }

    private static bool IsAdjacent(Hex a, Hex b)
    {
        for (int dir = 0; dir < 6; dir++)
        {
            var neighbor = a.Neighbor(dir);
            if (neighbor.q == b.q && neighbor.r == b.r)
                return true;
        }

        return false;
    }
}
