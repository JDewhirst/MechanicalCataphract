using System.Collections.Generic;

namespace MechanicalCataphract.Data.Entities;

public class TerrainType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public int BaseMovementCost { get; set; } = 1;
    public bool IsWater { get; set; } = false;
    public string ColorHex { get; set; } = "#FFFFFF";
    public bool IsUseIcon { get; set; } = false;
    public double ScaleFactor { get; set; } = 0.82;

    // Navigation
    public ICollection<MapHex> Hexes { get; set; } = new List<MapHex>();
}
