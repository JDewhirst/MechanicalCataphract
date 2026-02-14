using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Tests.Services.Integration;

[TestFixture]
public class CommanderCoordinateValidationTests : IntegrationTestBase
{
    private CommanderService _service = null!;
    private Faction _faction = null!;
    private int _hexQ;
    private int _hexR;

    [SetUp]
    public async Task SetUp()
    {
        await SeedHelpers.SeedMapAsync(Context, 3, 3);
        _faction = await SeedHelpers.SeedFactionAsync(Context);
        _service = new CommanderService(Context);
        var hex = Context.MapHexes.First();
        _hexQ = hex.Q;
        _hexR = hex.R;
    }

    [Test]
    public async Task CreateAsync_WithNullCoordinates_Succeeds()
    {
        var commander = await _service.CreateAsync(new Commander
        {
            Name = "Unplaced", FactionId = _faction.Id,
            CoordinateQ = null, CoordinateR = null
        });

        Assert.That(commander.Id, Is.GreaterThan(0));
    }

    [Test]
    public async Task CreateAsync_WithValidCoordinates_Succeeds()
    {
        var commander = await _service.CreateAsync(new Commander
        {
            Name = "Placed", FactionId = _faction.Id,
            CoordinateQ = _hexQ, CoordinateR = _hexR
        });

        Assert.That(commander.Id, Is.GreaterThan(0));
    }

    [Test]
    public void CreateAsync_WithOffMapCoordinates_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(new Commander
            {
                Name = "Bad", FactionId = _faction.Id,
                CoordinateQ = 999, CoordinateR = 999
            }));
    }

    [Test]
    public void CreateAsync_WithOneNullCoordinate_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(new Commander
            {
                Name = "Bad", FactionId = _faction.Id,
                CoordinateQ = _hexQ, CoordinateR = null
            }));
    }

    [Test]
    public void CreateAsync_WithOffMapTargetCoordinates_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(new Commander
            {
                Name = "Bad", FactionId = _faction.Id,
                CoordinateQ = _hexQ, CoordinateR = _hexR,
                TargetCoordinateQ = 999, TargetCoordinateR = 999
            }));
    }

    [Test]
    public async Task UpdateAsync_WithOffMapCoordinates_Throws()
    {
        var commander = await _service.CreateAsync(new Commander
        {
            Name = "Commander", FactionId = _faction.Id,
            CoordinateQ = _hexQ, CoordinateR = _hexR
        });

        commander.CoordinateQ = 999;
        commander.CoordinateR = 999;

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateAsync(commander));
    }
}
