using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Discord;
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
    private readonly IDiscordChannelManager? _discordChannelManager;
    private readonly int _mapRows;
    private readonly int _mapCols;

    public string EntityTypeName => "Message";

    public event Action? Saved;

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

    public int? SenderCoordinateQ => _message.SenderCoordinateQ;
    public int? SenderCoordinateR => _message.SenderCoordinateR;

    public int? SenderCol => SenderCoordinateQ == null || SenderCoordinateR == null ? null
        : OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, new Hex(SenderCoordinateQ.Value, SenderCoordinateR.Value, -SenderCoordinateQ.Value - SenderCoordinateR.Value)).col;
    public int? SenderRow => SenderCoordinateQ == null || SenderCoordinateR == null ? null
        : OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, new Hex(SenderCoordinateQ.Value, SenderCoordinateR.Value, -SenderCoordinateQ.Value - SenderCoordinateR.Value)).row;

    public int? TargetCoordinateQ
    {
        get => _message.TargetCoordinateQ;
        set { if (_message.TargetCoordinateQ != value) { _message.TargetCoordinateQ = value; OnPropertyChanged(); OnPropertyChanged(nameof(TargetCol)); _ = SaveAsync(); } }
    }
    public int? TargetCoordinateR
    {
        get => _message.TargetCoordinateR;
        set { if (_message.TargetCoordinateR != value) { _message.TargetCoordinateR = value; OnPropertyChanged(); OnPropertyChanged(nameof(TargetRow)); _ = SaveAsync(); } }
    }

    public int? TargetCol
    {
        get => TargetCoordinateQ == null || TargetCoordinateR == null ? null
             : OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, new Hex(TargetCoordinateQ.Value, TargetCoordinateR.Value, -TargetCoordinateQ.Value - TargetCoordinateR.Value)).col;
        set
        {
            if (value == null) { TargetCoordinateQ = null; TargetCoordinateR = null; return; }
            int row = TargetRow ?? 0;
            if (!IsOffsetInBounds(value.Value, row)) return;
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
            if (!IsOffsetInBounds(col, value.Value)) return;
            var hex = OffsetCoord.QoffsetToCube(OffsetCoord.ODD, new OffsetCoord(col, value.Value));
            TargetCoordinateQ = hex.q; TargetCoordinateR = hex.r;
            OnPropertyChanged();
        }
    }

    public int? CoordinateQ
    {
        get => _message.CoordinateQ;
        set { if (_message.CoordinateQ != value) { _message.CoordinateQ = value; OnPropertyChanged(); OnPropertyChanged(nameof(Col)); _ = SaveAsync(); } }
    }
    public int? CoordinateR
    {
        get => _message.CoordinateR;
        set { if (_message.CoordinateR != value) { _message.CoordinateR = value; OnPropertyChanged(); OnPropertyChanged(nameof(Row)); _ = SaveAsync(); } }
    }

    public int? Col
    {
        get => CoordinateQ == null || CoordinateR == null ? null
             : OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, new Hex(CoordinateQ.Value, CoordinateR.Value, -CoordinateQ.Value - CoordinateR.Value)).col;
        set
        {
            if (value == null) { CoordinateQ = null; CoordinateR = null; return; }
            int row = Row ?? 0;
            if (!IsOffsetInBounds(value.Value, row)) return;
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
            if (!IsOffsetInBounds(col, value.Value)) return;
            var hex = OffsetCoord.QoffsetToCube(OffsetCoord.ODD, new OffsetCoord(col, value.Value));
            CoordinateQ = hex.q; CoordinateR = hex.r;
            OnPropertyChanged();
        }
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

    [ObservableProperty]
    private string? _sendStatus;

    /// <summary>
    /// Sends this message's content to the target commander's Discord channel.
    /// Marks the message as delivered on success.
    /// </summary>
    [RelayCommand]
    private async Task SendToDiscord()
    {
        if (_discordChannelManager == null)
        {
            SendStatus = "Discord not available";
            return;
        }

        if (_message.TargetCommander == null)
        {
            SendStatus = "No target commander set";
            return;
        }

        if (!_message.TargetCommander.DiscordChannelId.HasValue)
        {
            SendStatus = "Target has no Discord channel";
            return;
        }

        if (string.IsNullOrWhiteSpace(_message.Content))
        {
            SendStatus = "Message has no content";
            return;
        }

        SendStatus = "Sending...";

        try
        {
            var senderName = _message.SenderCommander?.Name ?? "Unknown";
            var formatted = $"**Message from {senderName}:**\n{_message.Content}";
            await _discordChannelManager.SendMessageToCommanderChannelAsync(_message.TargetCommander, formatted);

            Delivered = true;
            SendStatus = "Sent";
        }
        catch (Exception ex)
        {
            SendStatus = $"Failed: {ex.Message}";
        }
    }

    private bool IsOffsetInBounds(int col, int row)
        => col >= 0 && col < _mapCols && row >= 0 && row < _mapRows;

    private async Task SaveAsync()
    {
        await _service.UpdateAsync(_message);
        Saved?.Invoke();
    }

    public MessageViewModel(
        Message message, IMessageService service, IEnumerable<Commander> availableCommanders,
        int mapRows = int.MaxValue, int mapCols = int.MaxValue,
        IPathfindingService? pathfindingService = null, IDiscordChannelManager? discordChannelManager = null)
    {
        _message = message;
        _service = service;
        _availableCommanders = availableCommanders;
        _mapRows = mapRows;
        _mapCols = mapCols;
        _pathfindingService = pathfindingService;
        _discordChannelManager = discordChannelManager;
    }
}
