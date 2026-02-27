using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hexes;
using Microsoft.EntityFrameworkCore;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public class NavyService : INavyService
{
    private readonly WargameDbContext _context;

    public NavyService(WargameDbContext context)
    {
        _context = context;
    }

    public async Task<Navy?> GetByIdAsync(int id)
    {
        return await _context.Navies
            .Include(n => n.Faction)
            .Include(n => n.Commander)
            .Include(n => n.MapHex)
            .Include(n => n.Ships)
            .FirstOrDefaultAsync(n => n.Id == id);
    }

    public async Task<IList<Navy>> GetAllAsync()
    {
        return await _context.Navies
            .Include(n => n.Faction)
            .Include(n => n.Commander)
            .Include(n => n.Ships)
            .ToListAsync();
    }

    public async Task<Navy> CreateAsync(Navy entity)
    {
        if (entity.FactionId == 0)
            entity.FactionId = 1;
        if (entity.CoordinateQ == null && entity.CoordinateR == null)
        {
            entity.CoordinateQ = MapHex.SentinelQ;
            entity.CoordinateR = MapHex.SentinelR;
        }
        await CoordinateValidator.ValidateCoordinatesAsync(_context, entity.CoordinateQ, entity.CoordinateR, "Location");
        _context.Navies.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Navy entity)
    {
        await CoordinateValidator.ValidateCoordinatesAsync(_context, entity.CoordinateQ, entity.CoordinateR, "Location");
        _context.Navies.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.Navies.FindAsync(id);
        if (entity != null)
        {
            _context.Navies.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Navy?> GetNavyWithShipsAsync(int navyId)
    {
        return await _context.Navies
            .Include(n => n.Faction)
            .Include(n => n.Ships)
            .Include(n => n.Commander)
            .Include(n => n.CarriedArmy)
                .ThenInclude(a => a!.Brigades)
            .FirstOrDefaultAsync(n => n.Id == navyId);
    }

    public async Task<IList<Navy>> GetNaviesByCommanderAsync(int commanderId)
    {
        return await _context.Navies
            .Where(n => n.CommanderId == commanderId)
            .Include(n => n.Faction)
            .Include(n => n.Ships)
            .ToListAsync();
    }

    public async Task<IList<Navy>> GetNaviesAtHexAsync(Hex hex)
    {
        return await _context.Navies
            .Where(n => n.CoordinateQ == hex.q && n.CoordinateR == hex.r)
            .Include(n => n.Faction)
            .Include(n => n.Commander)
            .Include(n => n.Ships)
            .ToListAsync();
    }

    public async Task<Ship> AddShipAsync(Ship ship)
    {
        _context.Ships.Add(ship);
        await _context.SaveChangesAsync();
        return ship;
    }

    public async Task DeleteShipAsync(int shipId)
    {
        var ship = await _context.Ships.FindAsync(shipId);
        if (ship != null)
        {
            _context.Ships.Remove(ship);
            await _context.SaveChangesAsync();
        }
    }

    public async Task EmbarkArmyAsync(int navyId, int armyId)
    {
        var army = await _context.Armies.FindAsync(armyId);
        if (army == null) return;
        army.NavyId = navyId;
        await _context.SaveChangesAsync();
    }

    public async Task DisembarkArmyAsync(int armyId)
    {
        var army = await _context.Armies.FindAsync(armyId);
        if (army == null) return;
        army.NavyId = null;
        await _context.SaveChangesAsync();
    }
}
