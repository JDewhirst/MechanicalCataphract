using System.Collections.Generic;
using System.Threading.Tasks;
using Hexes;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public interface IArmyService : IEntityService<Army>
{
    Task<IList<Army>> GetArmiesAtHexAsync(Hex hex);
    Task<IList<Army>> GetArmiesByFactionAsync(int factionId);
    Task<Army?> GetArmyWithBrigadesAsync(int armyId);
    Task MoveArmyAsync(int armyId, Hex destination);
    Task<int> CalculateTotalTroopsAsync(int armyId);
    Task<int> GetMaxScoutingRangeAsync(int armyId);
}
