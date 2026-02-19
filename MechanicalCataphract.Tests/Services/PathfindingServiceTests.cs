using System;
using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using MechanicalCataphract.Tests.Helpers;
using Moq;

namespace MechanicalCataphract.Tests.Services;

[TestFixture]
public class PathfindingServiceTests
{
    private Mock<IMessageService> _mockMessageService = null!;
    private Mock<IArmyService> _mockArmyService = null!;
    private Mock<ICommanderService> _mockCommanderService = null!;
    private Mock<IGameRulesService> _mockGameRulesService = null!;
    private Mock<IFactionRuleService> _mockFactionRuleService = null!;

    [SetUp]
    public void Setup()
    {
        // Set up static GameRules accessor so entity computed properties don't throw
        GameRules.SetForTesting(GameRulesService.CreateDefaults());

        _mockMessageService = new Mock<IMessageService>();
        _mockArmyService = new Mock<IArmyService>();
        _mockCommanderService = new Mock<ICommanderService>();

        _mockGameRulesService = new Mock<IGameRulesService>();
        _mockGameRulesService.Setup(s => s.Rules).Returns(GameRulesService.CreateDefaults());

        _mockFactionRuleService = new Mock<IFactionRuleService>();
        _mockFactionRuleService
            .Setup(s => s.PreloadForFactionAsync(It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        _mockFactionRuleService
            .Setup(s => s.GetCachedRuleValue(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<double>()))
            .Returns((int _, string _, double d) => d);
    }

    private PathfindingService CreateService(Mock<IMapService> mockMapService)
    {
        return new PathfindingService(
            mockMapService.Object,
            _mockMessageService.Object,
            _mockArmyService.Object,
            _mockCommanderService.Object,
            _mockGameRulesService.Object,
            _mockFactionRuleService.Object);
    }

    #region FindPathAsync Tests

    [Test]
    public async Task StartEqualsEnd_ReturnsEmptyPath()
    {
        var builder = new TestMapBuilder().AddHex(0, 0);
        var service = CreateService(builder.BuildMockMapService());
        var hex = new Hex(0, 0, 0);

        var result = await service.FindPathAsync(hex, hex);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Path, Is.Empty);
        Assert.That(result.TotalCost, Is.EqualTo(0));
    }

    [Test]
    public async Task StartNotFound_ReturnsFailure()
    {
        // Only add the end hex, not the start
        var builder = new TestMapBuilder().AddHex(1, 0);
        var service = CreateService(builder.BuildMockMapService());

        var result = await service.FindPathAsync(
            new Hex(0, 0, 0),
            new Hex(1, 0, -1));

        Assert.That(result.Success, Is.False);
        Assert.That(result.FailureReason, Does.Contain("Start"));
    }

    [Test]
    public async Task EndNotFound_ReturnsFailure()
    {
        var builder = new TestMapBuilder().AddHex(0, 0);
        var service = CreateService(builder.BuildMockMapService());

        var result = await service.FindPathAsync(
            new Hex(0, 0, 0),
            new Hex(5, 5, -10));

        Assert.That(result.Success, Is.False);
        Assert.That(result.FailureReason, Does.Contain("Target hex does not exist"));
    }

    [Test]
    public async Task EndIsWater_ReturnsFailure()
    {
        var builder = new TestMapBuilder()
            .AddHex(0, 0)
            .AddWaterHex(1, 0);
        var service = CreateService(builder.BuildMockMapService());

        var result = await service.FindPathAsync(
            new Hex(0, 0, 0),
            new Hex(1, 0, -1));

        Assert.That(result.Success, Is.False);
        Assert.That(result.FailureReason, Does.Contain("water"));
    }

    [Test]
    public async Task AdjacentHexes_NoRoad_ReturnsPath()
    {
        var builder = new TestMapBuilder()
            .AddHex(0, 0)
            .AddHex(1, 0);
        var service = CreateService(builder.BuildMockMapService());

        var result = await service.FindPathAsync(
            new Hex(0, 0, 0),
            new Hex(1, 0, -1));

        Assert.That(result.Success, Is.True);
        Assert.That(result.Path, Has.Count.EqualTo(1));
        Assert.That(result.TotalCost, Is.EqualTo(12)); // OffRoadCost
    }

    [Test]
    public async Task AdjacentHexes_WithRoad_ReturnsCheaperPath()
    {
        var builder = new TestMapBuilder()
            .AddHex(0, 0)
            .AddHex(1, 0)
            .AddRoad(0, 0, 0); // Direction 0 from (0,0) → (1,0)
        var service = CreateService(builder.BuildMockMapService());

        var result = await service.FindPathAsync(
            new Hex(0, 0, 0),
            new Hex(1, 0, -1));

        Assert.That(result.Success, Is.True);
        Assert.That(result.TotalCost, Is.EqualTo(6)); // RoadCost
    }

    [Test]
    public async Task PathAroundWater_AvoidsWaterHex()
    {
        // Layout: (0,0) -- water(1,0) -- (2,0)
        //              \--- (0,-1) ---/
        // Path must go around the water via direction 1 neighbor
        var builder = new TestMapBuilder()
            .AddHex(0, 0)
            .AddWaterHex(1, 0)    // blocked
            .AddHex(1, -1)        // go-around
            .AddHex(2, -1);

        var service = CreateService(builder.BuildMockMapService());

        var result = await service.FindPathAsync(
            new Hex(0, 0, 0),
            new Hex(2, -1, -1));

        Assert.That(result.Success, Is.True);
        // Path should not contain the water hex
        Assert.That(result.Path.Any(h => h.q == 1 && h.r == 0), Is.False);
    }

    [Test]
    public async Task NoPathExists_ReturnsFailure()
    {
        // Island: start hex surrounded by water on all sides, end hex unreachable
        var builder = new TestMapBuilder()
            .AddHex(0, 0)
            .AddWaterHex(1, 0)
            .AddWaterHex(1, -1)
            .AddWaterHex(0, -1)
            .AddWaterHex(-1, 0)
            .AddWaterHex(-1, 1)
            .AddWaterHex(0, 1)
            .AddHex(3, 0); // Unreachable
        var service = CreateService(builder.BuildMockMapService());

        var result = await service.FindPathAsync(
            new Hex(0, 0, 0),
            new Hex(3, 0, -3));

        Assert.That(result.Success, Is.False);
        Assert.That(result.FailureReason, Does.Contain("No path"));
    }

    [Test]
    public async Task PrefersRoads_WhenAvailable()
    {
        // Two routes from (0,0) to (2,0):
        //   Route A (road): (0,0) → (1,0) → (2,0)  cost = 6 + 6 = 12
        //   Route B (off-road): (0,0) → (1,-1) → (2,0)  cost = 12 + 12 = 24
        var builder = new TestMapBuilder()
            .AddHex(0, 0)
            .AddHex(1, 0)
            .AddHex(2, 0)
            .AddHex(1, -1)
            .AddRoad(0, 0, 0)   // (0,0) → (1,0)
            .AddRoad(1, 0, 0);  // (1,0) → (2,0)
        var service = CreateService(builder.BuildMockMapService());

        var result = await service.FindPathAsync(
            new Hex(0, 0, 0),
            new Hex(2, 0, -2));

        Assert.That(result.Success, Is.True);
        Assert.That(result.TotalCost, Is.EqualTo(12)); // 2 × RoadCost
        // Verify path goes through (1,0) not (1,-1)
        Assert.That(result.Path[0].q, Is.EqualTo(1));
        Assert.That(result.Path[0].r, Is.EqualTo(0));
    }

    [Test]
    public async Task ArmyEntityType_IncreasedCost()
    {
        var builder = new TestMapBuilder()
            .AddHex(0, 0)
            .AddHex(1, 0);
        var service = CreateService(builder.BuildMockMapService());

        var result = await service.FindPathAsync(
            new Hex(0, 0, 0),
            new Hex(1, 0, -1),
            TravelEntityType.Army);

        Assert.That(result.Success, Is.True);
        // Army cost = OffRoadCost(12) × 1.5 = 18
        Assert.That(result.TotalCost, Is.EqualTo(18));
    }

    [Test]
    public async Task PathExcludesStartHex()
    {
        var builder = new TestMapBuilder()
            .AddHex(0, 0)
            .AddHex(1, 0)
            .AddHex(2, 0);
        var service = CreateService(builder.BuildMockMapService());

        var result = await service.FindPathAsync(
            new Hex(0, 0, 0),
            new Hex(2, 0, -2));

        Assert.That(result.Success, Is.True);
        Assert.That(result.Path.Any(h => h.q == 0 && h.r == 0), Is.False);
    }

    [Test]
    public async Task StraightLine_ReturnsOptimalPath()
    {
        // 5 hexes in a line with roads: (0,0)→(1,0)→(2,0)→(3,0)→(4,0)
        var builder = new TestMapBuilder();
        for (int q = 0; q <= 4; q++)
            builder.AddHex(q, 0);
        for (int q = 0; q < 4; q++)
            builder.AddRoad(q, 0, 0);

        var service = CreateService(builder.BuildMockMapService());

        var result = await service.FindPathAsync(
            new Hex(0, 0, 0),
            new Hex(4, 0, -4));

        Assert.That(result.Success, Is.True);
        Assert.That(result.Path, Has.Count.EqualTo(4)); // excludes start
        Assert.That(result.TotalCost, Is.EqualTo(4 * 6)); // 4 × RoadCost
    }

    #endregion

    #region MoveMessage Tests

    [Test]
    public async Task MoveMessage_NullLocation_ReturnsZero()
    {
        var builder = new TestMapBuilder();
        var service = CreateService(builder.BuildMockMapService());

        var message = new Message { CoordinateQ = null, CoordinateR = null };
        var result = await service.MoveMessage(message, 1);
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public async Task MoveMessage_EmptyPath_ReturnsZero()
    {
        var builder = new TestMapBuilder();
        var service = CreateService(builder.BuildMockMapService());

        var message = new Message
        {
            CoordinateQ = 0, CoordinateR = 0,
            Path = new List<Hex>()
        };
        var result = await service.MoveMessage(message, 1);
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public async Task MoveMessage_InsufficientTime_IncrementsTimeInTransit()
    {
        var builder = new TestMapBuilder()
            .AddHex(0, 0)
            .AddHex(1, 0);
        var mockMap = builder.BuildMockMapService();
        // No road → off-road cost 12, rate 2 → threshold = 6 hours
        mockMap.Setup(m => m.HasRoadBetweenAsync(It.IsAny<Hex>(), It.IsAny<Hex>()))
            .ReturnsAsync(false);
        var service = CreateService(mockMap);

        var message = new Message
        {
            CoordinateQ = 0, CoordinateR = 0,
            Path = new List<Hex> { new Hex(1, 0, -1) },
            TimeInTransit = 0
        };

        var result = await service.MoveMessage(message, 1); // 1 hour < 6 threshold

        Assert.That(result, Is.EqualTo(0));
        Assert.That(message.TimeInTransit, Is.EqualTo(1));
    }

    [Test]
    public async Task MoveMessage_SufficientTime_MovesToNextHex()
    {
        var builder = new TestMapBuilder()
            .AddHex(0, 0)
            .AddHex(1, 0);
        var mockMap = builder.BuildMockMapService();
        mockMap.Setup(m => m.HasRoadBetweenAsync(It.IsAny<Hex>(), It.IsAny<Hex>()))
            .ReturnsAsync(false);
        var service = CreateService(mockMap);

        var nextHex = new Hex(1, 0, -1);
        var message = new Message
        {
            CoordinateQ = 0, CoordinateR = 0,
            Path = new List<Hex> { nextHex },
            TimeInTransit = 0
        };

        // Off-road cost=12, rate=2 → threshold=6. Give exactly 6 hours.
        var result = await service.MoveMessage(message, 6);

        Assert.That(result, Is.EqualTo(1));
        Assert.That(message.CoordinateQ, Is.EqualTo(1));
        Assert.That(message.CoordinateR, Is.EqualTo(0));
        Assert.That(message.Path, Is.Empty);
    }

    [Test]
    public async Task MoveMessage_OnRoad_MovesAtRoadSpeed()
    {
        var builder = new TestMapBuilder()
            .AddHex(0, 0)
            .AddHex(1, 0)
            .AddRoad(0, 0, 0);
        var mockMap = builder.BuildMockMapService();
        var service = CreateService(mockMap);

        var nextHex = new Hex(1, 0, -1);
        var message = new Message
        {
            CoordinateQ = 0, CoordinateR = 0,
            Path = new List<Hex> { nextHex },
            TimeInTransit = 0
        };

        // Road cost=6, rate=2 → threshold=3 hours
        var result = await service.MoveMessage(message, 3);

        Assert.That(result, Is.EqualTo(1));
        Assert.That(message.CoordinateQ, Is.EqualTo(1));
    }

    #endregion

    #region MoveArmy Tests

    [Test]
    public async Task MoveArmy_NullLocation_ReturnsZero()
    {
        var builder = new TestMapBuilder();
        var service = CreateService(builder.BuildMockMapService());
        var noon = new DateTime(2024, 1, 1, 12, 0, 0);

        var army = new Army { CoordinateQ = null, CoordinateR = null };
        var result = await service.MoveArmy(army, 1, noon);
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public async Task MoveArmy_EmptyPath_ReturnsZero()
    {
        var builder = new TestMapBuilder();
        var service = CreateService(builder.BuildMockMapService());
        var noon = new DateTime(2024, 1, 1, 12, 0, 0);

        var army = new Army
        {
            CoordinateQ = 0, CoordinateR = 0,
            Path = new List<Hex>()
        };
        var result = await service.MoveArmy(army, 1, noon);
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public async Task MoveArmy_SlowerThanMessage()
    {
        // Army rate = 1.0, Message rate = 2
        // Off-road cost 12: Army threshold = 12/1.0 = 12h, Message threshold = 12/2 = 6h
        var builder = new TestMapBuilder()
            .AddHex(0, 0)
            .AddHex(1, 0);
        var mockMap = builder.BuildMockMapService();
        mockMap.Setup(m => m.HasRoadBetweenAsync(It.IsAny<Hex>(), It.IsAny<Hex>()))
            .ReturnsAsync(false);
        var service = CreateService(mockMap);
        var noon = new DateTime(2024, 1, 1, 12, 0, 0);

        var nextHex = new Hex(1, 0, -1);

        // Army with 6 hours should NOT move (needs 12)
        var army = new Army
        {
            CoordinateQ = 0, CoordinateR = 0,
            Path = new List<Hex> { nextHex },
            TimeInTransit = 0
        };
        var armyResult = await service.MoveArmy(army, 6, noon);
        Assert.That(armyResult, Is.EqualTo(0));

        // Message with 12 hours SHOULD move (needs 6)
        var message = new Message
        {
            CoordinateQ = 0, CoordinateR = 0,
            Path = new List<Hex> { nextHex },
            TimeInTransit = 0
        };
        var msgResult = await service.MoveMessage(message, 12);
        Assert.That(msgResult, Is.EqualTo(1));
    }

    [Test]
    public async Task MoveArmy_SufficientTime_MovesToNextHex()
    {
        var builder = new TestMapBuilder()
            .AddHex(0, 0)
            .AddHex(1, 0);
        var mockMap = builder.BuildMockMapService();
        mockMap.Setup(m => m.HasRoadBetweenAsync(It.IsAny<Hex>(), It.IsAny<Hex>()))
            .ReturnsAsync(false);
        var service = CreateService(mockMap);
        var noon = new DateTime(2024, 1, 1, 12, 0, 0);

        var nextHex = new Hex(1, 0, -1);
        var army = new Army
        {
            CoordinateQ = 0, CoordinateR = 0,
            Path = new List<Hex> { nextHex },
            TimeInTransit = 0
        };

        // Off-road cost=12, rate=1.0 → threshold=12
        var result = await service.MoveArmy(army, 12, noon);

        Assert.That(result, Is.EqualTo(1));
        Assert.That(army.CoordinateQ, Is.EqualTo(1));
        Assert.That(army.CoordinateR, Is.EqualTo(0));
        Assert.That(army.Path, Is.Empty);
    }

    #endregion

    #region MoveCommander Tests

    [Test]
    public async Task MoveCommander_NullLocation_ReturnsZero()
    {
        var builder = new TestMapBuilder();
        var service = CreateService(builder.BuildMockMapService());

        var commander = new Commander { CoordinateQ = null, CoordinateR = null };
        var result = await service.MoveCommander(commander, 1);
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public async Task MoveCommander_FollowingArmy_ReturnsZero()
    {
        var builder = new TestMapBuilder()
            .AddHex(0, 0)
            .AddHex(1, 0);
        var mockMap = builder.BuildMockMapService();
        mockMap.Setup(m => m.HasRoadBetweenAsync(It.IsAny<Hex>(), It.IsAny<Hex>()))
            .ReturnsAsync(false);
        var service = CreateService(mockMap);

        var commander = new Commander
        {
            CoordinateQ = 0, CoordinateR = 0,
            Path = new List<Hex> { new Hex(1, 0, -1) },
            TimeInTransit = 0,
            FollowingArmyId = 42
        };

        // Even with enough time to move, should return 0 because following an army
        var result = await service.MoveCommander(commander, 6);

        Assert.That(result, Is.EqualTo(0));
        // Coordinates unchanged
        Assert.That(commander.CoordinateQ, Is.EqualTo(0));
        Assert.That(commander.CoordinateR, Is.EqualTo(0));
    }

    [Test]
    public async Task MoveCommander_NotFollowingArmy_MovesNormally()
    {
        var builder = new TestMapBuilder()
            .AddHex(0, 0)
            .AddHex(1, 0);
        var mockMap = builder.BuildMockMapService();
        mockMap.Setup(m => m.HasRoadBetweenAsync(It.IsAny<Hex>(), It.IsAny<Hex>()))
            .ReturnsAsync(false);
        var service = CreateService(mockMap);

        var commander = new Commander
        {
            CoordinateQ = 0, CoordinateR = 0,
            Path = new List<Hex> { new Hex(1, 0, -1) },
            TimeInTransit = 0,
            FollowingArmyId = null
        };

        // Off-road cost=12, rate=2 → threshold=6
        var result = await service.MoveCommander(commander, 6);

        Assert.That(result, Is.EqualTo(1));
        Assert.That(commander.CoordinateQ, Is.EqualTo(1));
    }

    [Test]
    public async Task MoveCommander_SufficientTime_MovesToNextHex()
    {
        var builder = new TestMapBuilder()
            .AddHex(0, 0)
            .AddHex(1, 0);
        var mockMap = builder.BuildMockMapService();
        mockMap.Setup(m => m.HasRoadBetweenAsync(It.IsAny<Hex>(), It.IsAny<Hex>()))
            .ReturnsAsync(false);
        var service = CreateService(mockMap);

        var nextHex = new Hex(1, 0, -1);
        var commander = new Commander
        {
            CoordinateQ = 0, CoordinateR = 0,
            Path = new List<Hex> { nextHex },
            TimeInTransit = 0
        };

        // Off-road cost=12, rate=2 → threshold=6
        var result = await service.MoveCommander(commander, 6);

        Assert.That(result, Is.EqualTo(1));
        Assert.That(commander.CoordinateQ, Is.EqualTo(1));
        Assert.That(commander.CoordinateR, Is.EqualTo(0));
        Assert.That(commander.Path, Is.Empty);
    }

    [Test]
    public async Task MoveCommander_OnRoad_MovesAtRoadSpeed()
    {
        var builder = new TestMapBuilder()
            .AddHex(0, 0)
            .AddHex(1, 0)
            .AddRoad(0, 0, 0);
        var mockMap = builder.BuildMockMapService();
        var service = CreateService(mockMap);

        var nextHex = new Hex(1, 0, -1);
        var commander = new Commander
        {
            CoordinateQ = 0, CoordinateR = 0,
            Path = new List<Hex> { nextHex },
            TimeInTransit = 0
        };

        // Road cost=6, rate=2 → threshold=3
        var result = await service.MoveCommander(commander, 3);

        Assert.That(result, Is.EqualTo(1));
        Assert.That(commander.CoordinateQ, Is.EqualTo(1));
    }

    #endregion
}
