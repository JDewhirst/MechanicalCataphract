using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Tests.Services.Integration;

[TestFixture]
public class ArmyCoordinateValidationTests : IntegrationTestBase
{
    private ArmyService _service = null!;
    private Faction _faction = null!;
    private int _hexQ;
    private int _hexR;

    [SetUp]
    public async Task SetUp()
    {
        await SeedHelpers.SeedMapAsync(Context, 3, 3);
        _faction = await SeedHelpers.SeedFactionAsync(Context);
        _service = new ArmyService(Context);
        var hex = Context.MapHexes.First();
        _hexQ = hex.Q;
        _hexR = hex.R;
    }

    [Test]
    public async Task CreateAsync_WithNullCoordinates_Succeeds()
    {
        var army = await _service.CreateAsync(new Army
        {
            Name = "Unplaced", FactionId = _faction.Id,
            CoordinateQ = null, CoordinateR = null
        });

        Assert.That(army.Id, Is.GreaterThan(0));
    }

    [Test]
    public async Task CreateAsync_WithValidCoordinates_Succeeds()
    {
        var army = await _service.CreateAsync(new Army
        {
            Name = "Placed", FactionId = _faction.Id,
            CoordinateQ = _hexQ, CoordinateR = _hexR
        });

        Assert.That(army.Id, Is.GreaterThan(0));
    }

    [Test]
    public void CreateAsync_WithOffMapCoordinates_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(new Army
            {
                Name = "Bad", FactionId = _faction.Id,
                CoordinateQ = 999, CoordinateR = 999
            }));
    }

    [Test]
    public void CreateAsync_WithOneNullCoordinate_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(new Army
            {
                Name = "Bad", FactionId = _faction.Id,
                CoordinateQ = _hexQ, CoordinateR = null
            }));
    }

    [Test]
    public void CreateAsync_WithOffMapTargetCoordinates_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(new Army
            {
                Name = "Bad", FactionId = _faction.Id,
                CoordinateQ = _hexQ, CoordinateR = _hexR,
                TargetCoordinateQ = 999, TargetCoordinateR = 999
            }));
    }

    [Test]
    public async Task UpdateAsync_WithOffMapCoordinates_Throws()
    {
        var army = await _service.CreateAsync(new Army
        {
            Name = "Army", FactionId = _faction.Id,
            CoordinateQ = _hexQ, CoordinateR = _hexR
        });

        army.CoordinateQ = 999;
        army.CoordinateR = 999;

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateAsync(army));
    }

    [Test]
    public async Task MoveArmyAsync_ToOffMapHex_Throws()
    {
        var army = await _service.CreateAsync(new Army
        {
            Name = "Army", FactionId = _faction.Id,
            CoordinateQ = _hexQ, CoordinateR = _hexR
        });

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.MoveArmyAsync(army.Id, new Hex(999, 999, -1998)));
    }

    [Test]
    public async Task MoveArmyAsync_ToValidHex_Succeeds()
    {
        var army = await _service.CreateAsync(new Army
        {
            Name = "Army", FactionId = _faction.Id,
            CoordinateQ = _hexQ, CoordinateR = _hexR
        });
        var dest = Context.MapHexes.First(h => h.Q != _hexQ || h.R != _hexR);

        await _service.MoveArmyAsync(army.Id, new Hex(dest.Q, dest.R, -dest.Q - dest.R));

        var loaded = await _service.GetByIdAsync(army.Id);
        Assert.That(loaded!.CoordinateQ, Is.EqualTo(dest.Q));
    }
}
