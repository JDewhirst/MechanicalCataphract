using System.Collections.Generic;

namespace MechanicalCataphract.Data.Entities;

public class Weather
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public double MovementModifier { get; set; } = 1.0;
    public double CombatModifier { get; set; } = 1.0;

    // Navigation
    public ICollection<MapHex> AffectedHexes { get; set; } = new List<MapHex>();
}
