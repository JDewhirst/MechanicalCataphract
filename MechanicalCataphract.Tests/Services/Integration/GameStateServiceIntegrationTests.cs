using MechanicalCataphract.Services;

namespace MechanicalCataphract.Tests.Services.Integration;

[TestFixture]
public class GameStateServiceIntegrationTests : IntegrationTestBase
{
    private GameStateService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new GameStateService(Context);
    }

    [Test]
    public async Task GetGameStateAsync_CreatesIfNotExists()
    {
        var gs = await _service.GetGameStateAsync();

        Assert.That(gs, Is.Not.Null);
        Assert.That(gs.Id, Is.EqualTo(1));
    }

    [Test]
    public async Task GetGameStateAsync_ReturnsSingleton()
    {
        var gs1 = await _service.GetGameStateAsync();
        var gs2 = await _service.GetGameStateAsync();

        Assert.That(gs2.Id, Is.EqualTo(gs1.Id));
    }

    [Test]
    public async Task GetCurrentWorldHourAsync_ReturnsZeroByDefault()
    {
        var hour = await _service.GetCurrentWorldHourAsync();

        Assert.That(hour, Is.EqualTo(0));
    }

    [Test]
    public async Task AdvanceWorldHourAsync_AddsHours()
    {
        var before = await _service.GetCurrentWorldHourAsync();

        await _service.AdvanceWorldHourAsync(6);

        var after = await _service.GetCurrentWorldHourAsync();
        Assert.That(after, Is.EqualTo(before + 6));
    }

    [Test]
    public async Task SetCurrentWorldHourAsync_SetsAbsoluteHour()
    {
        long target = 720; // 30 days * 24 hours

        await _service.SetCurrentWorldHourAsync(target);

        var result = await _service.GetCurrentWorldHourAsync();
        Assert.That(result, Is.EqualTo(target));
    }
}
