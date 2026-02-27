using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public class CommanderService : ICommanderService
{
    private readonly WargameDbContext _context;

    public CommanderService(WargameDbContext context)
    {
        _context = context;
    }

    public async Task<Commander?> GetByIdAsync(int id)
    {
        return await _context.Commanders
            .Include(c => c.Faction)
            .Include(c => c.FollowingArmy)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IList<Commander>> GetAllAsync()
    {
        return await _context.Commanders
            .Include(c => c.Faction)
            .Include(c => c.FollowingArmy)
            .Include(c => c.CommandedArmies)
            .ToListAsync();
    }

    public async Task<Commander> CreateAsync(Commander entity)
    {
        if (entity.CoordinateQ == null && entity.CoordinateR == null)
        {
            entity.CoordinateQ = MapHex.SentinelQ;
            entity.CoordinateR = MapHex.SentinelR;
        }
        await CoordinateValidator.ValidateCoordinatesAsync(_context, entity.CoordinateQ, entity.CoordinateR, "Location");
        await CoordinateValidator.ValidateCoordinatesAsync(_context, entity.TargetCoordinateQ, entity.TargetCoordinateR, "TargetCoordinate");
        _context.Commanders.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Commander entity)
    {
        await CoordinateValidator.ValidateCoordinatesAsync(_context, entity.CoordinateQ, entity.CoordinateR, "Location");
        await CoordinateValidator.ValidateCoordinatesAsync(_context, entity.TargetCoordinateQ, entity.TargetCoordinateR, "TargetCoordinate");
        _context.Commanders.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.Commanders.FindAsync(id);
        if (entity != null)
        {
            _context.Commanders.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Commander?> GetByDiscordIdAsync(ulong discordUserId)
    {
        return await _context.Commanders
            .Include(c => c.Faction)
            .FirstOrDefaultAsync(c => c.DiscordUserId == discordUserId);
    }

    public async Task<IList<Commander>> GetCommandersByFactionAsync(int factionId)
    {
        return await _context.Commanders
            .Where(c => c.FactionId == factionId)
            .ToListAsync();
    }

    public async Task<Commander?> GetCommanderWithArmiesAsync(int commanderId)
    {
        return await _context.Commanders
            .Include(c => c.CommandedArmies)
            .ThenInclude(a => a.Brigades)
            .Include(c => c.Faction)
            .Include(c => c.FollowingArmy)
            .FirstOrDefaultAsync(c => c.Id == commanderId);
    }

    public async Task<IList<Commander>> GetCommandersFollowingArmyAsync(int armyId)
    {
        return await _context.Commanders
            .Where(c => c.FollowingArmyId == armyId)
            .ToListAsync();
    }
}
