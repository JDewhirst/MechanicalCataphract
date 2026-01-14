using System.Collections.Generic;
using System.Threading.Tasks;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public interface ICommanderService : IEntityService<Commander>
{
    Task<Commander?> GetByDiscordIdAsync(ulong discordUserId);
    Task<IList<Commander>> GetCommandersByFactionAsync(int factionId);
    Task<Commander?> GetCommanderWithArmiesAsync(int commanderId);
}
