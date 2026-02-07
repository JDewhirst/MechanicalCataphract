using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Tests.Services.Integration;

[TestFixture]
public class FactionServiceIntegrationTests : IntegrationTestBase
{
    private FactionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new FactionService(Context);
    }

    [Test]
    public async Task GetByIdAsync_ReturnsSeededFaction()
    {
        var faction = await _service.GetByIdAsync(1);

        Assert.That(faction, Is.Not.Null);
        Assert.That(faction!.Name, Is.EqualTo("No Faction"));
    }

    [Test]
    public async Task CreateAndGetAll_IncludesNewFaction()
    {
        await _service.CreateAsync(new Faction { Name = "France", ColorHex = "#0000FF" });

        var all = await _service.GetAllAsync();

        Assert.That(all.Count, Is.EqualTo(2)); // seeded + new
        Assert.That(all.Any(f => f.Name == "France"), Is.True);
    }

    [Test]
    public async Task UpdateAsync_PersistsNameChange()
    {
        var faction = await _service.CreateAsync(new Faction { Name = "Old", ColorHex = "#000000" });
        faction.Name = "New";
        await _service.UpdateAsync(faction);

        var reloaded = await _service.GetByIdAsync(faction.Id);
        Assert.That(reloaded!.Name, Is.EqualTo("New"));
    }

    [Test]
    public async Task GetFactionWithArmiesAndCommandersAsync_LoadsNavProps()
    {
        await SeedHelpers.SeedMapAsync(Context, 3, 3);
        var faction = await SeedHelpers.SeedFactionAsync(Context, "Empire");
        var hex = Context.MapHexes.First();
        await SeedHelpers.SeedCommanderAsync(Context, "Napoleon", faction.Id, hex.Q, hex.R);
        await SeedHelpers.SeedArmyAsync(Context, "Grande Armee", faction.Id, hex.Q, hex.R);

        var loaded = await _service.GetFactionWithArmiesAndCommandersAsync(faction.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Armies.Count, Is.EqualTo(1));
        Assert.That(loaded.Commanders.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task GetFactionByNameAsync_FindsAndMisses()
    {
        var found = await _service.GetFactionByNameAsync("No Faction");
        var miss = await _service.GetFactionByNameAsync("Nonexistent");

        Assert.That(found, Is.Not.Null);
        Assert.That(miss, Is.Null);
    }
}
