using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hexes;
using Microsoft.EntityFrameworkCore;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public class ArmyService : IArmyService
{
    private readonly WargameDbContext _context;

    public ArmyService(WargameDbContext context)
    {
        _context = context;
    }

    public async Task<Army?> GetByIdAsync(int id)
    {
        return await _context.Armies
            .Include(a => a.Faction)
            .Include(a => a.Commander)
            .Include(a => a.Location)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IList<Army>> GetAllAsync()
    {
        return await _context.Armies
            .Include(a => a.Faction)
            .Include(a => a.Commander)
            .ToListAsync();
    }

    public async Task<Army> CreateAsync(Army entity)
    {
        _context.Armies.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Army entity)
    {
        _context.Armies.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.Armies.FindAsync(id);
        if (entity != null)
        {
            _context.Armies.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IList<Army>> GetArmiesAtHexAsync(Hex hex)
    {
        return await _context.Armies
            .Include(a => a.Faction)
            .Include(a => a.Commander)
            .Where(a => a.LocationQ == hex.q && a.LocationR == hex.r)
            .ToListAsync();
    }

    public async Task<IList<Army>> GetArmiesByFactionAsync(int factionId)
    {
        return await _context.Armies
            .Include(a => a.Commander)
            .Include(a => a.Brigades)
            .Where(a => a.FactionId == factionId)
            .ToListAsync();
    }

    public async Task<Army?> GetArmyWithBrigadesAsync(int armyId)
    {
        return await _context.Armies
            .Include(a => a.Brigades)
            .Include(a => a.Faction)
            .Include(a => a.Commander)
            .FirstOrDefaultAsync(a => a.Id == armyId);
    }

    public async Task MoveArmyAsync(int armyId, Hex destination)
    {
        var army = await _context.Armies.FindAsync(armyId);
        if (army != null)
        {
            army.LocationQ = destination.q;
            army.LocationR = destination.r;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> CalculateTotalTroopsAsync(int armyId)
    {
        return await _context.Brigades
            .Where(b => b.ArmyId == armyId)
            .SumAsync(b => b.Number);
    }

    public async Task<int> GetMaxScoutingRangeAsync(int armyId)
    {
        var brigades = await _context.Brigades
            .Where(b => b.ArmyId == armyId)
            .ToListAsync();

        return brigades.Count > 0 ? brigades.Max(b => b.ScoutingRange) : 0;
    }
}
