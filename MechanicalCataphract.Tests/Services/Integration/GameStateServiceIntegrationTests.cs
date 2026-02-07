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
    public async Task GetCurrentGameTimeAsync_ReturnsTime()
    {
        var time = await _service.GetCurrentGameTimeAsync();

        Assert.That(time, Is.Not.EqualTo(default(DateTime)));
    }

    [Test]
    public async Task AdvanceGameTimeAsync_AddsTimeSpan()
    {
        var before = await _service.GetCurrentGameTimeAsync();

        await _service.AdvanceGameTimeAsync(TimeSpan.FromHours(6));

        var after = await _service.GetCurrentGameTimeAsync();
        Assert.That(after, Is.EqualTo(before.AddHours(6)));
    }

    [Test]
    public async Task SetGameTimeAsync_SetsAbsoluteTime()
    {
        var target = new DateTime(1805, 12, 2, 8, 0, 0);

        await _service.SetGameTimeAsync(target);

        var result = await _service.GetCurrentGameTimeAsync();
        Assert.That(result, Is.EqualTo(target));
    }
}
