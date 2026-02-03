using System.Collections.Generic;
using Hexes;

namespace MechanicalCataphract.Data.Entities;

/// <summary>
/// Interface for entities that can move along a path on the hex map.
/// </summary>
public interface IPathMovable
{
    /// <summary>Current location Q coordinate.</summary>
    int? LocationQ { get; set; }

    /// <summary>Current location R coordinate.</summary>
    int? LocationR { get; set; }

    /// <summary>Target location Q coordinate for pathfinding.</summary>
    int? TargetLocationQ { get; set; }

    /// <summary>Target location R coordinate for pathfinding.</summary>
    int? TargetLocationR { get; set; }

    /// <summary>
    /// The planned path as a FIFO queue. Path[0] is the next waypoint.
    /// </summary>
    List<Hex>? Path { get; set; }

    /// <summary>
    /// Movement rate in miles per hour.
    /// </summary>
    float MovementRate { get; }
}
