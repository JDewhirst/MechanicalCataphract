using Moq;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using MechanicalCataphract.Services.Calendar;

namespace MechanicalCataphract.Tests.Services.Integration;

[TestFixture]
public class WeatherServiceIntegrationTests : IntegrationTestBase
{
    private WeatherService _weatherService = null!;
    private Mock<IGameRulesService> _mockGameRules = null!;
    private ICalendarService _calendarService = null!;

    // H=24, so worldHour/24 = day index. Day 0 = worldHour 0..23, day 1 = 24..47, etc.
    private const int H = 24;

    [SetUp]
    public async Task SetUp()
    {
        await SeedHelpers.SeedMapAsync(Context, 3, 3);

        _mockGameRules = new Mock<IGameRulesService>();
        _mockGameRules.Setup(s => s.Rules).Returns(GameRulesService.CreateDefaults());

        var calDef = CalendarDefinitionService.CreateHardcodedDefault();
        var mockCalDef = new Mock<ICalendarDefinitionService>();
        mockCalDef.Setup(s => s.GetCalendarDefinition()).Returns(calDef);
        _calendarService = new CalendarService(mockCalDef.Object);

        _weatherService = new WeatherService(Context, _mockGameRules.Object, _calendarService);
    }

    [Test]
    public async Task UpdateDailyWeather_AssignsWeatherToAllHexes()
    {
        long worldHour = 6; // hour 6 on day 0

        int updated = await _weatherService.UpdateDailyWeatherAsync(worldHour);

        Assert.That(updated, Is.GreaterThan(0));

        var hexes = Context.MapHexes.ToList();
        Assert.That(hexes.Count, Is.GreaterThan(0));
        Assert.That(hexes.All(h => h.WeatherId.HasValue), Is.True,
            "Every hex should have a WeatherId assigned after daily weather update");
    }

