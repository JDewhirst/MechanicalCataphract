using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GUI.ViewModels;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GUI.ViewModels.EntityViewModels;

/// <summary>
/// ViewModel wrapper for Order entity with auto-save on property change.
/// </summary>
public partial class OrderViewModel : ObservableObject, IEntityViewModel
{
    private readonly Order _order;
    private readonly IServiceScopeFactory _scopeFactory;

    public string EntityTypeName => "Order";

    public event Action? Saved;

    /// <summary>
    /// The underlying entity (for bindings that need direct access).
    /// </summary>
    public Order Entity => _order;

    public int Id => _order.Id;

    private readonly IEnumerable<Commander> _availableCommanders;
    public IEnumerable<Commander> AvailableCommanders => _availableCommanders;

    public Commander? Commander
    {
        get => AvailableCommanders.FirstOrDefault(c => c.Id == _order.CommanderId) ?? _order.Commander;
        set
        {
            if (value == null || (_order.CommanderId == value.Id && _order.Commander == value)) return;

            _order.Commander = value;
            _order.CommanderId = value.Id;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CommanderId));
            OnPropertyChanged(nameof(CommanderName));
            _ = SaveAsync();
        }
    }

    public int? CommanderId
    {
        get => _order.CommanderId;
        set
        {
            if (value == null || _order.CommanderId == value) return;

            _order.CommanderId = value.Value;
            _order.Commander = AvailableCommanders.FirstOrDefault(c => c.Id == value.Value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Commander));
            OnPropertyChanged(nameof(CommanderName));
            _ = SaveAsync();
        }
    }

    public string? CommanderName => _order.Commander?.Name;

    public string Contents
    {
        get => _order.Contents;
        set { if (_order.Contents != value) { _order.Contents = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public bool Processed
    {
        get => _order.Processed;
        set
        {
            if (_order.Processed != value)
            {
                _order.Processed = value;
                if (value) _order.ProcessedAt = DateTime.UtcNow;
                OnPropertyChanged();
                _ = SaveAsync();
            }
        }
    }

    public DateTime CreatedAt => _order.CreatedAt;
    public DateTime? ProcessedAt => _order.ProcessedAt;

    public IAsyncRelayCommand SaveCommand { get; }

    private async Task SaveAsync()
    {
        await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<IOrderService>().UpdateAsync(_order));
        Saved?.Invoke();
    }

    public OrderViewModel(Order order, IServiceScopeFactory scopeFactory, IEnumerable<Commander> availableCommanders)
    {
        _order = order;
        _scopeFactory = scopeFactory;
        _availableCommanders = availableCommanders;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }
}
