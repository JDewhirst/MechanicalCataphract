using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hexes;
using Microsoft.EntityFrameworkCore;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public class MapService : IMapService
{
    private readonly WargameDbContext _context;

    public MapService(WargameDbContext context)
    {
        _context = context;
    }

    public async Task InitializeMapAsync(int rows, int columns, int defaultTerrainTypeId = 1)
    {
        // Clear existing hexes
        _context.MapHexes.RemoveRange(_context.MapHexes);

        // Create hexes using odd-q offset coordinates
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var offset = new OffsetCoord(col, row);
                var hex = OffsetCoord.QoffsetToCube(OffsetCoord.ODD, offset);

                _context.MapHexes.Add(new MapHex
                {
                    Q = hex.q,
                    R = hex.r,
                    TerrainTypeId = defaultTerrainTypeId,
                    PopulationDensity = 0,
                    TimesForaged = 0
                });
            }
        }

        // Update or create game state
        var gameState = await _context.GameStates.FindAsync(1);
        if (gameState == null)
        {
            gameState = new GameState { Id = 1, MapRows = rows, MapColumns = columns };
            _context.GameStates.Add(gameState);
        }
        else
        {
            gameState.MapRows = rows;
            gameState.MapColumns = columns;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> MapExistsAsync()
    {
        return await _context.MapHexes.AnyAsync();
    }

    public async Task<(int rows, int columns)> GetMapDimensionsAsync()
    {
        var gameState = await _context.GameStates.FindAsync(1);
        return gameState != null ? (gameState.MapRows, gameState.MapColumns) : (0, 0);
    }

    public async Task<MapHex?> GetHexAsync(int q, int r)
    {
        return await _context.MapHexes
            .Include(h => h.TerrainType)
            .Include(h => h.ControllingFaction)
            .Include(h => h.Weather)
            .FirstOrDefaultAsync(h => h.Q == q && h.R == r);
    }

    public async Task<MapHex?> GetHexAsync(Hex hex)
    {
        return await GetHexAsync(hex.q, hex.r);
    }

    public async Task<IList<MapHex>> GetHexesInRangeAsync(int minQ, int maxQ, int minR, int maxR)
    {
        return await _context.MapHexes
            .Include(h => h.TerrainType)
            .Include(h => h.ControllingFaction)
            .Include(h => h.Weather)
            .Where(h => h.Q >= minQ && h.Q <= maxQ && h.R >= minR && h.R <= maxR)
            .ToListAsync();
    }

    public async Task<IList<MapHex>> GetAllHexesAsync()
    {
        return await _context.MapHexes
            .Include(h => h.TerrainType)
            .ToListAsync();
    }

    public async Task SetTerrainAsync(Hex hex, int terrainTypeId)
    {
        var mapHex = await _context.MapHexes.FindAsync(hex.q, hex.r);
        if (mapHex != null)
        {
            mapHex.TerrainTypeId = terrainTypeId;
            await _context.SaveChangesAsync();
        }
    }

    public async Task SetRoadAsync(Hex hex, int direction, bool hasRoad)
    {
        var mapHex = await _context.MapHexes.FindAsync(hex.q, hex.r);
        if (mapHex == null) return;

        var directions = string.IsNullOrEmpty(mapHex.RoadDirections)
            ? new HashSet<string>()
            : mapHex.RoadDirections.Split(',').ToHashSet();

        if (hasRoad)
            directions.Add(direction.ToString());
        else
            directions.Remove(direction.ToString());

        mapHex.RoadDirections = directions.Count > 0 ? string.Join(",", directions) : null;
        await _context.SaveChangesAsync();
    }

    public async Task SetRiverAsync(Hex hex, int edge, bool hasRiver)
    {
        var mapHex = await _context.MapHexes.FindAsync(hex.q, hex.r);
        if (mapHex == null) return;

        var edges = string.IsNullOrEmpty(mapHex.RiverEdges)
            ? new HashSet<string>()
            : mapHex.RiverEdges.Split(',').ToHashSet();

        if (hasRiver)
            edges.Add(edge.ToString());
        else
            edges.Remove(edge.ToString());

        mapHex.RiverEdges = edges.Count > 0 ? string.Join(",", edges) : null;
        await _context.SaveChangesAsync();
    }

    public async Task ClearRoadsAndRiversAsync(Hex hex)
    {
        var mapHex = await _context.MapHexes.FindAsync(hex.q, hex.r);
        if (mapHex == null) return;

        mapHex.RoadDirections = null;
        mapHex.RiverEdges = null;
        await _context.SaveChangesAsync();
    }

    public async Task SetFactionControlAsync(Hex hex, int? factionId)
    {
        var mapHex = await _context.MapHexes.FindAsync(hex.q, hex.r);
        if (mapHex != null)
        {
            mapHex.ControllingFactionId = factionId;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IList<MapHex>> GetHexesControlledByFactionAsync(int factionId)
    {
        return await _context.MapHexes
            .Where(h => h.ControllingFactionId == factionId)
            .ToListAsync();
    }

    public async Task IncrementForageCountAsync(Hex hex)
    {
        var mapHex = await _context.MapHexes.FindAsync(hex.q, hex.r);
        if (mapHex != null)
        {
            mapHex.TimesForaged++;
            await _context.SaveChangesAsync();
        }
    }

    public async Task ResetForageCountsAsync()
    {
        await _context.MapHexes.ExecuteUpdateAsync(h => h.SetProperty(x => x.TimesForaged, 0));
    }

    public async Task SetWeatherAsync(Hex hex, int? weatherId)
    {
        var mapHex = await _context.MapHexes.FindAsync(hex.q, hex.r);
        if (mapHex != null)
        {
            mapHex.WeatherId = weatherId;
            await _context.SaveChangesAsync();
        }
    }

    public async Task SetRegionWeatherAsync(IEnumerable<Hex> hexes, int weatherId)
    {
        var hexList = hexes.ToList();
        var qrPairs = hexList.Select(h => new { h.q, h.r }).ToList();

        foreach (var pair in qrPairs)
        {
            var mapHex = await _context.MapHexes.FindAsync(pair.q, pair.r);
            if (mapHex != null)
            {
                mapHex.WeatherId = weatherId;
            }
        }
        await _context.SaveChangesAsync();
    }

    public async Task<IList<TerrainType>> GetTerrainTypesAsync()
    {
        return await _context.TerrainTypes.ToListAsync();
    }

    public async Task<IList<Weather>> GetWeatherTypesAsync()
    {
        return await _context.WeatherTypes.ToListAsync();
    }
}