    [Test]
    public async Task UpdateDailyWeather_OnlyUsesWeatherTypesInTransitions()
    {
        // Default transitions only target Clear, Rain, Overcast, Fog
        // Storm and Snow are excluded from all transition rows
        var clearId = Context.WeatherTypes.Single(w => w.Name == "Clear").Id;
        foreach (var hex in Context.MapHexes.ToList())
            hex.WeatherId = clearId;
        await Context.SaveChangesAsync();

        await _weatherService.UpdateDailyWeatherAsync(0L);

        var hexes = Context.MapHexes.ToList();
        var assignedIds = hexes.Select(h => h.WeatherId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var assignedNames = Context.WeatherTypes
            .Where(w => assignedIds.Contains(w.Id))
            .Select(w => w.Name)
            .ToList();

        var allowedNames = new[] { "Clear", "Rain", "Overcast", "Fog" };
        Assert.That(assignedNames.All(n => allowedNames.Contains(n, StringComparer.OrdinalIgnoreCase)), Is.True,
            "Only weather types listed in transition rules should be assigned");
    }

    [Test]
    public async Task UpdateDailyWeather_IdempotentForSameDay()
    {
        long worldHour1 = 6L;   // hour 6, day 0
        long worldHour2 = 20L;  // hour 20, day 0 (same day)

        int firstUpdate = await _weatherService.UpdateDailyWeatherAsync(worldHour1);
        int secondUpdate = await _weatherService.UpdateDailyWeatherAsync(worldHour2);

        Assert.That(firstUpdate, Is.GreaterThan(0), "First call should update hexes");
        Assert.That(secondUpdate, Is.EqualTo(0), "Second call on same day should be skipped (gate)");

        // Only one WeatherUpdateRecord should exist
        var recordCount = Context.WeatherUpdateRecords.Count();
        Assert.That(recordCount, Is.EqualTo(1));
    }

    [Test]
    public async Task UpdateDailyWeather_RunsAgainOnNewDay()
    {
        long day1Hour = 6L;      // day 0
        long day2Hour = 24 + 6L; // day 1

        int day1Update = await _weatherService.UpdateDailyWeatherAsync(day1Hour);
        int day2Update = await _weatherService.UpdateDailyWeatherAsync(day2Hour);

        Assert.That(day1Update, Is.GreaterThan(0));
        Assert.That(day2Update, Is.GreaterThan(0), "A new day should trigger a fresh weather update");

        var recordCount = Context.WeatherUpdateRecords.Count();
        Assert.That(recordCount, Is.EqualTo(2));
    }

    [Test]
    public async Task UpdateDailyWeather_ReturnsZero_WhenNoTransitionsMatchDb()
    {
        var rulesWithNoMatch = new GameRulesData(
            GameRulesService.CreateDefaults().Movement,
            GameRulesService.CreateDefaults().MovementRates,
            GameRulesService.CreateDefaults().Supply,
            GameRulesService.CreateDefaults().Armies,
            GameRulesService.CreateDefaults().UnitStats,
            GameRulesService.CreateDefaults().News,
            new WeatherRules(6, new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, double>>
            {
                ["Clear"] = new() { ["Blizzard"] = 1.0 }  // target doesn't exist in seeded DB
            }),
            GameRulesService.CreateDefaults().Ships);
        _mockGameRules.Setup(s => s.Rules).Returns(rulesWithNoMatch);

        int updated = await _weatherService.UpdateDailyWeatherAsync(0L);

        Assert.That(updated, Is.EqualTo(0), "No update when no DB weather types match transition targets");
    }

    [Test]
    public async Task UpdateDailyWeather_UsesCurrentWeatherForTransition()
    {
        // Deterministic transitions: Clear->Overcast (100%), Rain->Fog (100%)
        var deterministicRules = new GameRulesData(
            GameRulesService.CreateDefaults().Movement,
            GameRulesService.CreateDefaults().MovementRates,
            GameRulesService.CreateDefaults().Supply,
            GameRulesService.CreateDefaults().Armies,
            GameRulesService.CreateDefaults().UnitStats,
            GameRulesService.CreateDefaults().News,
            new WeatherRules(6, new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, double>>
            {
                ["Clear"] = new() { ["Overcast"] = 1.0 },
                ["Rain"]  = new() { ["Fog"] = 1.0 }
            }),
            GameRulesService.CreateDefaults().Ships);
        _mockGameRules.Setup(s => s.Rules).Returns(deterministicRules);

        var weatherTypes = Context.WeatherTypes.ToList();
        var clearId = weatherTypes.Single(w => w.Name == "Clear").Id;
        var rainId  = weatherTypes.Single(w => w.Name == "Rain").Id;
        var overcastId = weatherTypes.Single(w => w.Name == "Overcast").Id;
        var fogId = weatherTypes.Single(w => w.Name == "Fog").Id;

        var hexes = Context.MapHexes.ToList();
        for (int i = 0; i < hexes.Count; i++)
            hexes[i].WeatherId = i % 2 == 0 ? clearId : rainId;
        await Context.SaveChangesAsync();

        await _weatherService.UpdateDailyWeatherAsync(0L);

        var updated = Context.MapHexes.ToList();
        for (int i = 0; i < updated.Count; i++)
        {
            if (i % 2 == 0)
                Assert.That(updated[i].WeatherId, Is.EqualTo(overcastId),
                    $"Hex {i} started as Clear, should transition to Overcast");
            else
                Assert.That(updated[i].WeatherId, Is.EqualTo(fogId),
                    $"Hex {i} started as Rain, should transition to Fog");
        }
    }

    [Test]
    public async Task UpdateDailyWeather_UsesDayIndex_NotTimeOfDay()
    {
        // Two calls on the same calendar day but different hours → second should be skipped
        long morningHour = 6L;  // day 0
        long eveningHour = 20L; // day 0

        int morningUpdate = await _weatherService.UpdateDailyWeatherAsync(morningHour);
        int eveningUpdate = await _weatherService.UpdateDailyWeatherAsync(eveningHour);

        Assert.That(morningUpdate, Is.GreaterThan(0));
        Assert.That(eveningUpdate, Is.EqualTo(0), "Same calendar day regardless of hour should be skipped");
    }
}
