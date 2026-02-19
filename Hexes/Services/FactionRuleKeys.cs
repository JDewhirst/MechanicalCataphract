using System.Collections.Generic;

namespace MechanicalCataphract.Services;

/// <summary>
/// String constants for faction-specific game rules, plus descriptions for the UI.
/// Add entries here to expose new per-faction tuning knobs.
/// </summary>
public static class FactionRuleKeys
{
    /// <summary>Messenger speed multiplier when travelling through own-controlled hexes (e.g. 1.5 = 50% faster).</summary>
    public const string OwnTerritoryMessengerMultiplier = "OwnTerritoryMessengerMultiplier";

    /// <summary>If 1, armies treat river edges as roads (affects pathfinding and movement).</summary>
    public const string RiversAsRoads = "RiversAsRoads";

    /// <summary>All defined keys, used to populate the ComboBox in FactionDetail.</summary>
    public static readonly IReadOnlyList<string> AllKeys = new[]
    {
        OwnTerritoryMessengerMultiplier,
        RiversAsRoads
    };

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [OwnTerritoryMessengerMultiplier] = "Messenger speed multiplier in own-controlled hexes (e.g. 1.5 = 50% faster)",
        [RiversAsRoads] = "If 1, armies treat river edges as roads (affects pathfinding and movement)"
    };
}
