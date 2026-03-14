using System.Collections.Generic;
using System.Linq;
using Hexes;

namespace MechanicalCataphract.Data.Entities;

public class MapHex
{
    // Sentinel coordinates for the off-board "Torment Hexagon" (entities not yet placed on map)
    // These cube coords correspond to offset col=-5, row=-5 (just off the top-left of the real grid)
    public const int SentinelQ = -5;
    public const int SentinelR = -2;

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
    private string? _roadDirections;
    public string? RoadDirections
    {
        get => _roadDirections;
        set { _roadDirections = value; _roadDirectionSet = null; }
    }

    // Rivers - stored as comma-separated edge directions (0-5)
    private string? _riverEdges;
    public string? RiverEdges
    {
        get => _riverEdges;
        set { _riverEdges = value; _riverEdgeSet = null; }
    }

    // Weather
    public int? WeatherId { get; set; }
    public Weather? Weather { get; set; }

    // Game state
    public int PopulationDensity { get; set; }
    public int TimesForaged { get; set; }

    // Location (embedded, formerly separate Location entity)
    public string? LocationName { get; set; }
    public int? LocationTypeId { get; set; }
    public LocationType? LocationType { get; set; }
    public int? LocationSupply { get; set; }
    public int? LocationFactionId { get; set; }
    public Faction? LocationFaction { get; set; }

    // Free-text notes
    public string? Notes { get; set; }

    // Navigation
    public ICollection<Army> Armies { get; set; } = new List<Army>();
    public ICollection<Commander> Commanders { get; set; } = new List<Commander>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();

    // Helper to convert to Hex struct
    public Hex ToHex() => new Hex(Q, R, -Q - R);

    // Cached parsed direction sets — lazily built, invalidated when string properties change
    private HashSet<int>? _roadDirectionSet;
    private HashSet<int>? _riverEdgeSet;

    public bool HasRoadInDirection(int direction) => GetRoadDirectionSet().Contains(direction);
    public bool HasRiverOnEdge(int edge) => GetRiverEdgeSet().Contains(edge);

    private HashSet<int> GetRoadDirectionSet() => _roadDirectionSet ??= ParseDirections(_roadDirections);
    private HashSet<int> GetRiverEdgeSet() => _riverEdgeSet ??= ParseDirections(_riverEdges);

    private static HashSet<int> ParseDirections(string? csv)
    {
        if (string.IsNullOrEmpty(csv)) return new HashSet<int>();
        var set = new HashSet<int>();
        foreach (var part in csv.Split(','))
            if (int.TryParse(part.Trim(), out var d)) set.Add(d);
        return set;
    }
}
