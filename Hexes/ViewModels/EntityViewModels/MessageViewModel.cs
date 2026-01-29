using CommunityToolkit.Mvvm.ComponentModel;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GUI.ViewModels.EntityViewModels;

/// <summary>
/// ViewModel wrapper for Message entity with auto-save on property change.
/// </summary>
public partial class MessageViewModel : ObservableObject, IEntityViewModel
{
    private readonly Message _message;
    private readonly IMessageService _service;

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
    public int? TargetLocationQ => _message.TargetLocationQ;
    public int? TargetLocationR => _message.TargetLocationR;

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

    private async Task SaveAsync()
    {
        await _service.UpdateAsync(_message);
    }

    public MessageViewModel(Message message, IMessageService service, IEnumerable<Commander> availableCommanders)
    {
        _message = message;
        _service = service;
        _availableCommanders = availableCommanders;
    }
}
