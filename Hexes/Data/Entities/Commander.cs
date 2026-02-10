using System.Collections.Generic;
using System.Linq;
using Hexes;

namespace MechanicalCataphract.Data.Entities;

public class Commander : IPathMovable
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Age { get; set; }
    public string? DiscordHandle { get; set; }
    public ulong? DiscordUserId { get; set; }
    public ulong? DiscordChannelId { get; set; }

    // Traits stored as comma-separated trait IDs
    public string? TraitIds { get; set; }

    // Faction
    public int FactionId { get; set; }
    public Faction? Faction { get; set; }

    // Coordinate (FK to MapHex)
    public int? CoordinateQ { get; set; }
    public int? CoordinateR { get; set; }
    public MapHex? MapHex { get; set; }

    // Target coordinate for pathfinding
    public int? TargetCoordinateQ { get; set; }
    public int? TargetCoordinateR { get; set; }

    // Movement
    public List<Hex>? Path { get; set; }
    public float TimeInTransit { get; set; }
    public float MovementRate => 2f;

    // Following Army (physical location coupling, distinct from commanding)
    public int? FollowingArmyId { get; set; }
    public Army? FollowingArmy { get; set; }

    // Navigation
    public ICollection<Army> CommandedArmies { get; set; } = new List<Army>();

    // Computed property for DataGrid display
    public string? CommandingArmyName => CommandedArmies?.FirstOrDefault()?.Name;
}
