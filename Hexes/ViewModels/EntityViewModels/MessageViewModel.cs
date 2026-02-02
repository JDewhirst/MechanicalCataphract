using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GUI.ViewModels.EntityViewModels;

/// <summary>
/// ViewModel wrapper for Message entity with auto-save on property change.
/// </summary>
public partial class MessageViewModel : ObservableObject, IEntityViewModel
{
    private readonly Message _message;
    private readonly IMessageService _service;
    private readonly IPathfindingService? _pathfindingService;

    public string EntityTypeName => "Message";

    /// <summary>
    /// The underlying entity (for bindings that need direct access).
    /// </summary>
    public Message Entity => _message;

    public int Id => _message.Id;

    private readonly IEnumerable<Commander> _availableCommanders;
    public IEnumerable<Commander> AvailableCommanders => _availableCommanders;

    public Commander? SenderCommander
    {
        get => _message.SenderCommander;
        set
        {
            if (_message.SenderCommander != value)
            {
                _message.SenderCommander = value;
                _message.SenderCommanderId = value?.Id;
                OnPropertyChanged();
                _ = SaveAsync();
            }
        }
    }

    public Commander? TargetCommander
    {
        get => _message.TargetCommander;
        set
        {
            if (_message.TargetCommander != value)
            {
                _message.TargetCommander = value;
                _message.TargetCommanderId = value?.Id;
                OnPropertyChanged();
                _ = SaveAsync();
            }
        }
    }

    public int? SenderLocationQ => _message.SenderLocationQ;
    public int? SenderLocationR => _message.SenderLocationR;

    public int? TargetLocationQ
    {
        get => _message.TargetLocationQ;
        set { if (_message.TargetLocationQ != value) { _message.TargetLocationQ = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }
    public int? TargetLocationR
    {
        get => _message.TargetLocationR;
        set { if (_message.TargetLocationR != value) { _message.TargetLocationR = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int? LocationQ
    {
        get => _message.LocationQ;
        set { if (_message.LocationQ != value) { _message.LocationQ = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }
    public int? LocationR
    {
        get => _message.LocationR;
        set { if (_message.LocationR != value) { _message.LocationR = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public string Content
    {
        get => _message.Content;
        set { if (_message.Content != value) { _message.Content = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public bool Delivered
    {
        get => _message.Delivered;
        set
        {
            if (_message.Delivered != value)
            {
                _message.Delivered = value;
                if (value) _message.DeliveredAt = DateTime.UtcNow;
                OnPropertyChanged();
                _ = SaveAsync();
            }
        }
    }

    public DateTime CreatedAt => _message.CreatedAt;
    public DateTime? DeliveredAt => _message.DeliveredAt;

    public List<Hex>? Path
    {
        get => _message.Path;
        set { if (_message.Path != value) { _message.Path = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int PathLength => _message.Path?.Count ?? 0;

    // Path selection mode state (set by HexMapViewModel)
    [ObservableProperty]
    private bool _isPathSelectionActive;

    [ObservableProperty]
    private int _pathSelectionCount;

    [ObservableProperty]
    private string? _pathComputeStatus;

    /// <summary>
    /// Event raised when user wants to select a path for this message.
    /// HexMapViewModel subscribes to this to enter path selection mode.
    /// </summary>
    public event Action<Message>? PathSelectionRequested;

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
        PathSelectionRequested?.Invoke(_message);
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

        var result = await _pathfindingService.FindPathAsync(start, end, TravelEntityType.Message);

        if (result.Success)
        {
            Path = result.Path.ToList();
            OnPropertyChanged(nameof(PathLength));
            PathComputeStatus = $"Path found: {result.Path.Count} hexes, cost {result.TotalCost}";
        }
        else
        {
            PathComputeStatus = result.FailureReason ?? "Path computation failed";
        }
    }

    private async Task SaveAsync()
    {
        await _service.UpdateAsync(_message);
    }

    public MessageViewModel(Message message, IMessageService service, IEnumerable<Commander> availableCommanders, IPathfindingService? pathfindingService = null)
    {
        _message = message;
        _service = service;
        _availableCommanders = availableCommanders;
        _pathfindingService = pathfindingService;
    }
}
