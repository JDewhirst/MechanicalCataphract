using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Tests.Services.Integration;

[TestFixture]
public class MessageServiceIntegrationTests : IntegrationTestBase
{
    private MessageService _service = null!;
    private Commander _sender = null!;
    private Commander _target = null!;

    [SetUp]
    public async Task SetUp()
    {
        await SeedHelpers.SeedMapAsync(Context, 3, 3);
        _service = new MessageService(Context);
        var hex = Context.MapHexes.First();
        _sender = await SeedHelpers.SeedCommanderAsync(Context, "Sender", 1, hex.Q, hex.R);
        _target = await SeedHelpers.SeedCommanderAsync(Context, "Target", 1, hex.Q, hex.R);
    }

    [Test]
    public async Task CreateAsync_SetsCreatedAt()
    {
        var before = DateTime.UtcNow;

        var msg = await _service.CreateAsync(new Message
        {
            SenderCommanderId = _sender.Id,
            TargetCommanderId = _target.Id,
            Content = "Hello",
            CoordinateQ = _sender.CoordinateQ,
            CoordinateR = _sender.CoordinateR
        });

        Assert.That(msg.CreatedAt, Is.GreaterThanOrEqualTo(before));
    }

    [Test]
    public async Task GetByIdAsync_IncludesSenderAndTarget()
    {
        var msg = await _service.CreateAsync(new Message
        {
            SenderCommanderId = _sender.Id,
            TargetCommanderId = _target.Id,
            Content = "Orders",
            CoordinateQ = _sender.CoordinateQ,
            CoordinateR = _sender.CoordinateR
        });

        var loaded = await _service.GetByIdAsync(msg.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.SenderCommander, Is.Not.Null);
        Assert.That(loaded.TargetCommander, Is.Not.Null);
        Assert.That(loaded.SenderCommander!.Name, Is.EqualTo("Sender"));
    }

    [Test]
    public async Task GetMessagesBySenderAsync_Filters()
    {
        await _service.CreateAsync(new Message { SenderCommanderId = _sender.Id, TargetCommanderId = _target.Id, Content = "A", CoordinateQ = _sender.CoordinateQ, CoordinateR = _sender.CoordinateR });
        await _service.CreateAsync(new Message { SenderCommanderId = _sender.Id, TargetCommanderId = _target.Id, Content = "B", CoordinateQ = _sender.CoordinateQ, CoordinateR = _sender.CoordinateR });
        await _service.CreateAsync(new Message { SenderCommanderId = _target.Id, TargetCommanderId = _sender.Id, Content = "C", CoordinateQ = _target.CoordinateQ, CoordinateR = _target.CoordinateR });

        var messages = await _service.GetMessagesBySenderAsync(_sender.Id);

        Assert.That(messages.Count, Is.EqualTo(2));
        Assert.That(messages.All(m => m.SenderCommanderId == _sender.Id), Is.True);
    }

    [Test]
    public async Task GetUndeliveredMessagesAsync_FiltersAndOrders()
    {
        var m1 = await _service.CreateAsync(new Message { SenderCommanderId = _sender.Id, TargetCommanderId = _target.Id, Content = "First", CoordinateQ = _sender.CoordinateQ, CoordinateR = _sender.CoordinateR });
        var m2 = await _service.CreateAsync(new Message { SenderCommanderId = _sender.Id, TargetCommanderId = _target.Id, Content = "Second", CoordinateQ = _sender.CoordinateQ, CoordinateR = _sender.CoordinateR });
        await _service.MarkAsDeliveredAsync(m1.Id);

        var undelivered = await _service.GetUndeliveredMessagesAsync();

        Assert.That(undelivered.Count, Is.EqualTo(1));
        Assert.That(undelivered[0].Content, Is.EqualTo("Second"));
    }

    [Test]
    public async Task MarkAsDeliveredAsync_SetsFields()
    {
        var msg = await _service.CreateAsync(new Message
        {
            SenderCommanderId = _sender.Id,
            TargetCommanderId = _target.Id,
            Content = "Urgent",
            CoordinateQ = _sender.CoordinateQ,
            CoordinateR = _sender.CoordinateR
        });

        await _service.MarkAsDeliveredAsync(msg.Id);

        var loaded = await _service.GetByIdAsync(msg.Id);
        Assert.That(loaded!.Delivered, Is.True);
        Assert.That(loaded.DeliveredAt, Is.Not.Null);
    }

    [Test]
    public async Task DeleteAsync_Removes()
    {
        var msg = await _service.CreateAsync(new Message
        {
            SenderCommanderId = _sender.Id,
            TargetCommanderId = _target.Id,
            Content = "Delete me",
            CoordinateQ = _sender.CoordinateQ,
            CoordinateR = _sender.CoordinateR
        });

        await _service.DeleteAsync(msg.Id);

        var loaded = await _service.GetByIdAsync(msg.Id);
        Assert.That(loaded, Is.Null);
    }
}
