using System;
using System.Collections.Generic;
using System.Linq;
using Hexes;
using MechanicalCataphract.Services;

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
    public float MovementRate => (float)GameRules.Current.MovementRates.ArmyBaseRate;

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
    public bool IsNightMarching { get; set; }
    public bool IsForcedMarch { get; set; }
    public int ForcedMarchHours { get; set; }

    // Computed: total marching column length in abstract units
    public int MarchingColumnLength
    {
        get
        {
            int footTroops = (Brigades?.Where(b => b.UnitType.CountsForFordingLength()).Sum(b => b.Number) ?? 0) + NonCombatants;
            int cavalry    =  Brigades?.Where(b => !b.UnitType.CountsForFordingLength()).Sum(b => b.Number) ?? 0;
            return (int)Math.Ceiling(footTroops / (double)UnitType.Infantry.MarchingColumnCapacity())
                 + (int)Math.Ceiling(cavalry    / (double)UnitType.Cavalry.MarchingColumnCapacity())
                 + (int)Math.Ceiling(Wagons / 50.0);
        }
    }

    // Computed: column length for fording (excludes cavalry — they ford at regular speed)
    public int FordingColumnLength
    {
        get
        {
            int footTroops = (Brigades?.Where(b => b.UnitType.CountsForFordingLength()).Sum(b => b.Number) ?? 0) + NonCombatants;
            return (int)Math.Ceiling(footTroops / (double)UnitType.Infantry.MarchingColumnCapacity())
                 + (int)Math.Ceiling(Wagons / 50.0);
        }
    }

    // Navigation
    public ICollection<Brigade> Brigades { get; set; } = new List<Brigade>();

    // Computed properties for DataGrid display
    public int CombatStrength => Brigades?.Sum(b => b.Number * b.UnitType.CombatPowerPerMan()) ?? 0;
    public int DailySupplyConsumption => (Brigades?.Sum(b => b.Number * b.UnitType.SupplyConsumptionPerMan()) ?? 0)
        + NonCombatants + (int)(Wagons * GameRules.Current.Supply.WagonSupplyMultiplier);
    public double DaysOfSupply => DailySupplyConsumption > 0 ? (double)CarriedSupply / DailySupplyConsumption : 0;
}
