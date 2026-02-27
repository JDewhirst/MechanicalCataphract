using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Tests.Services.Integration;

public abstract class IntegrationTestBase
{
    protected WargameDbContext Context { get; private set; } = null!;

    /// <summary>Real on-map hexes, excluding the off-board sentinel (Torment Hexagon).</summary>
    protected IQueryable<MapHex> GridHexes =>
        Context.MapHexes.Where(h => !(h.Q == MapHex.SentinelQ && h.R == MapHex.SentinelR));
    private SqliteConnection _connection = null!;

    [SetUp]
    public async Task BaseSetUp()
    {
        // Ensure entity computed properties (MovementRate, DailySupplyConsumption, etc.) don't throw
        GameRules.SetForTesting(GameRulesService.CreateDefaults());

        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<WargameDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new WargameDbContext(options);
        await Context.Database.EnsureCreatedAsync();
    }

    [TearDown]
    public async Task BaseTearDown()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
