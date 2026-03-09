using Hexes;
using Moq;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Discord;
using MechanicalCataphract.Services;
using MechanicalCataphract.Services.Calendar;

namespace MechanicalCataphract.Tests.Services.Integration;

[TestFixture]
public class TimeAdvanceServiceIntegrationTests : IntegrationTestBase
{
    private TimeAdvanceService _timeAdvanceService = null!;
    private GameStateService _gameStateService = null!;
    private ArmyService _armyService = null!;
    private MessageService _messageService = null!;
    private MapService _mapService = null!;
    private CommanderService _commanderService = null!;
    private PathfindingService _pathfindingService = null!;
    private CoLocationChannelService _coLocationChannelService = null!;
    private ICalendarService _calendarService = null!;

    [SetUp]
    public async Task SetUp()
    {
        await SeedHelpers.SeedMapAsync(Context, 5, 5);

        _gameStateService = new GameStateService(Context);
        _armyService = new ArmyService(Context, new FactionRuleService(Context));
        _messageService = new MessageService(Context);
        _mapService = new MapService(Context);
        _commanderService = new CommanderService(Context);
        var mockGameRules = new Mock<IGameRulesService>();
        mockGameRules.Setup(s => s.Rules).Returns(GameRulesService.CreateDefaults());
        var mockFactionRules = new Mock<IFactionRuleService>();
        mockFactionRules.Setup(s => s.PreloadForFactionAsync(It.IsAny<int>())).Returns(Task.CompletedTask);
        mockFactionRules.Setup(s => s.GetCachedRuleValue(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<double>()))
            .Returns((int _, string _, double d) => d);

        var calDef = CalendarDefinitionService.CreateHardcodedDefault();
        var mockCalDef = new Mock<ICalendarDefinitionService>();
        mockCalDef.Setup(s => s.GetCalendarDefinition()).Returns(calDef);
        _calendarService = new CalendarService(mockCalDef.Object);

        _pathfindingService = new PathfindingService(_mapService, _messageService, _armyService, _commanderService,
            mockGameRules.Object, mockFactionRules.Object, _calendarService);
        _coLocationChannelService = new CoLocationChannelService(Context);
        var discordChannelManager = new Mock<IDiscordChannelManager>();
        var newsService = new Mock<INewsService>();
        newsService.Setup(s => s.ProcessEventDeliveriesAsync(It.IsAny<long>())).ReturnsAsync(0);
        var weatherService = new Mock<IWeatherService>();
        weatherService.Setup(s => s.UpdateDailyWeatherAsync(It.IsAny<long>())).ReturnsAsync(0);
        _timeAdvanceService = new TimeAdvanceService(
            Context, _gameStateService, _armyService, _messageService,
            _mapService, _pathfindingService, _commanderService,
            _coLocationChannelService, discordChannelManager.Object, newsService.Object,
            weatherService.Object, _calendarService);

        // Pin game time to worldHour 8 (hour 8 of day 0 = within march window 8..20)
        await _gameStateService.SetCurrentWorldHourAsync(8);
    }

    [Test]
    public async Task AdvanceTime_UpdatesWorldHour()
    {
        var before = await _gameStateService.GetCurrentWorldHourAsync();

        var result = await _timeAdvanceService.AdvanceTimeAsync(1);

        Assert.That(result.Success, Is.True);
        var after = await _gameStateService.GetCurrentWorldHourAsync();
        Assert.That(after, Is.EqualTo(before + 1));
    }

    [Test]
    public async Task AdvanceTime_ReturnsFormattedTime()
    {
        var result = await _timeAdvanceService.AdvanceTimeAsync(1);

        Assert.That(result.Success, Is.True);
        Assert.That(result.FormattedTime, Is.Not.Empty);
        // Default calendar: "Year 1000, Dawnmarch 1, Firstday, 09:00"
        Assert.That(result.FormattedTime, Does.StartWith("Year 1000"));
    }

