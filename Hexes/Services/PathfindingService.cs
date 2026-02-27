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
    private readonly IGameRulesService _gameRulesService;
    private readonly IFactionRuleService _factionRuleService;

    public PathfindingService(
        IMapService mapService,
        IMessageService messageService,
        IArmyService armyService,
        ICommanderService commanderService,
        IGameRulesService gameRulesService,
        IFactionRuleService factionRuleService)
    {
        _mapService = mapService;
        _messageService = messageService;
        _armyService = armyService;
        _commanderService = commanderService;
        _gameRulesService = gameRulesService;
        _factionRuleService = factionRuleService;
    }

    public async Task<PathResult> FindPathAsync(Hex start, Hex end,
        TravelEntityType entityType = TravelEntityType.Message,
        int? factionId = null)
    {
        // Validate start and end hexes exist
        var startHex = await _mapService.GetHexAsync(start);
        var endHex = await _mapService.GetHexAsync(end);

        if (startHex == null)
            return new PathResult { Success = false, FailureReason = "Start hex does not exist" };

        if (startHex.Q == MapHex.SentinelQ && startHex.R == MapHex.SentinelR)
            return new PathResult { Success = false, FailureReason = "Entity is off the map — assign a map location before pathfinding" };

        if (endHex == null)
            return new PathResult { Success = false, FailureReason = "Target hex does not exist" };

        if (endHex.Q == MapHex.SentinelQ && endHex.R == MapHex.SentinelR)
            return new PathResult { Success = false, FailureReason = "Cannot pathfind to the Torment Hexagon" };

        if (IsWaterHex(endHex))
            return new PathResult { Success = false, FailureReason = "Target hex is water (impassable)" };

        if (start.q == end.q && start.r == end.r)
            return new PathResult { Success = true, Path = Array.Empty<Hex>(), TotalCost = 0 };

        // Load all hexes into a dictionary for fast lookup
        var allHexes = await _mapService.GetAllHexesAsync();
        var hexMap = allHexes.ToDictionary(h => (h.Q, h.R), h => h);

        var rules = _gameRulesService.Rules;

        // A* algorithm
        var openSet = new PriorityQueue<Hex, int>();
        var cameFrom = new Dictionary<(int q, int r), Hex>();
        var gScore = new Dictionary<(int q, int r), int> { [(start.q, start.r)] = 0 };
        var fScore = new Dictionary<(int q, int r), int> { [(start.q, start.r)] = PathfindingHeuristic(start, end, rules) };

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
                int moveCost = GetMovementCost(currentMapHex, dir, entityType, rules);

                int tentativeGScore = gScore[(current.q, current.r)] + moveCost;

                if (!gScore.TryGetValue(neighborKey, out var existingGScore) || tentativeGScore < existingGScore)
                {
                    // This path is better
                    cameFrom[neighborKey] = current;
                    gScore[neighborKey] = tentativeGScore;
                    fScore[neighborKey] = tentativeGScore + PathfindingHeuristic(neighbor, end, rules);

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
    private static int PathfindingHeuristic(Hex a, Hex b, GameRulesData rules)
    {
        return a.Distance(b) * rules.Movement.RoadCost;
    }

    /// <summary>
    /// Calculates movement cost from a hex in a given direction.
    /// </summary>
    private static int GetMovementCost(MapHex from, int direction, TravelEntityType entityType, GameRulesData rules)
    {
        bool hasRoad = from.HasRoadInDirection(direction);
        int baseCost = hasRoad ? rules.Movement.RoadCost : rules.Movement.OffRoadCost;

        return entityType switch
        {
            TravelEntityType.Army => (int)(baseCost * rules.Movement.ArmyMovementMultiplier),
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

        var rules = _gameRulesService.Rules;
        bool hasRoad = await _mapService.HasRoadBetweenAsync(currentHex, nextHex);
        int movementCost = (hasRoad ? rules.Movement.RoadCost : rules.Movement.OffRoadCost) + extraCost;

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
        float effectiveRate = message.MovementRate;

        // Faction rule: messenger speed bonus in own-controlled hexes
        if (message.CoordinateQ.HasValue && message.CoordinateR.HasValue && message.SenderCommander != null)
        {
            var hex = await _mapService.GetHexAsync(message.CoordinateQ.Value, message.CoordinateR.Value);
            if (hex?.ControllingFactionId != null && hex.ControllingFactionId == message.SenderCommander.FactionId)
            {
                await _factionRuleService.PreloadForFactionAsync(message.SenderCommander.FactionId);
                double multiplier = _factionRuleService.GetCachedRuleValue(
                    message.SenderCommander.FactionId,
                    FactionRuleKeys.OwnTerritoryMessengerMultiplier,
                    1.0);
                effectiveRate = (float)(effectiveRate * multiplier);
            }
        }

        return await MoveEntity(message, hours, () => _messageService.UpdateAsync(message), effectiveRate);
    }

    public async Task<int> MoveArmy(Army army, int hours, DateTime currentGameTime)
    {
        var rules = _gameRulesService.Rules;

        // Daytime restriction: armies only march during configured hours unless night marching
        if (!army.IsNightMarching &&
            (currentGameTime.Hour < rules.Movement.MarchDayStartHour ||
             currentGameTime.Hour >= rules.Movement.MarchDayEndHour))
            return 0;

        if (army.CoordinateQ == null || army.CoordinateR == null)
            return 0;

        if (army.Path == null || army.Path.Count == 0)
            return 0;

        // Determine effective movement rate
        float baseRate = army.MovementRate;
        bool isLongColumn = army.MarchingColumnLength > rules.Movement.LongColumnThreshold;

        // Speed cap for long columns
        if (isLongColumn)
            baseRate = (float)rules.Movement.LongColumnSpeedCap;

        // Forced march multiplies effective rate
        float effectiveRate = army.IsForcedMarch ? baseRate * (float)rules.Movement.ForcedMarchMultiplier : baseRate;

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
            extraCost = army.FordingColumnLength * rules.Movement.RiverFordingCostPerColumnUnit;

        return await MoveEntity(army, hours, () => _armyService.UpdateAsync(army), effectiveRate, extraCost);
    }

    public async Task<int> MoveCommander(Commander commander, int hours)
    {
        if (commander.FollowingArmyId != null)
            return 0;

        return await MoveEntity(commander, hours, () => _commanderService.UpdateAsync(commander));
    }
}
