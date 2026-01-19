using System.Collections.Generic;
using System.Threading.Tasks;
using Hexes;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public interface IMapService
{
    // Map initialization
    Task InitializeMapAsync(int rows, int columns, int defaultTerrainTypeId = 1);
    Task<bool> MapExistsAsync();
    Task<(int rows, int columns)> GetMapDimensionsAsync();

    // Hex operations
    Task<MapHex?> GetHexAsync(int q, int r);
    Task<MapHex?> GetHexAsync(Hex hex);
    Task<IList<MapHex>> GetHexesInRangeAsync(int minQ, int maxQ, int minR, int maxR);
    Task<IList<MapHex>> GetAllHexesAsync();

    // Terrain editing
    Task SetTerrainAsync(Hex hex, int terrainTypeId);
    Task SetRoadAsync(Hex hex, int direction, bool hasRoad);
    Task SetRiverAsync(Hex hex, int edge, bool hasRiver);
    Task ClearRoadsAndRiversAsync(Hex hex);

    // Faction control
    Task SetFactionControlAsync(Hex hex, int? factionId);
    Task<IList<MapHex>> GetHexesControlledByFactionAsync(int factionId);

    // Forage tracking
    Task IncrementForageCountAsync(Hex hex);
    Task ResetForageCountsAsync();

    // Weather
    Task SetWeatherAsync(Hex hex, int? weatherId);
    Task SetRegionWeatherAsync(IEnumerable<Hex> hexes, int weatherId);

    // Terrain types and weather
    Task<IList<TerrainType>> GetTerrainTypesAsync();
    Task<IList<Weather>> GetWeatherTypesAsync();
}
