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
    private readonly IFactionRuleService _factionRuleService;

    public ArmyService(WargameDbContext context, IFactionRuleService factionRuleService)
    {
        _context = context;
        _factionRuleService = factionRuleService;
    }

    public async Task<Army?> GetByIdAsync(int id)
    {
        return await _context.Armies
            .WithDetailIncludes()
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IList<Army>> GetAllAsync()
    {
        return await _context.Armies
            .WithStandardIncludes()
            .WithBrigades()
            .ToListAsync();
    }

    public async Task<Army> CreateAsync(Army entity)
    {
        if (entity.CoordinateQ == null && entity.CoordinateR == null)
        {
            entity.CoordinateQ = MapHex.SentinelQ;
            entity.CoordinateR = MapHex.SentinelR;
        }
        await entity.ValidateCoordinatesAsync(_context);
        _context.Armies.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Army entity)
    {
        await entity.ValidateCoordinatesAsync(_context);
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
            .WithStandardIncludes()
            .Where(a => a.CoordinateQ == hex.q && a.CoordinateR == hex.r)
            .ToListAsync();
    }

    public async Task<IList<Army>> GetArmiesByFactionAsync(int factionId)
    {
        return await _context.Armies
            .Include(a => a.Commander)
            .WithBrigades()
            .Where(a => a.FactionId == factionId)
            .ToListAsync();
    }

    public async Task<Army?> GetArmyWithBrigadesAsync(int armyId)
    {
        return await _context.Armies
            .WithStandardIncludes()
            .WithBrigades()
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
        brigade.SortOrder = await GetNextBrigadeSortOrderAsync(targetArmyId);
        await _context.SaveChangesAsync();
    }

    public async Task MoveArmyAsync(int armyId, Hex destination)
    {
        await CoordinateValidator.ValidateCoordinatesAsync(_context, destination.q, destination.r, "Destination");
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
        brigade.SortOrder = await GetNextBrigadeSortOrderAsync(brigade.ArmyId);
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
            var armyId = brigade.ArmyId;
            _context.Brigades.Remove(brigade);
            await _context.SaveChangesAsync();
            await NormalizeBrigadeSortOrdersAsync(armyId);
        }
    }

    public async Task UpdateBrigadeOrderAsync(int armyId, IReadOnlyList<int> orderedBrigadeIds)
    {
        var brigades = await _context.Brigades
            .Where(b => b.ArmyId == armyId)
            .ToListAsync();

        var orderedIds = orderedBrigadeIds.ToHashSet();
        var brigadeById = brigades.ToDictionary(b => b.Id);
        for (var i = 0; i < orderedBrigadeIds.Count; i++)
        {
            if (brigadeById.TryGetValue(orderedBrigadeIds[i], out var brigade))
            {
                brigade.SortOrder = i;
            }
        }

        var nextOrder = orderedBrigadeIds.Count;
        foreach (var brigade in brigades
                     .Where(b => !orderedIds.Contains(b.Id))
                     .OrderBy(b => b.SortOrder)
                     .ThenBy(b => b.Id))
        {
            brigade.SortOrder = nextOrder++;
        }

        await _context.SaveChangesAsync();
    }

    private async Task<int> GetNextBrigadeSortOrderAsync(int armyId)
    {
        var hasBrigades = await _context.Brigades.AnyAsync(b => b.ArmyId == armyId);
        if (!hasBrigades) return 0;

        return await _context.Brigades
            .Where(b => b.ArmyId == armyId)
            .MaxAsync(b => b.SortOrder) + 1;
    }

    private async Task NormalizeBrigadeSortOrdersAsync(int armyId)
    {
        var brigades = await _context.Brigades
            .Where(b => b.ArmyId == armyId)
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.Id)
            .ToListAsync();

        for (var i = 0; i < brigades.Count; i++)
        {
            brigades[i].SortOrder = i;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<int> GetDailySupplyConsumptionAsync(int armyId)
    {
        var army = await GetArmyWithBrigadesAsync(armyId);
        return army.Brigades.Sum(b => b.Number * b.UnitType.SupplyConsumptionPerMan())
            + (UnitType.Infantry.SupplyConsumptionPerMan() * army.NonCombatants)
            + (int)(GameRules.Current.Supply.WagonSupplyMultiplier * army.Wagons);
    }


}
