using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hexes;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public interface IPathfindingService
{
    /// <summary>
    /// Finds the optimal path between two hexes using A* algorithm.
    /// </summary>
    /// <param name="start">Starting hex</param>
    /// <param name="end">Target hex</param>
    /// <param name="entityType">Type of entity for travel speed calculation</param>
    /// <returns>Path result containing the route or failure reason</returns>
    Task<PathResult> FindPathAsync(Hex start, Hex end, TravelEntityType entityType = TravelEntityType.Message);

    /// <summary>Moves a message along its path for the given hours.</summary>
    Task<int> MoveMessage(Message message, int hours);

    /// <summary>Moves an army along its path for the given hours. Respects daytime restriction unless night marching.</summary>
    Task<int> MoveArmy(Army army, int hours, DateTime currentGameTime);

    /// <summary>Moves a commander along their path for the given hours.</summary>
    Task<int> MoveCommander(Commander commander, int hours);
}

/// <summary>
/// Types of entities that can travel, affecting movement speed.
/// </summary>
public enum TravelEntityType
{
    /// <summary>Messenger on horseback - fastest (2 mph)</summary>
    Message,
    /// <summary>Commander traveling alone - same as messenger (2 mph)</summary>
    Commander,
    /// <summary>Army on foot - slower (0.5 mph)</summary>
    Army,
    /// <summary>Supply convoy - slowest</summary>
    Supply
}

public class PathResult
{
    public bool Success { get; init; }
    /// <summary>
    /// The computed path, NOT including the start hex (first element is first waypoint).
    /// </summary>
    public IReadOnlyList<Hex> Path { get; init; } = Array.Empty<Hex>();
    public int TotalCost { get; init; }
    public string? FailureReason { get; init; }
};

