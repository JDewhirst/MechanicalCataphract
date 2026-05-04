using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using GUI.ViewModels;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using Microsoft.Extensions.DependencyInjection;

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

    public Commander? Commander => _order.Commander;

    /// <summary>
    /// Gets the commander name directly, avoiding deep traversal of the entity graph.
    /// </summary>
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

    private async Task SaveAsync()
    {
        await _scopeFactory.InScopeAsync(sp =>
            sp.GetRequiredService<IOrderService>().UpdateAsync(_order));
        Saved?.Invoke();
    }

    public OrderViewModel(Order order, IServiceScopeFactory scopeFactory)
    {
        _order = order;
        _scopeFactory = scopeFactory;
    }
}
