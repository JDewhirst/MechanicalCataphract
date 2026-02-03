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

    public int? LocationQ
    {
        get => _commander.LocationQ;
        set { if (_commander.LocationQ != value) { _commander.LocationQ = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }
    public int? LocationR
    {
        get => _commander.LocationR;
        set { if (_commander.LocationR != value) { _commander.LocationR = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int? TargetLocationQ
    {
        get => _commander.TargetLocationQ;
        set { if (_commander.TargetLocationQ != value) { _commander.TargetLocationQ = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int? TargetLocationR
    {
        get => _commander.TargetLocationR;
        set { if (_commander.TargetLocationR != value) { _commander.TargetLocationR = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public List<Hex>? Path
    {
        get => _commander.Path;
        set { if (_commander.Path != value) { _commander.Path = value; OnPropertyChanged(); OnPropertyChanged(nameof(PathLength)); _ = SaveAsync(); } }
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

        if (LocationQ == null || LocationR == null)
        {
            PathComputeStatus = "Current location not set";
            return;
        }

        if (TargetLocationQ == null || TargetLocationR == null)
        {
            PathComputeStatus = "Target location not set";
            return;
        }

        PathComputeStatus = "Computing...";

        var start = new Hex(LocationQ.Value, LocationR.Value, -LocationQ.Value - LocationR.Value);
        var end = new Hex(TargetLocationQ.Value, TargetLocationR.Value, -TargetLocationQ.Value - TargetLocationR.Value);

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

    public Faction? Faction => _commander.Faction;

    private async Task SaveAsync()
    {
        await _service.UpdateAsync(_commander);
    }

    public CommanderViewModel(Commander commander, ICommanderService service, IPathfindingService? pathfindingService = null)
    {
        _commander = commander;
        _service = service;
        _pathfindingService = pathfindingService;
    }
}
