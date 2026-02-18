using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hexes;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public class PathfindingService : IPathfindingService
{
    private readonly IMapService _mapService;
    private readonly IMessageService _messageService;
    private readonly IArmyService _armyService;
    private readonly ICommanderService _commanderService;

    // Cost constants (in abstract units, representing 6 miles per hex)
    private const int RoadCost = 6;
    private const int OffRoadCost = 12;

    public PathfindingService(
        IMapService mapService,
        IMessageService messageService,
        IArmyService armyService,
        ICommanderService commanderService)
    {
        _mapService = mapService;
        _messageService = messageService;
        _armyService = armyService;
        _commanderService = commanderService;
    }

    public async Task<PathResult> FindPathAsync(Hex start, Hex end, TravelEntityType entityType = TravelEntityType.Message)
    {
        // Validate start and end hexes exist
        var startHex = await _mapService.GetHexAsync(start);
        var endHex = await _mapService.GetHexAsync(end);

        if (startHex == null)
            return new PathResult { Success = false, FailureReason = "Start hex does not exist" };

        if (endHex == null)
            return new PathResult { Success = false, FailureReason = "Target hex does not exist" };

        if (IsWaterHex(endHex))
            return new PathResult { Success = false, FailureReason = "Target hex is water (impassable)" };

        if (start.q == end.q && start.r == end.r)
            return new PathResult { Success = true, Path = Array.Empty<Hex>(), TotalCost = 0 };

        // Load all hexes into a dictionary for fast lookup
        var allHexes = await _mapService.GetAllHexesAsync();
        var hexMap = allHexes.ToDictionary(h => (h.Q, h.R), h => h);

        // A* algorithm
        var openSet = new PriorityQueue<Hex, int>();
        var cameFrom = new Dictionary<(int q, int r), Hex>();
        var gScore = new Dictionary<(int q, int r), int> { [(start.q, start.r)] = 0 };
        var fScore = new Dictionary<(int q, int r), int> { [(start.q, start.r)] = PathfindingHeuristic(start, end) };

        openSet.Enqueue(start, fScore[(start.q, start.r)]);

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            // Goal reached
            if (current.q == end.q && current.r == end.r)
            {
                var path = ReconstructPath(cameFrom, current, start);
                return new PathResult
                {
                    Success = true,
                    Path = path,
                    TotalCost = gScore[(current.q, current.r)]
                };
            }

            // Explore neighbors (6 directions for hex grid)
            for (int dir = 0; dir < 6; dir++)
            {
                var neighbor = current.Neighbor(dir);
                var neighborKey = (neighbor.q, neighbor.r);

                // Check if neighbor hex exists and is traversable
                if (!hexMap.TryGetValue(neighborKey, out var neighborMapHex))
                    continue;

                if (IsWaterHex(neighborMapHex))
                    continue;

                // Calculate movement cost
                var currentMapHex = hexMap[(current.q, current.r)];
                int moveCost = GetMovementCost(currentMapHex, neighborMapHex, dir, entityType);

                int tentativeGScore = gScore[(current.q, current.r)] + moveCost;

                if (!gScore.TryGetValue(neighborKey, out var existingGScore) || tentativeGScore < existingGScore)
                {
                    // This path is better
                    cameFrom[neighborKey] = current;
                    gScore[neighborKey] = tentativeGScore;
                    fScore[neighborKey] = tentativeGScore + PathfindingHeuristic(neighbor, end);

                    // Add to open set (PriorityQueue handles duplicates by priority)
                    openSet.Enqueue(neighbor, fScore[neighborKey]);
                }
            }
        }

        // No path found
        return new PathResult { Success = false, FailureReason = "No path exists between the hexes" };
    }

    /// <summary>
    /// Heuristic function for A* - hex distance multiplied by minimum cost.
    /// Admissible because it never overestimates the actual cost.
    /// </summary>
    private static int PathfindingHeuristic(Hex a, Hex b)
    {
        return a.Distance(b) * RoadCost;
    }

    /// <summary>
    /// Calculates movement cost between two adjacent hexes.
    /// </summary>
    private static int GetMovementCost(MapHex from, MapHex to, int direction, TravelEntityType entityType)
    {
        // Check if there's a road connecting these hexes
        bool hasRoad = from.HasRoadInDirection(direction);

        int baseCost = hasRoad ? RoadCost : OffRoadCost;

        // Apply entity type modifier (could be expanded later)
        return entityType switch
        {
            TravelEntityType.Message => baseCost,           // Messengers move at base speed
            TravelEntityType.Army => (int)(baseCost * 1.5), // Armies are 50% slower
            _ => baseCost
        };
    }

    /// <summary>
    /// Checks if a hex is water (impassable).
    /// </summary>
    private static bool IsWaterHex(MapHex hex)
    {
        return hex.TerrainType?.IsWater ?? false;
    }

    /// <summary>
    /// Reconstructs the path from the A* search results.
    /// Returns path NOT including the start hex.
    /// </summary>
    private static List<Hex> ReconstructPath(Dictionary<(int q, int r), Hex> cameFrom, Hex current, Hex start)
    {
        var path = new List<Hex>();
        var currentKey = (current.q, current.r);

        while (cameFrom.ContainsKey(currentKey))
        {
            path.Add(current);
            current = cameFrom[currentKey];
            currentKey = (current.q, current.r);
        }

        // Don't include start hex in path
        path.Reverse();
        return path;
    }

    /// <summary>
    /// Shared movement logic for any IPathMovable entity.
    /// </summary>
    /// <param name="entity">The entity to move</param>
    /// <param name="hours">Hours of movement to apply</param>
    /// <param name="saveAsync">Callback to persist the entity</param>
    /// <param name="effectiveRate">Override movement rate (null = use entity's MovementRate)</param>
    /// <param name="extraCost">Additional cost on top of base movement cost (e.g. river fording)</param>
    /// <returns>1 if entity advanced to next waypoint, 0 otherwise</returns>
    private async Task<int> MoveEntity(IPathMovable entity, int hours, Func<Task> saveAsync, float? effectiveRate = null, int extraCost = 0)
    {
        if (entity.CoordinateQ == null || entity.CoordinateR == null)
            return 0;

        if (entity.Path == null || entity.Path.Count == 0)
            return 0;

        var currentHex = new Hex(
            entity.CoordinateQ.Value,
            entity.CoordinateR.Value,
            -entity.CoordinateQ.Value - entity.CoordinateR.Value);

        var nextHex = entity.Path[0];

        bool hasRoad = await _mapService.HasRoadBetweenAsync(currentHex, nextHex);
        int movementCost = (hasRoad ? RoadCost : OffRoadCost) + extraCost;

        float rate = effectiveRate ?? entity.MovementRate;

        entity.TimeInTransit += hours;
        if (entity.TimeInTransit >= (movementCost / rate))
        {
            entity.CoordinateQ = nextHex.q;
            entity.CoordinateR = nextHex.r;
            entity.Path.Remove(nextHex);
            entity.TimeInTransit = entity.TimeInTransit - movementCost / rate;
            await saveAsync();
            return 1;
        }

        await saveAsync();
        return 0;
    }

    public async Task<int> MoveMessage(Message message, int hours)
    {
        return await MoveEntity(message, hours, () => _messageService.UpdateAsync(message));
    }

    public async Task<int> MoveArmy(Army army, int hours, DateTime currentGameTime)
    {
        // Daytime restriction: armies only march 8am–8pm unless night marching
        if (!army.IsNightMarching && (currentGameTime.Hour < 8 || currentGameTime.Hour >= 20))
            return 0;

        if (army.CoordinateQ == null || army.CoordinateR == null)
            return 0;

        if (army.Path == null || army.Path.Count == 0)
            return 0;

        // Determine effective movement rate
        float baseRate = army.MovementRate; // 1.0 mph
        bool isLongColumn = army.MarchingColumnLength > 6;

        // Speed cap for long columns
        if (isLongColumn)
            baseRate = 0.5f;

        // Forced march doubles effective rate
        float effectiveRate = army.IsForcedMarch ? baseRate * 2.0f : baseRate;

        // Track forced march hours
        if (army.IsForcedMarch)
            army.ForcedMarchHours += hours;

        // River fording penalty: when crossing a river edge without a bridge
        int extraCost = 0;
        var currentHex = new Hex(
            army.CoordinateQ.Value,
            army.CoordinateR.Value,
            -army.CoordinateQ.Value - army.CoordinateR.Value);
        var nextHex = army.Path[0];

        bool hasRoad = await _mapService.HasRoadBetweenAsync(currentHex, nextHex);
        bool hasRiver = await _mapService.HasRiverBetweenAsync(currentHex, nextHex);

        // River without bridge = fording penalty (cavalry excluded, forced march doesn't help)
        if (hasRiver && !hasRoad)
            extraCost = army.FordingColumnLength * 6;

        return await MoveEntity(army, hours, () => _armyService.UpdateAsync(army), effectiveRate, extraCost);
    }

    public async Task<int> MoveCommander(Commander commander, int hours)
    {
        if (commander.FollowingArmyId != null)
            return 0;

        return await MoveEntity(commander, hours, () => _commanderService.UpdateAsync(commander));
    }
}
