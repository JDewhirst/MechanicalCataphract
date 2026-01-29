using System.Collections.Generic;
using System.Threading.Tasks;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public interface IOrderService : IEntityService<Order>
{
    Task<IList<Order>> GetOrdersByCommanderAsync(int commanderId);
    Task<IList<Order>> GetUnprocessedOrdersAsync();
    Task MarkAsProcessedAsync(int orderId);
}
