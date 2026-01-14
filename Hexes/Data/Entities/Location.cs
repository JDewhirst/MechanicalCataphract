namespace MechanicalCataphract.Data.Entities;

public class Location
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Location type (ID for enum: City, Town, Fort, etc.)
    public int LocationType { get; set; }
    public string? IconPath { get; set; }

    // Position (FK to MapHex)
    public int HexQ { get; set; }
    public int HexR { get; set; }
    public MapHex? Hex { get; set; }

    // Owning faction
    public int? FactionId { get; set; }
    public Faction? Faction { get; set; }

    // Properties
    public int Population { get; set; }
    public int DefenseBonus { get; set; }
}
