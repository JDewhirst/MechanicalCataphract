using System.Threading.Tasks;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public interface IFactionService : IEntityService<Faction>
{
    Task<Faction?> GetFactionWithArmiesAsync(int factionId);
    Task<Faction?> GetFactionWithCommandersAsync(int factionId);
    Task<Faction?> GetFactionByNameAsync(string name);
}
