using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Tests.Services.Integration;

[TestFixture]
public class DiscordEntityIntegrationTests : IntegrationTestBase
{
    // --- DiscordConfig ---

    [Test]
    public async Task DiscordConfig_CreateAndRead_PersistsAllFields()
    {
        var config = new DiscordConfig
        {
            Id = 1,
            BotToken = "test-token-abc123",
            GuildId = 123456789012345678UL,
            AdminRoleId = 987654321098765432UL
        };
        Context.DiscordConfigs.Add(config);
        await Context.SaveChangesAsync();

        var loaded = await Context.DiscordConfigs.FindAsync(1);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.BotToken, Is.EqualTo("test-token-abc123"));
        Assert.That(loaded.GuildId, Is.EqualTo(123456789012345678UL));
        Assert.That(loaded.AdminRoleId, Is.EqualTo(987654321098765432UL));
    }

    [Test]
    public async Task DiscordConfig_NullableFields_PersistAsNull()
    {
        var config = new DiscordConfig { Id = 1 };
        Context.DiscordConfigs.Add(config);
        await Context.SaveChangesAsync();

        var loaded = await Context.DiscordConfigs.FindAsync(1);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.BotToken, Is.Null);
        Assert.That(loaded.GuildId, Is.Null);
        Assert.That(loaded.AdminRoleId, Is.Null);
    }

    [Test]
    public async Task DiscordConfig_Update_PersistsChanges()
    {
        var config = new DiscordConfig { Id = 1 };
        Context.DiscordConfigs.Add(config);
        await Context.SaveChangesAsync();

        config.BotToken = "updated-token";
        config.GuildId = 111111111111111111UL;
        await Context.SaveChangesAsync();

        var loaded = await Context.DiscordConfigs.FindAsync(1);

        Assert.That(loaded!.BotToken, Is.EqualTo("updated-token"));
        Assert.That(loaded.GuildId, Is.EqualTo(111111111111111111UL));
    }

    // --- WeatherUpdateRecord ---

    [Test]
    public async Task WeatherUpdateRecord_CreateAndRead_PersistsDate()
    {
        var record = new WeatherUpdateRecord
        {
            UpdateDate = new DateTime(1805, 12, 2) // Austerlitz
        };
        Context.WeatherUpdateRecords.Add(record);
        await Context.SaveChangesAsync();

        var loaded = await Context.WeatherUpdateRecords.FindAsync(record.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.UpdateDate, Is.EqualTo(new DateTime(1805, 12, 2)));
    }

    [Test]
    public async Task WeatherUpdateRecord_QueryMostRecent_ReturnsLatestDate()
    {
        Context.WeatherUpdateRecords.Add(new WeatherUpdateRecord { UpdateDate = new DateTime(1805, 1, 1) });
        Context.WeatherUpdateRecords.Add(new WeatherUpdateRecord { UpdateDate = new DateTime(1805, 6, 15) });
        Context.WeatherUpdateRecords.Add(new WeatherUpdateRecord { UpdateDate = new DateTime(1805, 3, 10) });
        await Context.SaveChangesAsync();

        // This is the query pattern WeatherUpdateService will use
        var mostRecent = Context.WeatherUpdateRecords
            .OrderByDescending(r => r.UpdateDate)
            .FirstOrDefault();

        Assert.That(mostRecent, Is.Not.Null);
        Assert.That(mostRecent!.UpdateDate, Is.EqualTo(new DateTime(1805, 6, 15)));
    }

    [Test]
    public async Task WeatherUpdateRecord_EmptyTable_QueryReturnsNull()
    {
        var mostRecent = Context.WeatherUpdateRecords
            .OrderByDescending(r => r.UpdateDate)
            .FirstOrDefault();

        Assert.That(mostRecent, Is.Null);
    }

    // --- Faction Discord Fields ---

    [Test]
    public async Task Faction_DiscordFields_PersistWhenSet()
    {
        var faction = new Faction
        {
            Name = "Rome",
            ColorHex = "#8B0000",
            DiscordRoleId = 111111111111111111UL,
            DiscordCategoryId = 222222222222222222UL,
            DiscordChannelId = 333333333333333333UL
        };
        Context.Factions.Add(faction);
        await Context.SaveChangesAsync();

        var loaded = await Context.Factions.FindAsync(faction.Id);

        Assert.That(loaded!.DiscordRoleId, Is.EqualTo(111111111111111111UL));
        Assert.That(loaded.DiscordCategoryId, Is.EqualTo(222222222222222222UL));
        Assert.That(loaded.DiscordChannelId, Is.EqualTo(333333333333333333UL));
    }

    [Test]
    public async Task Faction_DiscordFields_DefaultToNull()
    {
        var faction = new Faction { Name = "Carthage", ColorHex = "#FFD700" };
        Context.Factions.Add(faction);
        await Context.SaveChangesAsync();

        var loaded = await Context.Factions.FindAsync(faction.Id);

        Assert.That(loaded!.DiscordRoleId, Is.Null);
        Assert.That(loaded.DiscordCategoryId, Is.Null);
        Assert.That(loaded.DiscordChannelId, Is.Null);
    }

    [Test]
    public async Task Faction_DiscordFields_UpdatePersists()
    {
        var faction = new Faction { Name = "Gaul", ColorHex = "#228B22" };
        Context.Factions.Add(faction);
        await Context.SaveChangesAsync();

        faction.DiscordRoleId = 444444444444444444UL;
        faction.DiscordCategoryId = 555555555555555555UL;
        faction.DiscordChannelId = 666666666666666666UL;
        await Context.SaveChangesAsync();

        // Detach and reload to verify persistence
        Context.ChangeTracker.Clear();
        var loaded = await Context.Factions.FindAsync(faction.Id);

        Assert.That(loaded!.DiscordRoleId, Is.EqualTo(444444444444444444UL));
        Assert.That(loaded.DiscordCategoryId, Is.EqualTo(555555555555555555UL));
        Assert.That(loaded.DiscordChannelId, Is.EqualTo(666666666666666666UL));
    }

    // --- Commander DiscordChannelId ---

    [Test]
    public async Task Commander_DiscordChannelId_PersistsWhenSet()
    {
        var commander = new Commander
        {
            Name = "Caesar",
            FactionId = 1,
            DiscordUserId = 777777777777777777UL,
            DiscordChannelId = 888888888888888888UL
        };
        Context.Commanders.Add(commander);
        await Context.SaveChangesAsync();

        var loaded = await Context.Commanders.FindAsync(commander.Id);

        Assert.That(loaded!.DiscordChannelId, Is.EqualTo(888888888888888888UL));
        Assert.That(loaded.DiscordUserId, Is.EqualTo(777777777777777777UL));
    }

    [Test]
    public async Task Commander_DiscordChannelId_DefaultsToNull()
    {
        var commander = new Commander { Name = "Pompey", FactionId = 1 };
        Context.Commanders.Add(commander);
        await Context.SaveChangesAsync();

        var loaded = await Context.Commanders.FindAsync(commander.Id);

        Assert.That(loaded!.DiscordChannelId, Is.Null);
    }
}
