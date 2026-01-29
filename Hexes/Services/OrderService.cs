using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public class OrderService : IOrderService
{
    private readonly WargameDbContext _context;

    public OrderService(WargameDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(int id)
    {
        return await _context.Orders
            .Include(o => o.Commander)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<IList<Order>> GetAllAsync()
    {
        return await _context.Orders
            .Include(o => o.Commander)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<Order> CreateAsync(Order entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        _context.Orders.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Order entity)
    {
        _context.Orders.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.Orders.FindAsync(id);
        if (entity != null)
        {
            _context.Orders.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IList<Order>> GetOrdersByCommanderAsync(int commanderId)
    {
        return await _context.Orders
            .Include(o => o.Commander)
            .Where(o => o.CommanderId == commanderId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<IList<Order>> GetUnprocessedOrdersAsync()
    {
        return await _context.Orders
            .Include(o => o.Commander)
            .Where(o => !o.Processed)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task MarkAsProcessedAsync(int orderId)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order != null)
        {
            order.Processed = true;
            order.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