    [Test]
    public async Task AdvanceTime_MovesMessages()
    {
        var hexes = GridHexes.ToList();
        var startHex = hexes[0];
        var endHex = hexes[1];

        await _mapService.SetRoadAsync(startHex.ToHex(), MapService.GetNeighborDirection(startHex.ToHex(), endHex.ToHex()) ?? 0, true);

        var sender = await SeedHelpers.SeedCommanderAsync(Context, "Sender", 1, startHex.Q, startHex.R);
        var target = await SeedHelpers.SeedCommanderAsync(Context, "Target", 1, endHex.Q, endHex.R);

        var msg = await _messageService.CreateAsync(new Message
        {
            SenderCommanderId = sender.Id,
            TargetCommanderId = target.Id,
            Content = "Test",
            CoordinateQ = startHex.Q,
            CoordinateR = startHex.R,
            Path = new List<Hex> { endHex.ToHex() }
        });

        // Road cost 6, messenger rate 2 → needs 3 hours
        TimeAdvanceResult result = null!;
        for (int i = 0; i < 3; i++)
        {
            result = await _timeAdvanceService.AdvanceTimeAsync(1);
        }

        Assert.That(result!.Success, Is.True);
        var reloaded = await _messageService.GetByIdAsync(msg.Id);
        Assert.That(reloaded!.CoordinateQ, Is.EqualTo(endHex.Q));
        Assert.That(reloaded.CoordinateR, Is.EqualTo(endHex.R));
    }

    [Test]
    public async Task AdvanceTime_MovesArmies()
    {
        var hexes = GridHexes.ToList();
        var startHex = hexes[0];
        var endHex = hexes[1];

        var dir = MapService.GetNeighborDirection(startHex.ToHex(), endHex.ToHex());
        if (dir != null)
            await _mapService.SetRoadAsync(startHex.ToHex(), dir.Value, true);

        var army = await _armyService.CreateAsync(new Army
        {
            Name = "Marching",
            FactionId = 1,
            CoordinateQ = startHex.Q,
            CoordinateR = startHex.R,
            Path = new List<Hex> { endHex.ToHex() }
        });

        // Empty army: rate=1.0, road cost=6 → needs 6 valid march hours
        // Starting at worldHour 8, march window is 8..19 (20 exclusive), so 12 ticks covers it
        for (int i = 0; i < 12; i++)
        {
            await _timeAdvanceService.AdvanceTimeAsync(1);
        }

        var reloaded = await _armyService.GetByIdAsync(army.Id);
        Assert.That(reloaded!.CoordinateQ, Is.EqualTo(endHex.Q));
        Assert.That(reloaded.CoordinateR, Is.EqualTo(endHex.R));
    }

    [Test]
    public async Task AdvanceTime_MovesCommanders()
    {
        var hexes = GridHexes.ToList();
        var startHex = hexes[0];
        var endHex = hexes[1];

        var dir = MapService.GetNeighborDirection(startHex.ToHex(), endHex.ToHex());
        if (dir != null)
            await _mapService.SetRoadAsync(startHex.ToHex(), dir.Value, true);

        var commander = await _commanderService.CreateAsync(new Commander
        {
            Name = "Traveler",
            FactionId = 1,
            CoordinateQ = startHex.Q,
            CoordinateR = startHex.R,
            Path = new List<Hex> { endHex.ToHex() }
        });

        // Commander rate 2, road cost 6 → needs 3 hours
        for (int i = 0; i < 3; i++)
        {
            await _timeAdvanceService.AdvanceTimeAsync(1);
        }

        var reloaded = await _commanderService.GetByIdAsync(commander.Id);
        Assert.That(reloaded!.CoordinateQ, Is.EqualTo(endHex.Q));
        Assert.That(reloaded.CoordinateR, Is.EqualTo(endHex.R));
    }

