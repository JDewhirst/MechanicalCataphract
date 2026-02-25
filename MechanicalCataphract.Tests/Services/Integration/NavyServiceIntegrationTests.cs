using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Tests.Services.Integration;

[TestFixture]
public class NavyServiceIntegrationTests : IntegrationTestBase
{
    private NavyService _service = null!;
    private ArmyService _armyService = null!;
    private int _hexQ;
    private int _hexR;

    [SetUp]
    public async Task SetUp()
    {
        await SeedHelpers.SeedMapAsync(Context, 3, 3);
        _service = new NavyService(Context);
        _armyService = new ArmyService(Context);
        var hex = Context.MapHexes.First();
        _hexQ = hex.Q;
        _hexR = hex.R;
    }

    // ──────────────────────────────────────────────
    // Basic CRUD
    // ──────────────────────────────────────────────

    [Test]
    public async Task CreateAndGetById_IncludesShips()
    {
        var navy = await _service.CreateAsync(new Navy
        {
            Name = "Home Fleet",
            CoordinateQ = _hexQ,
            CoordinateR = _hexR
        });

        await SeedHelpers.SeedShipAsync(Context, navy.Id, ShipType.Warship);

        var loaded = await _service.GetByIdAsync(navy.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Ships.Count, Is.EqualTo(1));
        Assert.That(loaded.Ships.First().ShipType, Is.EqualTo(ShipType.Warship));
    }

    [Test]
    public async Task GetAllAsync_ReturnsAll()
    {
        await _service.CreateAsync(new Navy { Name = "A", CoordinateQ = _hexQ, CoordinateR = _hexR });
        await _service.CreateAsync(new Navy { Name = "B", CoordinateQ = _hexQ, CoordinateR = _hexR });

        var all = await _service.GetAllAsync();
        Assert.That(all.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task UpdateAsync_PersistsChanges()
    {
        var navy = await _service.CreateAsync(new Navy { Name = "Old Name", CoordinateQ = _hexQ, CoordinateR = _hexR });
        navy.Name = "New Name";
        await _service.UpdateAsync(navy);

        var loaded = await _service.GetByIdAsync(navy.Id);
        Assert.That(loaded!.Name, Is.EqualTo("New Name"));
    }

    [Test]
    public async Task DeleteAsync_CascadesDeleteShips()
    {
        var navy = await _service.CreateAsync(new Navy { Name = "Doomed", CoordinateQ = _hexQ, CoordinateR = _hexR });
        await SeedHelpers.SeedShipAsync(Context, navy.Id);
        await SeedHelpers.SeedShipAsync(Context, navy.Id);

        await _service.DeleteAsync(navy.Id);

        Assert.That(await _service.GetByIdAsync(navy.Id), Is.Null);
        Assert.That(Context.Ships.Count(), Is.EqualTo(0));
    }

    // ──────────────────────────────────────────────
    // Queries
    // ──────────────────────────────────────────────

    [Test]
    public async Task GetNaviesAtHexAsync_FiltersByLocation()
    {
        var allHexes = Context.MapHexes.ToList();
        var hex2 = allHexes.First(h => h.Q != _hexQ || h.R != _hexR);

        await _service.CreateAsync(new Navy { Name = "Here", CoordinateQ = _hexQ, CoordinateR = _hexR });
        await _service.CreateAsync(new Navy { Name = "There", CoordinateQ = hex2.Q, CoordinateR = hex2.R });

        var result = await _service.GetNaviesAtHexAsync(new Hex(_hexQ, _hexR, -_hexQ - _hexR));
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("Here"));
    }

    [Test]
    public async Task GetNaviesByCommanderAsync_FiltersByCommander()
    {
        var cmd = await SeedHelpers.SeedCommanderAsync(Context, "Admiral", factionId: 1);
        await _service.CreateAsync(new Navy { Name = "Fleet A", CoordinateQ = _hexQ, CoordinateR = _hexR, CommanderId = cmd.Id });
        await _service.CreateAsync(new Navy { Name = "Fleet B", CoordinateQ = _hexQ, CoordinateR = _hexR });

        var result = await _service.GetNaviesByCommanderAsync(cmd.Id);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("Fleet A"));
    }

