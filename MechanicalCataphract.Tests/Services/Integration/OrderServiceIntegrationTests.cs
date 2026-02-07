using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Tests.Services.Integration;

[TestFixture]
public class OrderServiceIntegrationTests : IntegrationTestBase
{
    private OrderService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new OrderService(Context);
    }

    [Test]
    public async Task CreateAsync_SetsCreatedAt()
    {
        var commander = await SeedHelpers.SeedCommanderAsync(Context, "Cmd", 1);
        var before = DateTime.UtcNow;

        var order = await _service.CreateAsync(new Order
        {
            CommanderId = commander.Id,
            Contents = "March north"
        });

        Assert.That(order.CreatedAt, Is.GreaterThanOrEqualTo(before));
    }

    [Test]
    public async Task GetByIdAsync_IncludesCommander()
    {
        var commander = await SeedHelpers.SeedCommanderAsync(Context, "Wellington", 1);
        var order = await _service.CreateAsync(new Order
        {
            CommanderId = commander.Id,
            Contents = "Hold the line"
        });

        var loaded = await _service.GetByIdAsync(order.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Commander, Is.Not.Null);
        Assert.That(loaded.Commander!.Name, Is.EqualTo("Wellington"));
    }

    [Test]
    public async Task GetOrdersByCommanderAsync_Filters()
    {
        var cmd1 = await SeedHelpers.SeedCommanderAsync(Context, "Cmd1", 1);
        var cmd2 = await SeedHelpers.SeedCommanderAsync(Context, "Cmd2", 1);
        await _service.CreateAsync(new Order { CommanderId = cmd1.Id, Contents = "A" });
        await _service.CreateAsync(new Order { CommanderId = cmd1.Id, Contents = "B" });
        await _service.CreateAsync(new Order { CommanderId = cmd2.Id, Contents = "C" });

        var orders = await _service.GetOrdersByCommanderAsync(cmd1.Id);

        Assert.That(orders.Count, Is.EqualTo(2));
        Assert.That(orders.All(o => o.CommanderId == cmd1.Id), Is.True);
    }

    [Test]
    public async Task GetUnprocessedOrdersAsync_FiltersAndOrders()
    {
        var cmd = await SeedHelpers.SeedCommanderAsync(Context, "Cmd", 1);
        var o1 = await _service.CreateAsync(new Order { CommanderId = cmd.Id, Contents = "First" });
        var o2 = await _service.CreateAsync(new Order { CommanderId = cmd.Id, Contents = "Second" });
        await _service.MarkAsProcessedAsync(o1.Id);

        var unprocessed = await _service.GetUnprocessedOrdersAsync();

        Assert.That(unprocessed.Count, Is.EqualTo(1));
        Assert.That(unprocessed[0].Contents, Is.EqualTo("Second"));
    }

    [Test]
    public async Task MarkAsProcessedAsync_SetsFields()
    {
        var cmd = await SeedHelpers.SeedCommanderAsync(Context, "Cmd", 1);
        var order = await _service.CreateAsync(new Order { CommanderId = cmd.Id, Contents = "Go" });

        await _service.MarkAsProcessedAsync(order.Id);

        var loaded = await _service.GetByIdAsync(order.Id);
        Assert.That(loaded!.Processed, Is.True);
        Assert.That(loaded.ProcessedAt, Is.Not.Null);
    }
}
