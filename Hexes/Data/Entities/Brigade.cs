namespace MechanicalCataphract.Data.Entities;

public class Brigade
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Parent army
    public int ArmyId { get; set; }
    public Army? Army { get; set; }

    // Optional separate faction (can differ from army)
    public int? FactionId { get; set; }
    public Faction? Faction { get; set; }

    // Unit type
    public UnitType UnitType { get; set; } = UnitType.Infantry;

    // Stats
    public int Number { get; set; }

    // Computed from UnitType — Cavalry scouts farther
    public int ScoutingRange => UnitType.ScoutingRange();
}
