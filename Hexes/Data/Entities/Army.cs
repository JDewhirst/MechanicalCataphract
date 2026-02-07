using System.Collections.Generic;
using Hexes;

namespace MechanicalCataphract.Data.Entities;

public class Army : IPathMovable
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Coordinate (composite FK to MapHex)
    public int? CoordinateQ { get; set; }
    public int? CoordinateR { get; set; }
    public MapHex? MapHex { get; set; }

    // Target coordinate for pathfinding
    public int? TargetCoordinateQ { get; set; }
    public int? TargetCoordinateR { get; set; }

    // Movement
    public List<Hex>? Path { get; set; }
    public float TimeInTransit { get; set; }
    public float MovementRate => 0.5f;

    // Ownership
    public int FactionId { get; set; }
    public Faction? Faction { get; set; }

    // Command
    public int? CommanderId { get; set; }
    public Commander? Commander { get; set; }

    // Stats from wargame plan
    public int Morale { get; set; } = 9;
    public int Wagons { get; set; }
    public double BaseNoncombatantsPercentage { get; set; } = 0.25;
    public int NonCombatants { get; set; }
    public int CarriedSupply { get; set; }
    public int CarriedLoot { get; set; }
    public int CarriedCoins { get; set; }
    public bool IsGarrison { get; set; }
    public bool IsResting { get; set; }

    // Navigation
    public ICollection<Brigade> Brigades { get; set; } = new List<Brigade>();
}
