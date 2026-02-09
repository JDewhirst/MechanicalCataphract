using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hexes;
using Microsoft.EntityFrameworkCore;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;
using GUI.ViewModels.EntityViewModels;

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
            .Include(a => a.MapHex)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IList<Army>> GetAllAsync()
    {
        return await _context.Armies
            .Include(a => a.Faction)
            .Include(a => a.Commander)
            .Include(a => a.Brigades)
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
            .Where(a => a.CoordinateQ == hex.q && a.CoordinateR == hex.r)
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

    public async Task TransferBrigadeAsync(int brigadeId, int targetArmyId)
    {
        var brigade = await _context.Brigades.FindAsync(brigadeId);
        if (brigade == null) return;

        var targetArmy = await _context.Armies.FindAsync(targetArmyId);
        if (targetArmy == null) return;

        brigade.ArmyId = targetArmyId;
        brigade.FactionId = targetArmy.FactionId;  // Brigade inherits target army's faction
        await _context.SaveChangesAsync();
    }

    public async Task MoveArmyAsync(int armyId, Hex destination)
    {
        var army = await _context.Armies.FindAsync(armyId);
        if (army != null)
        {
            army.CoordinateQ = destination.q;
            army.CoordinateR = destination.r;
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

    public async Task<Brigade> AddBrigadeAsync(Brigade brigade)
    {
        _context.Brigades.Add(brigade);
        await _context.SaveChangesAsync();
        return brigade;
    }

    public async Task UpdateBrigadeAsync(Brigade brigade)
    {
        _context.Brigades.Update(brigade);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteBrigadeAsync(int brigadeId)
    {
        var brigade = await _context.Brigades.FindAsync(brigadeId);
        if (brigade != null)
        {
            _context.Brigades.Remove(brigade);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> GetDailySupplyConsumptionAsync(int armyId)
    {
        var army = await GetArmyWithBrigadesAsync(armyId);
        return army.Brigades.Sum(b => b.Number * GetUnitSupplyConsumption(b.UnitType)) + (GetUnitSupplyConsumption(UnitType.Infantry) * army.NonCombatants) + (GetUnitSupplyConsumption(UnitType.Cavalry) * army.Wagons); ;
    }

    private static int GetUnitSupplyConsumption(UnitType unitType) => unitType switch
    {
        UnitType.Infantry => 1,
        UnitType.Skirmishers => 1,
        UnitType.Cavalry => 10,
        _ => 0
    };


}
