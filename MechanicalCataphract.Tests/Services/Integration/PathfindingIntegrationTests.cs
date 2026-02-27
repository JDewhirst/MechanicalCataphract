using System;
using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using Moq;

namespace MechanicalCataphract.Tests.Services.Integration;

[TestFixture]
public class PathfindingIntegrationTests : IntegrationTestBase
{
    private PathfindingService _pathfindingService = null!;
    private MapService _mapService = null!;
    private ArmyService _armyService = null!;
    private MessageService _messageService = null!;
    private CommanderService _commanderService = null!;

    [SetUp]
    public async Task SetUp()
    {
        await SeedHelpers.SeedMapAsync(Context, 5, 5);
        _mapService = new MapService(Context);
        _armyService = new ArmyService(Context);
        _messageService = new MessageService(Context);
        _commanderService = new CommanderService(Context);

        var mockGameRules = new Mock<IGameRulesService>();
        mockGameRules.Setup(s => s.Rules).Returns(GameRulesService.CreateDefaults());

        var mockFactionRules = new Mock<IFactionRuleService>();
        mockFactionRules.Setup(s => s.PreloadForFactionAsync(It.IsAny<int>())).Returns(Task.CompletedTask);
        mockFactionRules.Setup(s => s.GetCachedRuleValue(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<double>()))
            .Returns((int _, string _, double d) => d);

        _pathfindingService = new PathfindingService(
            _mapService, _messageService, _armyService, _commanderService,
            mockGameRules.Object, mockFactionRules.Object);
    }

    [Test]
    public async Task FindPath_OnRealMap_ReturnsValidPath()
    {
        var hexes = GridHexes.ToList();
        var start = hexes.First().ToHex();
        var end = hexes.Last().ToHex();

        // Add some roads to make a clear path
        for (int i = 0; i < hexes.Count - 1; i++)
        {
            var dir = MapService.GetNeighborDirection(hexes[i].ToHex(), hexes[i + 1].ToHex());
            if (dir != null)
                await _mapService.SetRoadAsync(hexes[i].ToHex(), dir.Value, true);
        }

        var result = await _pathfindingService.FindPathAsync(start, end);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Path.Count, Is.GreaterThan(0));
        // Path ends at destination
        Assert.That(result.Path[^1].q, Is.EqualTo(end.q));
        Assert.That(result.Path[^1].r, Is.EqualTo(end.r));
    }

    [Test]
    public async Task FindPath_AroundWater_OnRealMap()
    {
        var water = await SeedHelpers.SeedWaterTerrainAsync(Context);

        // Get two hexes on opposite sides
        var hexes = GridHexes.OrderBy(h => h.Q).ThenBy(h => h.R).ToList();
        var start = hexes.First().ToHex();
        var end = hexes.Last().ToHex();

        // Block some middle hexes with water
        var middleHexes = hexes.Skip(hexes.Count / 3).Take(2).ToList();
        foreach (var mh in middleHexes)
        {
            await _mapService.SetTerrainAsync(mh.ToHex(), water.Id);
        }

        var result = await _pathfindingService.FindPathAsync(start, end);

        Assert.That(result.Success, Is.True);
        // Path should not include any water hexes
        foreach (var pathHex in result.Path)
        {
            var mapHex = await _mapService.GetHexAsync(pathHex);
            Assert.That(mapHex!.TerrainType!.IsWater, Is.False,
                $"Path includes water hex at ({pathHex.q},{pathHex.r})");
        }
    }

    [Test]
    public async Task MoveArmy_PersistsLocationChange()
    {
        var hexes = GridHexes.ToList();
        var startHex = hexes[0];
        var endHex = hexes[1];

        // Road for fast movement
        var dir = MapService.GetNeighborDirection(startHex.ToHex(), endHex.ToHex());
        if (dir != null)
            await _mapService.SetRoadAsync(startHex.ToHex(), dir.Value, true);

        var army = await _armyService.CreateAsync(new Army
        {
            Name = "PathArmy",
            FactionId = 1,
            CoordinateQ = startHex.Q,
            CoordinateR = startHex.R,
            Path = new List<Hex> { endHex.ToHex() }
        });

        // Army rate 1.0, road cost 6 → need 6 hours
        var noon = new DateTime(2024, 1, 1, 12, 0, 0);
        int totalMoved = 0;
        for (int i = 0; i < 6; i++)
        {
            totalMoved += await _pathfindingService.MoveArmy(army, 1, noon);
        }

        // Reload from DB to verify persistence
        var reloaded = await _armyService.GetByIdAsync(army.Id);
        Assert.That(reloaded!.CoordinateQ, Is.EqualTo(endHex.Q));
        Assert.That(reloaded.CoordinateR, Is.EqualTo(endHex.R));
        Assert.That(totalMoved, Is.EqualTo(1));
    }
}
