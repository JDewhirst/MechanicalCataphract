using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Tests.Services.Integration;

[TestFixture]
public class MapServiceIntegrationTests : IntegrationTestBase
{
    private MapService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        await SeedHelpers.SeedDefaultTerrainAsync(Context);
        _service = new MapService(Context);
    }

    [Test]
    public async Task InitializeMap_CreatesCorrectHexCount()
    {
        await _service.InitializeMapAsync(3, 3);

        var all = await _service.GetAllHexesAsync();
        Assert.That(all.Count, Is.EqualTo(9));
    }

    [Test]
    public async Task InitializeMap_SetsGameStateDimensions()
    {
        await _service.InitializeMapAsync(4, 5);

        var (rows, cols) = await _service.GetMapDimensionsAsync();
        Assert.That(rows, Is.EqualTo(4));
        Assert.That(cols, Is.EqualTo(5));
    }

    [Test]
    public async Task MapExistsAsync_FalseWhenEmpty_TrueAfterInit()
    {
        Assert.That(await _service.MapExistsAsync(), Is.False);

        await _service.InitializeMapAsync(2, 2);

        Assert.That(await _service.MapExistsAsync(), Is.True);
    }

    [Test]
    public async Task GetHexAsync_ReturnsWithTerrain()
    {
        await _service.InitializeMapAsync(3, 3);
        // (0,0) offset → Hex(0,0,0) in cube coords
        var hex = await _service.GetHexAsync(0, 0);

        Assert.That(hex, Is.Not.Null);
        Assert.That(hex!.TerrainType, Is.Not.Null);
        Assert.That(hex.TerrainType!.Name, Is.EqualTo("Grass"));
    }

    [Test]
    public async Task GetHexAsync_ReturnsNull_ForNonexistent()
    {
        await _service.InitializeMapAsync(2, 2);

        var hex = await _service.GetHexAsync(999, 999);
        Assert.That(hex, Is.Null);
    }

    [Test]
    public async Task GetAllHexesAsync_ReturnsAll()
    {
        await _service.InitializeMapAsync(4, 4);

        var all = await _service.GetAllHexesAsync();
        Assert.That(all.Count, Is.EqualTo(16));
    }

    [Test]
    public async Task GetHexesInRangeAsync_FiltersCorrectly()
    {
        await _service.InitializeMapAsync(5, 5);

        var subset = await _service.GetHexesInRangeAsync(0, 1, 0, 1);

        // Should be a subset, not all 25
        Assert.That(subset.Count, Is.GreaterThan(0));
        Assert.That(subset.Count, Is.LessThan(25));
        Assert.That(subset.All(h => h.Q >= 0 && h.Q <= 1 && h.R >= 0 && h.R <= 1), Is.True);
    }

    [Test]
    public async Task SetTerrainAsync_UpdatesTerrainType()
    {
        await _service.InitializeMapAsync(3, 3);
        var water = await SeedHelpers.SeedWaterTerrainAsync(Context);

        var target = new Hex(0, 0, 0);
        await _service.SetTerrainAsync(target, water.Id);

        var hex = await _service.GetHexAsync(target);
        Assert.That(hex!.TerrainTypeId, Is.EqualTo(water.Id));
    }

    [Test]
    public async Task SetRoadAsync_AddsAndRemoves()
    {
        await _service.InitializeMapAsync(3, 3);
        var target = new Hex(0, 0, 0);

        await _service.SetRoadAsync(target, 0, true);
        var hex = await _service.GetHexAsync(target);
        Assert.That(hex!.RoadDirections, Does.Contain("0"));

        await _service.SetRoadAsync(target, 0, false);
        hex = await _service.GetHexAsync(target);
        Assert.That(hex!.RoadDirections, Is.Null);
    }

    [Test]
    public async Task SetRiverAsync_AddsAndRemoves()
    {
        await _service.InitializeMapAsync(3, 3);
        var target = new Hex(0, 0, 0);

        await _service.SetRiverAsync(target, 2, true);
        var hex = await _service.GetHexAsync(target);
        Assert.That(hex!.RiverEdges, Does.Contain("2"));

        await _service.SetRiverAsync(target, 2, false);
        hex = await _service.GetHexAsync(target);
        Assert.That(hex!.RiverEdges, Is.Null);
    }

    [Test]
    public async Task ClearRoadsAndRiversAsync_ClearsBoth()
    {
        await _service.InitializeMapAsync(3, 3);
        var target = new Hex(0, 0, 0);
        await _service.SetRoadAsync(target, 0, true);
        await _service.SetRiverAsync(target, 1, true);

        await _service.ClearRoadsAndRiversAsync(target);

        var hex = await _service.GetHexAsync(target);
        Assert.That(hex!.RoadDirections, Is.Null);
        Assert.That(hex.RiverEdges, Is.Null);
    }

    [Test]
    public async Task SetFactionControlAsync_AndQuery()
    {
        await _service.InitializeMapAsync(3, 3);
        var faction = await SeedHelpers.SeedFactionAsync(Context, "Empire");
        var target = new Hex(0, 0, 0);

        await _service.SetFactionControlAsync(target, faction.Id);

        var controlled = await _service.GetHexesControlledByFactionAsync(faction.Id);
        Assert.That(controlled.Count, Is.EqualTo(1));
        Assert.That(controlled[0].Q, Is.EqualTo(0));
    }

    [Test]
    public async Task ForageHexesAsync_CalculatesSupply()
    {
        await _service.InitializeMapAsync(3, 3);
        var hex = await _service.GetHexAsync(0, 0);
        hex!.PopulationDensity = 5;
        await _service.UpdateHexAsync(hex);

        var supply = await _service.ForageHexesAsync(new[] { new Hex(0, 0, 0) });

        Assert.That(supply, Is.EqualTo(2500)); // 5 * 500
        var reloaded = await _service.GetHexAsync(0, 0);
        Assert.That(reloaded!.TimesForaged, Is.EqualTo(1));
    }

    [Test]
    public async Task ResetForageCountsAsync_ResetsAll()
    {
        await _service.InitializeMapAsync(3, 3);
        var hex = await _service.GetHexAsync(0, 0);
        hex!.PopulationDensity = 5;
        await _service.UpdateHexAsync(hex);
        await _service.ForageHexesAsync(new[] { new Hex(0, 0, 0) });

        await _service.ResetForageCountsAsync();

        // ExecuteUpdateAsync bypasses change tracker — tracked entities retain stale values.
        // Detach all tracked MapHex entries so FindAsync fetches fresh data from SQLite.
        foreach (var entry in Context.ChangeTracker.Entries<MapHex>().ToList())
            entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var reloaded = await Context.MapHexes.FindAsync(0, 0);
        Assert.That(reloaded!.TimesForaged, Is.EqualTo(0));
    }

    [Test]
    public async Task SetAndClearLocation()
    {
        await _service.InitializeMapAsync(3, 3);
        var target = new Hex(0, 0, 0);

        await _service.SetLocationAsync(target, 1, "Paris");
        var hex = await _service.GetHexAsync(target);
        Assert.That(hex!.LocationName, Is.EqualTo("Paris"));
        Assert.That(hex.LocationTypeId, Is.EqualTo(1));

        await _service.ClearLocationAsync(target);
        hex = await _service.GetHexAsync(target);
        Assert.That(hex!.LocationName, Is.Null);
        Assert.That(hex.LocationTypeId, Is.Null);
    }

    [Test]
    public async Task HasRoadBetweenAsync_TrueAndFalse()
    {
        await _service.InitializeMapAsync(3, 3);
        var a = new Hex(0, 0, 0);
        var b = a.Neighbor(0);

        // No road initially
        Assert.That(await _service.HasRoadBetweenAsync(a, b), Is.False);

        // Add road in direction 0
        await _service.SetRoadAsync(a, 0, true);
        Assert.That(await _service.HasRoadBetweenAsync(a, b), Is.True);
    }

    [Test]
    public async Task SetWeatherAsync_Updates()
    {
        await _service.InitializeMapAsync(3, 3);
        var target = new Hex(0, 0, 0);

        await _service.SetWeatherAsync(target, 2); // Rain

        var hex = await _service.GetHexAsync(target);
        Assert.That(hex!.WeatherId, Is.EqualTo(2));
        Assert.That(hex.Weather, Is.Not.Null);
        Assert.That(hex.Weather!.Name, Is.EqualTo("Rain"));
    }

    [Test]
    public async Task GetTerrainTypes_WeatherTypes_LocationTypes_ReturnSeededData()
    {
        var terrains = await _service.GetTerrainTypesAsync();
        var weathers = await _service.GetWeatherTypesAsync();
        var locations = await _service.GetLocationTypesAsync();

        Assert.That(terrains.Count, Is.EqualTo(1)); // only manually seeded Grass
        Assert.That(weathers.Count, Is.EqualTo(5)); // EF-seeded
        Assert.That(locations.Count, Is.EqualTo(5)); // EF-seeded (includes "No Location" sentinel)
    }
}
