using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Discord;

namespace MechanicalCataphract.Tests.Discord;

[TestFixture]
public class DiscordBotServiceTests
{
    private SqliteConnection _connection = null!;
    private ServiceProvider _serviceProvider = null!;

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContext<WargameDbContext>(options =>
            options.UseSqlite(_connection));

        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _serviceProvider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Test]
    public void IsConnected_BeforeStart_ReturnsFalse()
    {
        var botService = new DiscordBotService(_serviceProvider);

        Assert.That(botService.IsConnected, Is.False);
        Assert.That(botService.Client, Is.Null);
    }

    [Test]
    public async Task StartBotAsync_NoConfigInDb_StaysDisconnected()
    {
        var botService = new DiscordBotService(_serviceProvider);

        await botService.StartBotAsync();

        Assert.That(botService.IsConnected, Is.False);
        Assert.That(botService.Client, Is.Null);
    }

    [Test]
    public async Task StartBotAsync_EmptyToken_StaysDisconnected()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
            db.DiscordConfigs.Add(new DiscordConfig
            {
                Id = 1,
                BotToken = "",
                GuildId = 123456789012345678UL
            });
            await db.SaveChangesAsync();
        }

        var botService = new DiscordBotService(_serviceProvider);

        await botService.StartBotAsync();

        Assert.That(botService.IsConnected, Is.False);
        Assert.That(botService.Client, Is.Null);
    }

    [Test]
    public async Task StartBotAsync_NullGuildId_StaysDisconnected()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
            db.DiscordConfigs.Add(new DiscordConfig
            {
                Id = 1,
                BotToken = "some-token",
                GuildId = null
            });
            await db.SaveChangesAsync();
        }

        var botService = new DiscordBotService(_serviceProvider);

        await botService.StartBotAsync();

        Assert.That(botService.IsConnected, Is.False);
        Assert.That(botService.Client, Is.Null);
    }

    [Test]
    public async Task StopBotAsync_WhenNotStarted_DoesNotThrow()
    {
        var botService = new DiscordBotService(_serviceProvider);

        Assert.DoesNotThrowAsync(async () => await botService.StopBotAsync());
    }
}
