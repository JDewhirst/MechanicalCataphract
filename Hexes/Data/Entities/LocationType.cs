namespace MechanicalCataphract.Data.Entities;

public class LocationType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IconPath { get; set; }

    // Display color (hex format like "#FF0000")
    public string? ColorHex { get; set; }
}
