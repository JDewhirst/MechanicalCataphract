using System.Collections.Generic;
using System.Threading.Tasks;
using Hexes;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public interface INavyService : IEntityService<Navy>
{
    Task<Navy?> GetNavyWithShipsAsync(int navyId);
    Task<IList<Navy>> GetNaviesByCommanderAsync(int commanderId);
    Task<IList<Navy>> GetNaviesAtHexAsync(Hex hex);
    Task<Ship> AddShipAsync(Ship ship);
    Task DeleteShipAsync(int shipId);
    Task EmbarkArmyAsync(int navyId, int armyId);
    Task DisembarkArmyAsync(int armyId);
}
