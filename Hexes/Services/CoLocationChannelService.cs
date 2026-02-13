using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public class CoLocationChannelService : ICoLocationChannelService
{
    private readonly WargameDbContext _context;

    public CoLocationChannelService(WargameDbContext context)
    {
        _context = context;
    }

    public async Task<CoLocationChannel?> GetByIdAsync(int id)
    {
        return await _context.CoLocationChannels
            .Include(c => c.FollowingArmy)
            .Include(c => c.FollowingHex)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IList<CoLocationChannel>> GetAllAsync()
    {
        return await _context.CoLocationChannels
            .Include(c => c.FollowingArmy)
            .Include(c => c.Commanders)
            .ToListAsync();
    }

    public async Task<CoLocationChannel> CreateAsync(CoLocationChannel entity)
    {
        _context.CoLocationChannels.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(CoLocationChannel entity)
    {
        _context.CoLocationChannels.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.CoLocationChannels.FindAsync(id);
        if (entity != null)
        {
            _context.CoLocationChannels.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<CoLocationChannel?> GetWithCommandersAsync(int id)
    {
        return await _context.CoLocationChannels
            .Include(c => c.Commanders)
            .Include(c => c.FollowingArmy)
            .Include(c => c.FollowingHex)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IList<CoLocationChannel>> GetAllWithCommandersAsync()
    {
        return await _context.CoLocationChannels
            .Include(c => c.Commanders)
            .Include(c => c.FollowingArmy)
            .Include(c => c.FollowingHex)
            .ToListAsync();
    }

    public async Task<IList<CoLocationChannel>> GetChannelsForCommanderAsync(int commanderId)
    {
        return await _context.CoLocationChannels
            .Include(c => c.Commanders)
            .Include(c => c.FollowingArmy)
            .Include(c => c.FollowingHex)
            .Where(c => c.Commanders.Any(cmd => cmd.Id == commanderId))
            .ToListAsync();
    }

    public async Task AddCommanderAsync(int channelId, int commanderId)
    {
        var channel = await _context.CoLocationChannels
            .Include(c => c.Commanders)
            .FirstOrDefaultAsync(c => c.Id == channelId);
        if (channel == null) return;

        var commander = await _context.Commanders.FindAsync(commanderId);
        if (commander == null) return;

        if (!channel.Commanders.Any(c => c.Id == commanderId))
        {
            channel.Commanders.Add(commander);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveCommanderAsync(int channelId, int commanderId)
    {
        var channel = await _context.CoLocationChannels
            .Include(c => c.Commanders)
            .FirstOrDefaultAsync(c => c.Id == channelId);
        if (channel == null) return;

        var commander = channel.Commanders.FirstOrDefault(c => c.Id == commanderId);
        if (commander != null)
        {
            channel.Commanders.Remove(commander);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<(int Q, int R)?> GetChannelLocationAsync(int channelId)
    {
        var channel = await _context.CoLocationChannels
            .Include(c => c.FollowingArmy)
            .FirstOrDefaultAsync(c => c.Id == channelId);
        if (channel == null) return null;

        // Army-following: use army's current coordinates
        if (channel.FollowingArmyId != null && channel.FollowingArmy != null)
        {
            if (channel.FollowingArmy.CoordinateQ != null && channel.FollowingArmy.CoordinateR != null)
                return (channel.FollowingArmy.CoordinateQ.Value, channel.FollowingArmy.CoordinateR.Value);
            return null;
        }

        // Hex-following: use stored coordinates
        if (channel.FollowingHexQ != null && channel.FollowingHexR != null)
            return (channel.FollowingHexQ.Value, channel.FollowingHexR.Value);

        return null;
    }

    public async Task<IList<CoLocationChannel>> EnforceProximityAsync(Commander commander)
    {
        var removed = new List<CoLocationChannel>();

        if (commander.CoordinateQ == null || commander.CoordinateR == null)
            return removed;

        var channels = await GetChannelsForCommanderAsync(commander.Id);
        foreach (var channel in channels)
        {
            var location = await GetChannelLocationAsync(channel.Id);
            if (location == null) continue;

            // Commander is no longer at the channel's location
            if (commander.CoordinateQ != location.Value.Q || commander.CoordinateR != location.Value.R)
            {
                await RemoveCommanderAsync(channel.Id, commander.Id);
                removed.Add(channel);
            }
        }

        return removed;
    }
}
