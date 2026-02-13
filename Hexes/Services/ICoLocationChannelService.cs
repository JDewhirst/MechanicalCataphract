using System.Collections.Generic;
using System.Threading.Tasks;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public interface ICoLocationChannelService : IEntityService<CoLocationChannel>
{
    Task<CoLocationChannel?> GetWithCommandersAsync(int id);
    Task<IList<CoLocationChannel>> GetAllWithCommandersAsync();
    Task<IList<CoLocationChannel>> GetChannelsForCommanderAsync(int commanderId);
    Task AddCommanderAsync(int channelId, int commanderId);
    Task RemoveCommanderAsync(int channelId, int commanderId);

    /// <summary>
    /// Returns the effective (Q, R) location of a co-location channel,
    /// resolved from either the followed army's position or the followed hex.
    /// </summary>
    Task<(int Q, int R)?> GetChannelLocationAsync(int channelId);

    /// <summary>
    /// Checks all of a commander's co-location channels and removes them
    /// from any where they are no longer at the channel's location.
    /// Returns the list of channels the commander was removed from.
    /// </summary>
    Task<IList<CoLocationChannel>> EnforceProximityAsync(Commander commander);
}
