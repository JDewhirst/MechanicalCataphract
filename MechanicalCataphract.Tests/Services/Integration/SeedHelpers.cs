using Hexes;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Tests.Services.Integration;

public static class SeedHelpers
{
    public static async Task<TerrainType> SeedDefaultTerrainAsync(WargameDbContext ctx)
    {
        var terrain = new TerrainType { Id = 1, Name = "Grass", ColorHex = "#00FF00" };
        ctx.TerrainTypes.Add(terrain);
        await ctx.SaveChangesAsync();
        return terrain;
    }

    public static async Task<TerrainType> SeedWaterTerrainAsync(WargameDbContext ctx)
    {
        var terrain = new TerrainType { Id = 99, Name = "Water", ColorHex = "#0000FF", IsWater = true };
        ctx.TerrainTypes.Add(terrain);
        await ctx.SaveChangesAsync();
        return terrain;
    }

    public static async Task<Faction> SeedFactionAsync(WargameDbContext ctx, string name = "Test Faction", string color = "#FF0000")
    {
        var faction = new Faction { Name = name, ColorHex = color };
        ctx.Factions.Add(faction);
        await ctx.SaveChangesAsync();
        return faction;
    }

    public static async Task SeedMapAsync(WargameDbContext ctx, int rows, int cols)
    {
        await SeedDefaultTerrainAsync(ctx);
        var mapService = new MapService(ctx);
        await mapService.InitializeMapAsync(rows, cols, defaultTerrainTypeId: 1);
    }

    public static async Task<Commander> SeedCommanderAsync(
        WargameDbContext ctx, string name, int factionId, int? coordinateQ = null, int? coordinateR = null)
    {
        var commander = new Commander
        {
            Name = name,
            FactionId = factionId,
            CoordinateQ = coordinateQ,
            CoordinateR = coordinateR
        };
        ctx.Commanders.Add(commander);
        await ctx.SaveChangesAsync();
        return commander;
    }

    public static async Task<Army> SeedArmyAsync(
        WargameDbContext ctx, string name, int factionId, int? coordinateQ, int? coordinateR, int? commanderId = null)
    {
        var army = new Army
        {
            Name = name,
            FactionId = factionId,
            CoordinateQ = coordinateQ,
            CoordinateR = coordinateR,
            CommanderId = commanderId
        };
        ctx.Armies.Add(army);
        await ctx.SaveChangesAsync();
        return army;
    }

    public static async Task<Brigade> SeedBrigadeAsync(
        WargameDbContext ctx, int armyId, string name = "Brigade", int number = 100,
        UnitType unitType = UnitType.Infantry)
    {
        var brigade = new Brigade
        {
            ArmyId = armyId,
            Name = name,
            Number = number,
            UnitType = unitType
        };
        ctx.Brigades.Add(brigade);
        await ctx.SaveChangesAsync();
        return brigade;
    }

    public static async Task<Navy> SeedNavyAsync(
        WargameDbContext ctx, string name = "Test Navy", int? coordinateQ = null, int? coordinateR = null,
        int? commanderId = null, int carriedSupply = 0)
    {
        var navy = new Navy
        {
            Name = name,
            CoordinateQ = coordinateQ,
            CoordinateR = coordinateR,
            CommanderId = commanderId,
            CarriedSupply = carriedSupply
        };
        ctx.Navies.Add(navy);
        await ctx.SaveChangesAsync();
        return navy;
    }

    public static async Task<Ship> SeedShipAsync(
        WargameDbContext ctx, int navyId,
        ShipType shipType = ShipType.Transport, int count = 1)
    {
        var ship = new Ship { NavyId = navyId, ShipType = shipType, Count = count };
        ctx.Ships.Add(ship);
        await ctx.SaveChangesAsync();
        return ship;
    }
}
