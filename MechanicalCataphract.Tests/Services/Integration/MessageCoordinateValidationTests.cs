using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Tests.Services.Integration;

[TestFixture]
public class MessageCoordinateValidationTests : IntegrationTestBase
{
    private MessageService _service = null!;
    private Faction _faction = null!;
    private Commander _commander = null!;
    private int _hexQ;
    private int _hexR;

    [SetUp]
    public async Task SetUp()
    {
        await SeedHelpers.SeedMapAsync(Context, 3, 3);
        _faction = await SeedHelpers.SeedFactionAsync(Context);
        _commander = await SeedHelpers.SeedCommanderAsync(Context, "Sender", _faction.Id);
        _service = new MessageService(Context);
        var hex = Context.MapHexes.First();
        _hexQ = hex.Q;
        _hexR = hex.R;
    }

    [Test]
    public async Task CreateAsync_WithNullCoordinates_Succeeds()
    {
        var message = await _service.CreateAsync(new Message
        {
            Content = "Hello",
            SenderCommanderId = _commander.Id
        });

        Assert.That(message.Id, Is.GreaterThan(0));
    }

    [Test]
    public async Task CreateAsync_WithValidCoordinates_Succeeds()
    {
        var message = await _service.CreateAsync(new Message
        {
            Content = "Hello",
            SenderCommanderId = _commander.Id,
            CoordinateQ = _hexQ, CoordinateR = _hexR,
            SenderCoordinateQ = _hexQ, SenderCoordinateR = _hexR,
            TargetCoordinateQ = _hexQ, TargetCoordinateR = _hexR
        });

        Assert.That(message.Id, Is.GreaterThan(0));
    }

    [Test]
    public void CreateAsync_WithOffMapLocation_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(new Message
            {
                Content = "Hello",
                SenderCommanderId = _commander.Id,
                CoordinateQ = 999, CoordinateR = 999
            }));
    }

    [Test]
    public void CreateAsync_WithOffMapSenderCoordinate_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(new Message
            {
                Content = "Hello",
                SenderCommanderId = _commander.Id,
                SenderCoordinateQ = 999, SenderCoordinateR = 999
            }));
    }

    [Test]
    public void CreateAsync_WithOffMapTargetCoordinate_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(new Message
            {
                Content = "Hello",
                SenderCommanderId = _commander.Id,
                TargetCoordinateQ = 999, TargetCoordinateR = 999
            }));
    }

    [Test]
    public void CreateAsync_WithOneNullSenderCoordinate_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(new Message
            {
                Content = "Hello",
                SenderCommanderId = _commander.Id,
                SenderCoordinateQ = _hexQ, SenderCoordinateR = null
            }));
    }

    [Test]
    public async Task UpdateAsync_WithOffMapCoordinates_Throws()
    {
        var message = await _service.CreateAsync(new Message
        {
            Content = "Hello",
            SenderCommanderId = _commander.Id,
            CoordinateQ = _hexQ, CoordinateR = _hexR
        });

        message.CoordinateQ = 999;
        message.CoordinateR = 999;

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateAsync(message));
    }
}
