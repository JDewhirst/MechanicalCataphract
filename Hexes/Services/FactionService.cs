using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public class FactionService : IFactionService
{
    private readonly WargameDbContext _context;

    public FactionService(WargameDbContext context)
    {
        _context = context;
    }

    public async Task<Faction?> GetByIdAsync(int id)
    {
        return await _context.Factions.FindAsync(id);
    }

    public async Task<IList<Faction>> GetAllAsync()
    {
        return await _context.Factions.ToListAsync();
    }

    public async Task<Faction> CreateAsync(Faction entity)
    {
        _context.Factions.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Faction entity)
    {
        _context.Factions.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.Factions.FindAsync(id);
        if (entity != null)
        {
            _context.Factions.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Faction?> GetFactionWithArmiesAndCommandersAsync(int factionId)
    {
        return await _context.Factions
            .Include(f => f.Armies)
            .Include(f => f.Commanders)
            .FirstOrDefaultAsync(f => f.Id == factionId);
    }

    public async Task<Faction?> GetFactionByNameAsync(string name)
    {
        return await _context.Factions
            .FirstOrDefaultAsync(f => f.Name == name);
    }
}
