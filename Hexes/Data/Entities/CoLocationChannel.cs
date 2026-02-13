using System.Collections.Generic;

namespace MechanicalCataphract.Data.Entities;

public class CoLocationChannel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ulong? DiscordChannelId { get; set; }

    // Following an army (mobile) — mutually exclusive with hex
    public int? FollowingArmyId { get; set; }
    public Army? FollowingArmy { get; set; }

    // Following a hex (fixed) — mutually exclusive with army
    public int? FollowingHexQ { get; set; }
    public int? FollowingHexR { get; set; }
    public MapHex? FollowingHex { get; set; }

    // Many-to-many: commanders who can see this channel
    public ICollection<Commander> Commanders { get; set; } = new List<Commander>();
}
