using System.Collections.Generic;
using Hexes;

namespace MechanicalCataphract.Data.Entities;

/// <summary>
/// Interface for entities that can move along a path on the hex map.
/// </summary>
public interface IPathMovable
{
    /// <summary>Current coordinate Q (cube coordinate FK to MapHex).</summary>
    int? CoordinateQ { get; set; }

    /// <summary>Current coordinate R (cube coordinate FK to MapHex).</summary>
    int? CoordinateR { get; set; }

    /// <summary>Target coordinate Q for pathfinding.</summary>
    int? TargetCoordinateQ { get; set; }

    /// <summary>Target coordinate R for pathfinding.</summary>
    int? TargetCoordinateR { get; set; }

    /// <summary>
    /// The planned path as a FIFO queue. Path[0] is the next waypoint.
    /// </summary>
    List<Hex>? Path { get; set; }

    /// <summary>
    /// Movement rate in miles per hour.
    /// </summary>
    float MovementRate { get; }

    /// <summary>
    /// Time accumulated moving toward the next waypoint (in hours).
    /// </summary>
    float TimeInTransit { get; set; }
}
