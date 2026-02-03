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
            TravelEntityType.Supply => baseCost * 2,        // Supply convoys are twice as slow
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

    public async Task<int> MoveMessage(Message message, int hours)
    {
        // 1. Get current location as Hex
        if (message.LocationQ == null || message.LocationR == null)
            return 0; // No location

        if (message.Path == null || message.Path.Count == 0)
            return 0; // No path to follow

        var currentHex = new Hex(
            message.LocationQ.Value,
            message.LocationR.Value,
            -message.LocationQ.Value - message.LocationR.Value);

        // 2. Next waypoint is Path[0]
        var nextHex = message.Path[0];

        // 3. Check for road between them
        bool hasRoad = await _mapService.HasRoadBetweenAsync(currentHex, nextHex);

        // 4. Determine cost
        int movementCost = hasRoad ? RoadCost : OffRoadCost;

        // 5. increment timeInTransit and check if reached cost
        message.TimeInTransit += hours;
        if (message.TimeInTransit >= (movementCost / message.MovementRate))
        {
            message.LocationQ = nextHex.q;
            message.LocationR = nextHex.r;
            message.Path.Remove(nextHex);
            message.TimeInTransit = message.TimeInTransit - movementCost / message.MovementRate;
            await _messageService.UpdateAsync(message);
            return 1;
        }

        await _messageService.UpdateAsync(message);
        return 0;
    }

    public async Task<int> MoveArmy(Army army, int hours)
    {
        if (army.LocationQ == null || army.LocationR == null)
            return 0;

        if (army.Path == null || army.Path.Count == 0)
            return 0;

        var currentHex = new Hex(
            army.LocationQ.Value,
            army.LocationR.Value,
            -army.LocationQ.Value - army.LocationR.Value);

        var nextHex = army.Path[0];

        bool hasRoad = await _mapService.HasRoadBetweenAsync(currentHex, nextHex);

        int movementCost = hasRoad ? RoadCost : OffRoadCost;

        army.TimeInTransit += hours;
        if (army.TimeInTransit >= (movementCost / army.MovementRate))
        {
            army.LocationQ = nextHex.q;
            army.LocationR = nextHex.r;
            army.Path.Remove(nextHex);
            army.TimeInTransit = army.TimeInTransit - movementCost / army.MovementRate;
            await _armyService.UpdateAsync(army);
            return 1;
        }

        await _armyService.UpdateAsync(army);
        return 0;
    }

    public async Task<int> MoveCommander(Commander commander, int hours)
    {
        if (commander.LocationQ == null || commander.LocationR == null)
            return 0;

        if (commander.Path == null || commander.Path.Count == 0)
            return 0;

        var currentHex = new Hex(
            commander.LocationQ.Value,
            commander.LocationR.Value,
            -commander.LocationQ.Value - commander.LocationR.Value);

        var nextHex = commander.Path[0];

        bool hasRoad = await _mapService.HasRoadBetweenAsync(currentHex, nextHex);

        int movementCost = hasRoad ? RoadCost : OffRoadCost;

        commander.TimeInTransit += hours;
        if (commander.TimeInTransit >= (movementCost / commander.MovementRate))
        {
            commander.LocationQ = nextHex.q;
            commander.LocationR = nextHex.r;
            commander.Path.Remove(nextHex);
            commander.TimeInTransit = commander.TimeInTransit - movementCost / commander.MovementRate;
            await _commanderService.UpdateAsync(commander);
            return 1;
        }

        await _commanderService.UpdateAsync(commander);
        return 0;
    }
}
