using System.Collections.Generic;
using System.Linq;
using Hexes;

namespace MechanicalCataphract.Data.Entities;

public class MapHex
{
    // Primary key: composite of Q, R (S is derived as -Q-R)
    public int Q { get; set; }
    public int R { get; set; }

    // Terrain
    public int? TerrainTypeId { get; set; }
    public TerrainType? TerrainType { get; set; }

    // Faction control
    public int? ControllingFactionId { get; set; }
    public Faction? ControllingFaction { get; set; }

    // Roads - stored as comma-separated directions (0-5)
    public string? RoadDirections { get; set; }

    // Rivers - stored as comma-separated edge directions (0-5)
    public string? RiverEdges { get; set; }

    // Weather
    public int? WeatherId { get; set; }
    public Weather? Weather { get; set; }

    // Game state
    public int PopulationDensity { get; set; }
    public int TimesForaged { get; set; }

    // Location (embedded, formerly separate Location entity)
    public string? LocationName { get; set; }
    public int? LocationType { get; set; }  // 0=City, 1=Town, 2=Fort, etc.
    public int? LocationFactionId { get; set; }
    public Faction? LocationFaction { get; set; }

    // Navigation
    public ICollection<Army> Armies { get; set; } = new List<Army>();
    public ICollection<Commander> Commanders { get; set; } = new List<Commander>();

    // Helper to convert to Hex struct
    public Hex ToHex() => new Hex(Q, R, -Q - R);

    // Helper to check if has road in direction
    public bool HasRoadInDirection(int direction)
    {
        if (string.IsNullOrEmpty(RoadDirections)) return false;
        return RoadDirections.Split(',').Contains(direction.ToString());
    }

    // Helper to check if has river on edge
    public bool HasRiverOnEdge(int edge)
    {
        if (string.IsNullOrEmpty(RiverEdges)) return false;
        return RiverEdges.Split(',').Contains(edge.ToString());
    }
}
