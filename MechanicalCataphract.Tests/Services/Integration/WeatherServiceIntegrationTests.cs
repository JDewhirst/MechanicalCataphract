using Moq;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Tests.Services.Integration;

[TestFixture]
public class WeatherServiceIntegrationTests : IntegrationTestBase
{
    private WeatherService _weatherService = null!;
    private Mock<IGameRulesService> _mockGameRules = null!;

    [SetUp]
    public async Task SetUp()
    {
        await SeedHelpers.SeedMapAsync(Context, 3, 3);

        _mockGameRules = new Mock<IGameRulesService>();
        _mockGameRules.Setup(s => s.Rules).Returns(GameRulesService.CreateDefaults());

        _weatherService = new WeatherService(Context, _mockGameRules.Object);
    }

    [Test]
    public async Task UpdateDailyWeather_AssignsWeatherToAllHexes()
    {
        var gameDate = new DateTime(1805, 6, 15, 6, 0, 0);

        int updated = await _weatherService.UpdateDailyWeatherAsync(gameDate);

        Assert.That(updated, Is.GreaterThan(0));

        var hexes = Context.MapHexes.ToList();
        Assert.That(hexes.Count, Is.GreaterThan(0));
        Assert.That(hexes.All(h => h.WeatherId.HasValue), Is.True,
            "Every hex should have a WeatherId assigned after daily weather update");
    }

    [Test]
    public async Task UpdateDailyWeather_OnlyUsesWeatherTypesInProbabilities()
    {
        // Default probabilities only cover Clear, Rain, Overcast, Fog (ids 1,2,5,6)
        // Storm (3) and Snow (4) are excluded
        var gameDate = new DateTime(1805, 6, 15);

        await _weatherService.UpdateDailyWeatherAsync(gameDate);

        // All seeded weather types from EnsureCreated are in DB
        // Fetch weather names assigned to hexes
        var hexes = Context.MapHexes.ToList();
        var assignedIds = hexes.Select(h => h.WeatherId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var assignedNames = Context.WeatherTypes
            .Where(w => assignedIds.Contains(w.Id))
            .Select(w => w.Name)
            .ToList();

        var allowedNames = new[] { "Clear", "Rain", "Overcast", "Fog" };
        Assert.That(assignedNames.All(n => allowedNames.Contains(n, StringComparer.OrdinalIgnoreCase)), Is.True,
            "Only weather types listed in probability rules should be assigned");
    }

    [Test]
    public async Task UpdateDailyWeather_IdempotentForSameDate()
    {
        var gameDate = new DateTime(1805, 6, 15, 6, 0, 0);

        int firstUpdate = await _weatherService.UpdateDailyWeatherAsync(gameDate);
        int secondUpdate = await _weatherService.UpdateDailyWeatherAsync(gameDate);

        Assert.That(firstUpdate, Is.GreaterThan(0), "First call should update hexes");
        Assert.That(secondUpdate, Is.EqualTo(0), "Second call on same date should be skipped (gate)");

        // Only one WeatherUpdateRecord should exist
        var recordCount = Context.WeatherUpdateRecords.Count();
        Assert.That(recordCount, Is.EqualTo(1));
    }

    [Test]
    public async Task UpdateDailyWeather_RunsAgainOnNewDate()
    {
        var day1 = new DateTime(1805, 6, 15, 6, 0, 0);
        var day2 = new DateTime(1805, 6, 16, 6, 0, 0);

        int day1Update = await _weatherService.UpdateDailyWeatherAsync(day1);
        int day2Update = await _weatherService.UpdateDailyWeatherAsync(day2);

        Assert.That(day1Update, Is.GreaterThan(0));
        Assert.That(day2Update, Is.GreaterThan(0), "A new date should trigger a fresh weather update");

        var recordCount = Context.WeatherUpdateRecords.Count();
        Assert.That(recordCount, Is.EqualTo(2));
    }

    [Test]
    public async Task UpdateDailyWeather_ReturnsZero_WhenNoProbabilitiesMatchDb()
    {
        // Override rules with probabilities for weather names that don't exist in the DB
        var rulesWithNoMatch = new GameRulesData(
            GameRulesService.CreateDefaults().Movement,
            GameRulesService.CreateDefaults().MovementRates,
            GameRulesService.CreateDefaults().Supply,
            GameRulesService.CreateDefaults().UnitStats,
            GameRulesService.CreateDefaults().News,
            new WeatherRules(6, new System.Collections.Generic.Dictionary<string, double>
            {
                ["Blizzard"] = 1.0  // doesn't exist in seeded DB
            }));
        _mockGameRules.Setup(s => s.Rules).Returns(rulesWithNoMatch);

        var gameDate = new DateTime(1805, 6, 15);
        int updated = await _weatherService.UpdateDailyWeatherAsync(gameDate);

        Assert.That(updated, Is.EqualTo(0), "No update when no DB weather types match probabilities");
    }

    [Test]
    public async Task UpdateDailyWeather_UsesDatePart_NotTime()
    {
        // Two calls on the same calendar day but different times → second should be skipped
        var morning = new DateTime(1805, 6, 15, 6, 0, 0);
        var evening = new DateTime(1805, 6, 15, 20, 0, 0);

        int morningUpdate = await _weatherService.UpdateDailyWeatherAsync(morning);
        int eveningUpdate = await _weatherService.UpdateDailyWeatherAsync(evening);

        Assert.That(morningUpdate, Is.GreaterThan(0));
        Assert.That(eveningUpdate, Is.EqualTo(0), "Same calendar date regardless of time should be skipped");
    }
}
