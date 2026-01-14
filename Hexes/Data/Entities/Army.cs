using System.Collections.Generic;

namespace MechanicalCataphract.Data.Entities;

public class Army
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Location (composite FK to MapHex)
    public int LocationQ { get; set; }
    public int LocationR { get; set; }
    public MapHex? Location { get; set; }

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