    [Test]
    public async Task GetNavyWithShipsAsync_IncludesCarriedArmy()
    {
        var navy = await _service.CreateAsync(new Navy { Name = "Carrier", CoordinateQ = _hexQ, CoordinateR = _hexR });
        var army = await _armyService.CreateAsync(new Army { Name = "Embarked", FactionId = 1, CoordinateQ = _hexQ, CoordinateR = _hexR });

        await _service.EmbarkArmyAsync(navy.Id, army.Id);

        var loaded = await _service.GetNavyWithShipsAsync(navy.Id);
        Assert.That(loaded!.CarriedArmy, Is.Not.Null);
        Assert.That(loaded.CarriedArmy!.Name, Is.EqualTo("Embarked"));
    }

    // ──────────────────────────────────────────────
    // Ship management
    // ──────────────────────────────────────────────

    [Test]
    public async Task AddShipAsync_AddsShipAndReturnsWithId()
    {
        var navy = await _service.CreateAsync(new Navy { Name = "Fleet", CoordinateQ = _hexQ, CoordinateR = _hexR });

        var ship = await _service.AddShipAsync(new Ship { NavyId = navy.Id, ShipType = ShipType.Transport, Count = 1 });

        Assert.That(ship.Id, Is.GreaterThan(0));
        Assert.That(Context.Ships.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task DeleteShipAsync_RemovesShip()
    {
        var navy = await _service.CreateAsync(new Navy { Name = "Fleet", CoordinateQ = _hexQ, CoordinateR = _hexR });
        var ship = await SeedHelpers.SeedShipAsync(Context, navy.Id, ShipType.Transport);

        await _service.DeleteShipAsync(ship.Id);

        Assert.That(Context.Ships.Count(), Is.EqualTo(0));
    }

    // ──────────────────────────────────────────────
    // Embarkation
    // ──────────────────────────────────────────────

    [Test]
    public async Task EmbarkArmyAsync_SetsArmyNavyId()
    {
        var navy = await _service.CreateAsync(new Navy { Name = "Fleet", CoordinateQ = _hexQ, CoordinateR = _hexR });
        var army = await _armyService.CreateAsync(new Army { Name = "Legion", FactionId = 1, CoordinateQ = _hexQ, CoordinateR = _hexR });

        await _service.EmbarkArmyAsync(navy.Id, army.Id);

        var loaded = await _armyService.GetByIdAsync(army.Id);
        Assert.That(loaded!.NavyId, Is.EqualTo(navy.Id));
        Assert.That(loaded.IsEmbarked, Is.True);
    }

    [Test]
    public async Task DisembarkArmyAsync_ClearsArmyNavyId()
    {
        var navy = await _service.CreateAsync(new Navy { Name = "Fleet", CoordinateQ = _hexQ, CoordinateR = _hexR });
        var army = await _armyService.CreateAsync(new Army { Name = "Legion", FactionId = 1, CoordinateQ = _hexQ, CoordinateR = _hexR });
        await _service.EmbarkArmyAsync(navy.Id, army.Id);

        await _service.DisembarkArmyAsync(army.Id);

        var loaded = await _armyService.GetByIdAsync(army.Id);
        Assert.That(loaded!.NavyId, Is.Null);
        Assert.That(loaded.IsEmbarked, Is.False);
    }

    [Test]
    public async Task DeleteNavyAsync_SetsArmyNavyIdNull()
    {
        // Army.NavyId FK is SetNull on Navy delete
        var navy = await _service.CreateAsync(new Navy { Name = "Fleet", CoordinateQ = _hexQ, CoordinateR = _hexR });
        var army = await _armyService.CreateAsync(new Army { Name = "Legion", FactionId = 1, CoordinateQ = _hexQ, CoordinateR = _hexR });
        await _service.EmbarkArmyAsync(navy.Id, army.Id);

        await _service.DeleteAsync(navy.Id);

        var reloaded = await _armyService.GetByIdAsync(army.Id);
        Assert.That(reloaded!.NavyId, Is.Null);
    }
}
