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
    Task SetPopulationDensityAsync(Hex hex, int density);
    Task SetRoadAsync(Hex hex, int direction, bool hasRoad);
    Task SetRiverAsync(Hex hex, int edge, bool hasRiver);
    Task ClearRoadsAndRiversAsync(Hex hex);

    // Faction control
    Task SetFactionControlAsync(Hex hex, int? factionId);
    Task<IList<MapHex>> GetHexesControlledByFactionAsync(int factionId);

    // Forage tracking
    Task<int> ForageHexesAsync(IEnumerable<Hex> hexes);
    Task ResetForageCountsAsync();

    // Weather
    Task SetWeatherAsync(Hex hex, int? weatherId);
    Task SetRegionWeatherAsync(IEnumerable<Hex> hexes, int weatherId);

    // Terrain types and weather
    Task<IList<TerrainType>> GetTerrainTypesAsync();
    Task<IList<Weather>> GetWeatherTypesAsync();

    // Location types
    Task<IList<LocationType>> GetLocationTypesAsync();
    Task SetLocationAsync(Hex hex, int? locationTypeId, string? locationName, int? locationFactionId = null);
    Task ClearLocationAsync(Hex hex);

    // Movement Helpers
    Task<bool> HasRoadBetweenAsync(Hex a, Hex b);
    Task<bool> HasRiverBetweenAsync(Hex a, Hex b);

    // General hex update
    Task UpdateHexAsync(MapHex hex);
}
