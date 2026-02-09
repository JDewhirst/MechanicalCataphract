using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace GUI.ViewModels.EntityViewModels;

/// <summary>
/// ViewModel wrapper for Commander entity with auto-save on property change.
/// </summary>
public partial class CommanderViewModel : ObservableObject, IEntityViewModel
{
    private readonly Commander _commander;
    private readonly ICommanderService _service;
    private readonly IPathfindingService? _pathfindingService;

    public string EntityTypeName => "Commander";

    public IEnumerable<Army> AvailableArmies { get; }
    public IEnumerable<Faction> AvailableFactions { get; }

    /// <summary>
    /// The underlying entity (for bindings that need direct access).
    /// </summary>
    public Commander Entity => _commander;

    public int Id => _commander.Id;

    public string Name
    {
        get => _commander.Name;
        set { if (_commander.Name != value) { _commander.Name = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int? Age
    {
        get => _commander.Age;
        set { if (_commander.Age != value) { _commander.Age = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public string? DiscordHandle
    {
        get => _commander.DiscordHandle;
        set { if (_commander.DiscordHandle != value) { _commander.DiscordHandle = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int? CoordinateQ
    {
        get => _commander.CoordinateQ;
        set { if (_commander.CoordinateQ != value) { _commander.CoordinateQ = value; OnPropertyChanged(); OnPropertyChanged(nameof(Col)); _ = SaveAsync(); } }
    }
    public int? CoordinateR
    {
        get => _commander.CoordinateR;
        set { if (_commander.CoordinateR != value) { _commander.CoordinateR = value; OnPropertyChanged(); OnPropertyChanged(nameof(Row)); _ = SaveAsync(); } }
    }

    public int? Col
    {
        get => CoordinateQ == null || CoordinateR == null ? null
             : OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, new Hex(CoordinateQ.Value, CoordinateR.Value, -CoordinateQ.Value - CoordinateR.Value)).col;
        set
        {
            if (value == null) { CoordinateQ = null; CoordinateR = null; return; }
            int row = Row ?? 0;
            var hex = OffsetCoord.QoffsetToCube(OffsetCoord.ODD, new OffsetCoord(value.Value, row));
            CoordinateQ = hex.q; CoordinateR = hex.r;
            OnPropertyChanged();
        }
    }

    public int? Row
    {
        get => CoordinateQ == null || CoordinateR == null ? null
             : OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, new Hex(CoordinateQ.Value, CoordinateR.Value, -CoordinateQ.Value - CoordinateR.Value)).row;
        set
        {
            if (value == null) { CoordinateQ = null; CoordinateR = null; return; }
            int col = Col ?? 0;
            var hex = OffsetCoord.QoffsetToCube(OffsetCoord.ODD, new OffsetCoord(col, value.Value));
            CoordinateQ = hex.q; CoordinateR = hex.r;
            OnPropertyChanged();
        }
    }

    public int? TargetCoordinateQ
    {
        get => _commander.TargetCoordinateQ;
        set { if (_commander.TargetCoordinateQ != value) { _commander.TargetCoordinateQ = value; OnPropertyChanged(); OnPropertyChanged(nameof(TargetCol)); _ = SaveAsync(); } }
    }

    public int? TargetCoordinateR
    {
        get => _commander.TargetCoordinateR;
        set { if (_commander.TargetCoordinateR != value) { _commander.TargetCoordinateR = value; OnPropertyChanged(); OnPropertyChanged(nameof(TargetRow)); _ = SaveAsync(); } }
    }

    public int? TargetCol
    {
        get => TargetCoordinateQ == null || TargetCoordinateR == null ? null
             : OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, new Hex(TargetCoordinateQ.Value, TargetCoordinateR.Value, -TargetCoordinateQ.Value - TargetCoordinateR.Value)).col;
        set
        {
            if (value == null) { TargetCoordinateQ = null; TargetCoordinateR = null; return; }
            int row = TargetRow ?? 0;
            var hex = OffsetCoord.QoffsetToCube(OffsetCoord.ODD, new OffsetCoord(value.Value, row));
            TargetCoordinateQ = hex.q; TargetCoordinateR = hex.r;
            OnPropertyChanged();
        }
    }

    public int? TargetRow
    {
        get => TargetCoordinateQ == null || TargetCoordinateR == null ? null
             : OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, new Hex(TargetCoordinateQ.Value, TargetCoordinateR.Value, -TargetCoordinateQ.Value - TargetCoordinateR.Value)).row;
        set
        {
            if (value == null) { TargetCoordinateQ = null; TargetCoordinateR = null; return; }
            int col = TargetCol ?? 0;
            var hex = OffsetCoord.QoffsetToCube(OffsetCoord.ODD, new OffsetCoord(col, value.Value));
            TargetCoordinateQ = hex.q; TargetCoordinateR = hex.r;
            OnPropertyChanged();
        }
    }

    public List<Hex>? Path
    {
        get => _commander.Path;
        set
        {
            if (_commander.Path != value)
            {
                _commander.Path = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PathLength));

                // Mutual exclusivity: assigning a path clears following army
                if (value != null && value.Count > 0 && _commander.FollowingArmyId != null)
                {
                    _commander.FollowingArmy = null;
                    _commander.FollowingArmyId = null;
                    OnPropertyChanged(nameof(FollowingArmy));
                }

                _ = SaveAsync();
            }
        }
    }

    public int PathLength => _commander.Path?.Count ?? 0;

    // Path selection mode state
    [ObservableProperty]
    private bool _isPathSelectionActive;

    [ObservableProperty]
    private int _pathSelectionCount;

    [ObservableProperty]
    private string? _pathComputeStatus;

    /// <summary>
    /// Event raised when user wants to select a path for this commander.
    /// HexMapViewModel subscribes to this to enter path selection mode.
    /// </summary>
    public event Action<Commander>? PathSelectionRequested;

    /// <summary>
    /// Event raised when user confirms path selection.
    /// </summary>
    public event Func<Task>? PathSelectionConfirmRequested;

    /// <summary>
    /// Event raised when user cancels path selection.
    /// </summary>
    public event Action? PathSelectionCancelRequested;

    [RelayCommand]
    private void SelectPath()
    {
        PathSelectionRequested?.Invoke(_commander);
    }

    [RelayCommand]
    private async Task ConfirmPathSelection()
    {
        if (PathSelectionConfirmRequested != null)
            await PathSelectionConfirmRequested.Invoke();
    }

    [RelayCommand]
    private void CancelPathSelection()
    {
        PathSelectionCancelRequested?.Invoke();
    }

    [RelayCommand]
    private async Task ComputePath()
    {
        if (_pathfindingService == null)
        {
            PathComputeStatus = "Pathfinding not available";
            return;
        }

        if (CoordinateQ == null || CoordinateR == null)
        {
            PathComputeStatus = "Current location not set";
            return;
        }

        if (TargetCoordinateQ == null || TargetCoordinateR == null)
        {
            PathComputeStatus = "Target location not set";
            return;
        }

        PathComputeStatus = "Computing...";

        var start = new Hex(CoordinateQ.Value, CoordinateR.Value, -CoordinateQ.Value - CoordinateR.Value);
        var end = new Hex(TargetCoordinateQ.Value, TargetCoordinateR.Value, -TargetCoordinateQ.Value - TargetCoordinateR.Value);

        var result = await _pathfindingService.FindPathAsync(start, end, TravelEntityType.Commander);

        if (result.Success)
        {
            Path = result.Path.ToList();
            PathComputeStatus = $"Path found: {result.Path.Count} hexes, cost {result.TotalCost}";
        }
        else
        {
            PathComputeStatus = result.FailureReason ?? "Path computation failed";
        }
    }

    public Faction? Faction
    {
        get => _commander.Faction;
        set
        {
            if (_commander.Faction != value)
            {
                _commander.Faction = value;
                _commander.FactionId = value?.Id ?? 1;
                OnPropertyChanged();
                _ = SaveAsync();
            }
        }
    }

    public Army? FollowingArmy
    {
        get => _commander.FollowingArmy;
        set
        {
            if (_commander.FollowingArmy != value)
            {
                _commander.FollowingArmy = value;
                _commander.FollowingArmyId = value?.Id;
                OnPropertyChanged();

                // Mutual exclusivity: following an army clears independent path
                if (value != null)
                {
                    _commander.Path = null;
                    _commander.TargetCoordinateQ = null;
                    _commander.TargetCoordinateR = null;
                    _commander.TimeInTransit = 0;
                    OnPropertyChanged(nameof(Path));
                    OnPropertyChanged(nameof(PathLength));
                    OnPropertyChanged(nameof(TargetCoordinateQ));
                    OnPropertyChanged(nameof(TargetCoordinateR));
                    OnPropertyChanged(nameof(TargetCol));
                    OnPropertyChanged(nameof(TargetRow));
                }

                _ = SaveAsync();
            }
        }
    }

    private async Task SaveAsync()
    {
        await _service.UpdateAsync(_commander);
    }

    public CommanderViewModel(Commander commander, ICommanderService service, IEnumerable<Army> availableArmies, IEnumerable<Faction> availableFactions, IPathfindingService? pathfindingService = null)
    {
        _commander = commander;
        _service = service;
        AvailableArmies = availableArmies;
        AvailableFactions = availableFactions;
        _pathfindingService = pathfindingService;
    }
}
