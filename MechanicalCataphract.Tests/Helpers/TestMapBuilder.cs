using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using Moq;

namespace MechanicalCataphract.Tests.Helpers;

/// <summary>
/// Fluent builder for constructing mock hex grids used in pathfinding tests.
/// Creates MapHex objects and configures Mock&lt;IMapService&gt; accordingly.
/// </summary>
public class TestMapBuilder
{
    private readonly Dictionary<(int q, int r), MapHex> _hexes = new();
    private readonly TerrainType _defaultTerrain;

    public TestMapBuilder()
    {
        _defaultTerrain = new TerrainType { Id = 1, Name = "Grass", IsWater = false };
    }

    /// <summary>
    /// Adds a hex with the default (grass) terrain.
    /// </summary>
    public TestMapBuilder AddHex(int q, int r)
    {
        return AddHex(q, r, _defaultTerrain);
    }

    /// <summary>
    /// Adds a hex with a specific terrain type.
    /// </summary>
    public TestMapBuilder AddHex(int q, int r, TerrainType terrain)
    {
        _hexes[(q, r)] = new MapHex
        {
            Q = q,
            R = r,
            TerrainType = terrain,
            TerrainTypeId = terrain.Id
        };
        return this;
    }

    /// <summary>
    /// Adds a water hex (impassable).
    /// </summary>
    public TestMapBuilder AddWaterHex(int q, int r)
    {
        var water = new TerrainType { Id = 99, Name = "Water", IsWater = true };
        return AddHex(q, r, water);
    }

    /// <summary>
    /// Adds a road in the given direction from the specified hex.
    /// Also adds the reciprocal road on the neighbor if it exists.
    /// </summary>
    public TestMapBuilder AddRoad(int q, int r, int direction)
    {
        if (_hexes.TryGetValue((q, r), out var hex))
        {
            var existing = hex.RoadDirections;
            hex.RoadDirections = string.IsNullOrEmpty(existing)
                ? direction.ToString()
                : $"{existing},{direction}";
        }

        // Add reciprocal road on neighbor
        var neighborHex = new Hex(q, r, -q - r).Neighbor(direction);
        int oppositeDir = (direction + 3) % 6;
        if (_hexes.TryGetValue((neighborHex.q, neighborHex.r), out var neighbor))
        {
            var existing = neighbor.RoadDirections;
            neighbor.RoadDirections = string.IsNullOrEmpty(existing)
                ? oppositeDir.ToString()
                : $"{existing},{oppositeDir}";
        }

        return this;
    }

    /// <summary>
    /// Builds a configured Mock&lt;IMapService&gt; from the current hex grid.
    /// </summary>
    public Mock<IMapService> BuildMockMapService()
    {
        var mock = new Mock<IMapService>();

        // GetHexAsync(Hex) — lookup by hex struct
        mock.Setup(m => m.GetHexAsync(It.IsAny<Hex>()))
            .ReturnsAsync((Hex h) =>
                _hexes.TryGetValue((h.q, h.r), out var mapHex) ? mapHex : null);

        // GetHexAsync(int, int) — lookup by q, r
        mock.Setup(m => m.GetHexAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((int q, int r) =>
                _hexes.TryGetValue((q, r), out var mapHex) ? mapHex : null);

        // GetAllHexesAsync
        mock.Setup(m => m.GetAllHexesAsync())
            .ReturnsAsync(_hexes.Values.ToList());

        // HasRoadBetweenAsync — checks if hex A has a road in the direction of hex B
        mock.Setup(m => m.HasRoadBetweenAsync(It.IsAny<Hex>(), It.IsAny<Hex>()))
            .ReturnsAsync((Hex a, Hex b) =>
            {
                if (!_hexes.TryGetValue((a.q, a.r), out var mapHex))
                    return false;
                var dir = a.DirectionTo(b);
                if (dir == null) return false;
                return mapHex.HasRoadInDirection(dir.Value);
            });

        return mock;
    }

    /// <summary>
    /// Creates a standard water terrain type for use in tests.
    /// </summary>
    public static TerrainType WaterTerrain => new TerrainType { Id = 99, Name = "Water", IsWater = true };

    /// <summary>
    /// Creates a standard grass terrain type for use in tests.
    /// </summary>
    public static TerrainType GrassTerrain => new TerrainType { Id = 1, Name = "Grass", IsWater = false };
}
