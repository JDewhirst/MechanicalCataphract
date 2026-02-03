using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public class MessageService : IMessageService
{
    private readonly WargameDbContext _context;

    public MessageService(WargameDbContext context)
    {
        _context = context;
    }

    public async Task<Message?> GetByIdAsync(int id)
    {
        return await _context.Messages
            .Include(m => m.SenderCommander)
            .Include(m => m.TargetCommander)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IList<Message>> GetAllAsync()
    {
        return await _context.Messages
            .Include(m => m.SenderCommander)
            .Include(m => m.TargetCommander)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<Message> CreateAsync(Message entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        _context.Messages.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Message entity)
    {
        _context.Messages.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.Messages.FindAsync(id);
        if (entity != null)
        {
            _context.Messages.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IList<Message>> GetMessagesBySenderAsync(int commanderId)
    {
        return await _context.Messages
            .Include(m => m.SenderCommander)
            .Include(m => m.TargetCommander)
            .Where(m => m.SenderCommanderId == commanderId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IList<Message>> GetMessagesByTargetAsync(int commanderId)
    {
        return await _context.Messages
            .Include(m => m.SenderCommander)
            .Include(m => m.TargetCommander)
            .Where(m => m.TargetCommanderId == commanderId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IList<Message>> GetUndeliveredMessagesAsync()
    {
        return await _context.Messages
            .Include(m => m.SenderCommander)
            .Include(m => m.TargetCommander)
            .Where(m => !m.Delivered)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task MarkAsDeliveredAsync(int messageId)
    {
        var message = await _context.Messages.FindAsync(messageId);
        if (message != null)
        {
            message.Delivered = true;
            message.DeliveredAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

}