    [Test]
    public async Task AdvanceTime_ProcessesSupplyAtCorrectHour()
    {
        var hexes = GridHexes.ToList();
        var hex = hexes[0];

        var army = await _armyService.CreateAsync(new Army
        {
            Name = "Hungry",
            FactionId = 1,
            CoordinateQ = hex.Q,
            CoordinateR = hex.R,
            CarriedSupply = 10000
        });
        await SeedHelpers.SeedBrigadeAsync(Context, army.Id, "Inf", 100, UnitType.Infantry);

        // Supply trigger is at hour 21. Set worldHour to 20 so advancing 1 hour crosses it.
        await _gameStateService.SetCurrentWorldHourAsync(20);

        var result = await _timeAdvanceService.AdvanceTimeAsync(1);

        Assert.That(result.Success, Is.True);
        Assert.That(result.ArmiesSupplied, Is.GreaterThan(0));
        var reloaded = await _armyService.GetByIdAsync(army.Id);
        Assert.That(reloaded!.CarriedSupply, Is.LessThan(10000));
    }

    [Test]
    public async Task AdvanceTime_FollowingCommander_MovesWithArmy()
    {
        var hexes = GridHexes.ToList();
        var startHex = hexes[0];
        var endHex = hexes[1];

        var dir = MapService.GetNeighborDirection(startHex.ToHex(), endHex.ToHex());
        if (dir != null)
            await _mapService.SetRoadAsync(startHex.ToHex(), dir.Value, true);

        var army = await _armyService.CreateAsync(new Army
        {
            Name = "Marching",
            FactionId = 1,
            CoordinateQ = startHex.Q,
            CoordinateR = startHex.R,
            Path = new List<Hex> { endHex.ToHex() }
        });

        var commander = await _commanderService.CreateAsync(new Commander
        {
            Name = "Follower",
            FactionId = 1,
            CoordinateQ = startHex.Q,
            CoordinateR = startHex.R,
            FollowingArmyId = army.Id
        });

        for (int i = 0; i < 12; i++)
        {
            await _timeAdvanceService.AdvanceTimeAsync(1);
        }

        var reloadedArmy = await _armyService.GetByIdAsync(army.Id);
        var reloadedCommander = await _commanderService.GetByIdAsync(commander.Id);

        Assert.That(reloadedArmy!.CoordinateQ, Is.EqualTo(endHex.Q));
        Assert.That(reloadedArmy.CoordinateR, Is.EqualTo(endHex.R));
        Assert.That(reloadedCommander!.CoordinateQ, Is.EqualTo(endHex.Q));
        Assert.That(reloadedCommander.CoordinateR, Is.EqualTo(endHex.R));
    }

    [Test]
    public async Task AdvanceTime_FollowingCommander_DoesNotUseOwnPath()
    {
        var hexes = GridHexes.ToList();
        var startHex = hexes[0];
        var otherHex = hexes.Count > 2 ? hexes[2] : hexes[1];

        var army = await _armyService.CreateAsync(new Army
        {
            Name = "Stationary",
            FactionId = 1,
            CoordinateQ = startHex.Q,
            CoordinateR = startHex.R
        });

        var commander = await _commanderService.CreateAsync(new Commander
        {
            Name = "FollowerWithPath",
            FactionId = 1,
            CoordinateQ = startHex.Q,
            CoordinateR = startHex.R,
            FollowingArmyId = army.Id,
            Path = new List<Hex> { otherHex.ToHex() }
        });

        for (int i = 0; i < 6; i++)
        {
            await _timeAdvanceService.AdvanceTimeAsync(1);
        }

        var reloadedCommander = await _commanderService.GetByIdAsync(commander.Id);
        Assert.That(reloadedCommander!.CoordinateQ, Is.EqualTo(startHex.Q));
        Assert.That(reloadedCommander.CoordinateR, Is.EqualTo(startHex.R));
    }

    [Test]
    public async Task AdvanceTime_TransactionRollsBackOnError()
    {
        var before = await _gameStateService.GetCurrentWorldHourAsync();

        var result = await _timeAdvanceService.AdvanceTimeAsync(1);

        Assert.That(result.Success, Is.True);
        var after = await _gameStateService.GetCurrentWorldHourAsync();
        Assert.That(after, Is.EqualTo(before + 1));
    }
}
