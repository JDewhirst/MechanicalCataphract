using System.Collections.Generic;

namespace MechanicalCataphract.Data.Entities;

public class Faction
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#FF0000";
    public string? Rules { get; set; }
    public bool IsPlayerFaction { get; set; }

    // Discord integration
    public ulong? DiscordRoleId { get; set; }
    public ulong? DiscordCategoryId { get; set; }
    public ulong? DiscordChannelId { get; set; }

    // Navigation
    public ICollection<Army> Armies { get; set; } = new List<Army>();
    public ICollection<Commander> Commanders { get; set; } = new List<Commander>();
    public ICollection<MapHex> ControlledHexes { get; set; } = new List<MapHex>();
}
