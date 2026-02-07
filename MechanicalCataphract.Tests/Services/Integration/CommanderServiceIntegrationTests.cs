using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Tests.Services.Integration;

[TestFixture]
public class CommanderServiceIntegrationTests : IntegrationTestBase
{
    private CommanderService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new CommanderService(Context);
    }

    [Test]
    public async Task CreateAndGetById_IncludesFaction()
    {
        var commander = await _service.CreateAsync(new Commander { Name = "Napoleon", FactionId = 1 });

        var loaded = await _service.GetByIdAsync(commander.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Name, Is.EqualTo("Napoleon"));
        Assert.That(loaded.Faction, Is.Not.Null);
    }

    [Test]
    public async Task GetAllAsync_ReturnsAll()
    {
        await _service.CreateAsync(new Commander { Name = "A", FactionId = 1 });
        await _service.CreateAsync(new Commander { Name = "B", FactionId = 1 });

        var all = await _service.GetAllAsync();
        Assert.That(all.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task DeleteAsync_Removes()
    {
        var commander = await _service.CreateAsync(new Commander { Name = "Doomed", FactionId = 1 });

        await _service.DeleteAsync(commander.Id);

        var loaded = await _service.GetByIdAsync(commander.Id);
        Assert.That(loaded, Is.Null);
    }

    [Test]
    public async Task GetByDiscordIdAsync_FindsAndMisses()
    {
        await _service.CreateAsync(new Commander { Name = "Discord", FactionId = 1, DiscordUserId = 12345 });

        var found = await _service.GetByDiscordIdAsync(12345);
        var miss = await _service.GetByDiscordIdAsync(99999);

        Assert.That(found, Is.Not.Null);
        Assert.That(found!.Name, Is.EqualTo("Discord"));
        Assert.That(miss, Is.Null);
    }

    [Test]
    public async Task GetCommandersByFactionAsync_Filters()
    {
        var faction2 = await SeedHelpers.SeedFactionAsync(Context, "France");
        await _service.CreateAsync(new Commander { Name = "Cmd1", FactionId = 1 });
        await _service.CreateAsync(new Commander { Name = "Cmd2", FactionId = faction2.Id });
        await _service.CreateAsync(new Commander { Name = "Cmd3", FactionId = 1 });

        var result = await _service.GetCommandersByFactionAsync(1);

        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result.All(c => c.FactionId == 1), Is.True);
    }

    [Test]
    public async Task GetCommanderWithArmiesAsync_IncludesAll()
    {
        await SeedHelpers.SeedMapAsync(Context, 3, 3);
        var hex = Context.MapHexes.First();
        var commander = await _service.CreateAsync(new Commander { Name = "General", FactionId = 1, LocationQ = hex.Q, LocationR = hex.R });
        var army = await SeedHelpers.SeedArmyAsync(Context, "Army1", 1, hex.Q, hex.R, commander.Id);
        await SeedHelpers.SeedBrigadeAsync(Context, army.Id, "1st Infantry", 500);

        var loaded = await _service.GetCommanderWithArmiesAsync(commander.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.CommandedArmies.Count, Is.EqualTo(1));
        Assert.That(loaded.CommandedArmies.First().Brigades.Count, Is.EqualTo(1));
    }
}
