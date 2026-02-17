using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Tests.Services.Integration;

[TestFixture]
public class ArmyServiceIntegrationTests : IntegrationTestBase
{
    private ArmyService _service = null!;
    private int _hexQ;
    private int _hexR;

    [SetUp]
    public async Task SetUp()
    {
        await SeedHelpers.SeedMapAsync(Context, 3, 3);
        _service = new ArmyService(Context);
        var hex = Context.MapHexes.First();
        _hexQ = hex.Q;
        _hexR = hex.R;
    }

    [Test]
    public async Task CreateAndGetById_IncludesFaction()
    {
        var army = await _service.CreateAsync(new Army { Name = "1st Army", FactionId = 1, CoordinateQ = _hexQ, CoordinateR = _hexR });

        var loaded = await _service.GetByIdAsync(army.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Faction, Is.Not.Null);
    }

    [Test]
    public async Task GetAllAsync_ReturnsAll()
    {
        await _service.CreateAsync(new Army { Name = "A", FactionId = 1, CoordinateQ = _hexQ, CoordinateR = _hexR });
        await _service.CreateAsync(new Army { Name = "B", FactionId = 1, CoordinateQ = _hexQ, CoordinateR = _hexR });

        var all = await _service.GetAllAsync();
        Assert.That(all.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task UpdateAsync_PersistsChanges()
    {
        var army = await _service.CreateAsync(new Army { Name = "Old", FactionId = 1, CoordinateQ = _hexQ, CoordinateR = _hexR });
        army.Name = "New";
        await _service.UpdateAsync(army);

        var loaded = await _service.GetByIdAsync(army.Id);
        Assert.That(loaded!.Name, Is.EqualTo("New"));
    }

    [Test]
    public async Task DeleteAsync_CascadesDeleteBrigades()
    {
        var army = await _service.CreateAsync(new Army { Name = "Doomed", FactionId = 1, CoordinateQ = _hexQ, CoordinateR = _hexR });
        await SeedHelpers.SeedBrigadeAsync(Context, army.Id, "1st", 100);
        await SeedHelpers.SeedBrigadeAsync(Context, army.Id, "2nd", 200);

        await _service.DeleteAsync(army.Id);

        Assert.That(await _service.GetByIdAsync(army.Id), Is.Null);
        Assert.That(Context.Brigades.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task GetArmiesAtHexAsync_FiltersByLocation()
    {
        var allHexes = Context.MapHexes.ToList();
        var hex2 = allHexes.First(h => h.Q != _hexQ || h.R != _hexR);
        await _service.CreateAsync(new Army { Name = "Here", FactionId = 1, CoordinateQ = _hexQ, CoordinateR = _hexR });
        await _service.CreateAsync(new Army { Name = "There", FactionId = 1, CoordinateQ = hex2.Q, CoordinateR = hex2.R });

        var atHex1 = await _service.GetArmiesAtHexAsync(new Hex(_hexQ, _hexR, -_hexQ - _hexR));
        Assert.That(atHex1.Count, Is.EqualTo(1));
        Assert.That(atHex1[0].Name, Is.EqualTo("Here"));
    }

    [Test]
    public async Task GetArmiesByFactionAsync_FiltersByFaction()
    {
        var faction2 = await SeedHelpers.SeedFactionAsync(Context, "France");
        await _service.CreateAsync(new Army { Name = "A1", FactionId = 1, CoordinateQ = _hexQ, CoordinateR = _hexR });
        await _service.CreateAsync(new Army { Name = "A2", FactionId = faction2.Id, CoordinateQ = _hexQ, CoordinateR = _hexR });

        var result = await _service.GetArmiesByFactionAsync(1);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("A1"));
    }

    [Test]
    public async Task GetArmyWithBrigadesAsync_IncludesBrigades()
    {
        var army = await _service.CreateAsync(new Army { Name = "Army", FactionId = 1, CoordinateQ = _hexQ, CoordinateR = _hexR });
        await SeedHelpers.SeedBrigadeAsync(Context, army.Id, "1st", 100);
        await SeedHelpers.SeedBrigadeAsync(Context, army.Id, "2nd", 200);

        var loaded = await _service.GetArmyWithBrigadesAsync(army.Id);

        Assert.That(loaded!.Brigades.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task TransferBrigadeAsync_MovesBrigade()
    {
        var faction2 = await SeedHelpers.SeedFactionAsync(Context, "France");
        var army1 = await _service.CreateAsync(new Army { Name = "Army1", FactionId = 1, CoordinateQ = _hexQ, CoordinateR = _hexR });
        var army2 = await _service.CreateAsync(new Army { Name = "Army2", FactionId = faction2.Id, CoordinateQ = _hexQ, CoordinateR = _hexR });
        var brigade = await SeedHelpers.SeedBrigadeAsync(Context, army1.Id, "Transfer", 100);

        await _service.TransferBrigadeAsync(brigade.Id, army2.Id);

        var reloaded = await Context.Brigades.FindAsync(brigade.Id);
        Assert.That(reloaded!.ArmyId, Is.EqualTo(army2.Id));
        Assert.That(reloaded.FactionId, Is.EqualTo(faction2.Id));
    }

    [Test]
    public async Task MoveArmyAsync_UpdatesLocation()
    {
        var army = await _service.CreateAsync(new Army { Name = "Moving", FactionId = 1, CoordinateQ = _hexQ, CoordinateR = _hexR });
        var allHexes = Context.MapHexes.ToList();
        var dest = allHexes.First(h => h.Q != _hexQ || h.R != _hexR);

        await _service.MoveArmyAsync(army.Id, new Hex(dest.Q, dest.R, -dest.Q - dest.R));

        var loaded = await _service.GetByIdAsync(army.Id);
        Assert.That(loaded!.CoordinateQ, Is.EqualTo(dest.Q));
        Assert.That(loaded.CoordinateR, Is.EqualTo(dest.R));
    }

    [Test]
    public async Task CalculateTotalTroopsAsync_Sums()
    {
        var army = await _service.CreateAsync(new Army { Name = "Army", FactionId = 1, CoordinateQ = _hexQ, CoordinateR = _hexR });
        await SeedHelpers.SeedBrigadeAsync(Context, army.Id, "1st", 100);
        await SeedHelpers.SeedBrigadeAsync(Context, army.Id, "2nd", 200);
        await SeedHelpers.SeedBrigadeAsync(Context, army.Id, "3rd", 300);

        var total = await _service.CalculateTotalTroopsAsync(army.Id);
        Assert.That(total, Is.EqualTo(600));
    }

    [Test]
    public async Task GetMaxScoutingRangeAsync_ReturnsMax()
    {
        var army = await _service.CreateAsync(new Army { Name = "Army", FactionId = 1, CoordinateQ = _hexQ, CoordinateR = _hexR });
        await SeedHelpers.SeedBrigadeAsync(Context, army.Id, "1st", 100, unitType: UnitType.Infantry);
        await SeedHelpers.SeedBrigadeAsync(Context, army.Id, "2nd", 100, unitType: UnitType.Cavalry);
        await SeedHelpers.SeedBrigadeAsync(Context, army.Id, "3rd", 100, unitType: UnitType.Skirmishers);

        var max = await _service.GetMaxScoutingRangeAsync(army.Id);
        Assert.That(max, Is.EqualTo(2));
    }

    [Test]
    public async Task GetDailySupplyConsumption_Calculates()
    {
        var army = await _service.CreateAsync(new Army
        {
            Name = "Army",
            FactionId = 1,
            CoordinateQ = _hexQ,
            CoordinateR = _hexR,
            NonCombatants = 50,
            Wagons = 5
        });
        // Infantry: 100 * 1 = 100
        await SeedHelpers.SeedBrigadeAsync(Context, army.Id, "Inf", 100, UnitType.Infantry);
        // Cavalry: 50 * 10 = 500
        await SeedHelpers.SeedBrigadeAsync(Context, army.Id, "Cav", 50, UnitType.Cavalry);
        // Skirmishers: 30 * 1 = 30
        await SeedHelpers.SeedBrigadeAsync(Context, army.Id, "Skrm", 30, UnitType.Skirmishers);

        // Total = 100 + 500 + 30 + (50 noncombatants * 1) + (5 wagons * 10) = 730
        var supply = await _service.GetDailySupplyConsumptionAsync(army.Id);
        Assert.That(supply, Is.EqualTo(730));
    }
}
