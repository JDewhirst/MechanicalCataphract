using Moq;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Discord;
using MechanicalCataphract.Services;
using GUI.ViewModels;
using GUI.ViewModels.EntityViewModels;

namespace MechanicalCataphract.Tests.Discord;

[TestFixture]
public class CoLocationChannelViewModelTests
{
    private Mock<IDiscordChannelManager> _channelMgr = null!;
    private Mock<ICoLocationChannelService> _coLocService = null!;
    private Mock<IFactionService> _factionService = null!;
    private Mock<ICommanderService> _commanderService = null!;

    [SetUp]
    public void SetUp()
    {
        _channelMgr = new Mock<IDiscordChannelManager>();
        _coLocService = new Mock<ICoLocationChannelService>();
        _factionService = new Mock<IFactionService>();
        _commanderService = new Mock<ICommanderService>();
    }

    private CoLocationChannelViewModel CreateVM(CoLocationChannel channel)
    {
        return new CoLocationChannelViewModel(
            channel,
            _coLocService.Object,
            Array.Empty<Army>(),
            Array.Empty<Commander>(),
            _channelMgr.Object);
    }

    #region CoLocationChannelViewModel Tests

    [Test]
    public async Task AddCommander_TriggersOnCommanderAddedToCoLocation()
    {
        var channel = new CoLocationChannel { Id = 1, Name = "Test Channel", DiscordChannelId = 500UL };
        var commander = new Commander { Id = 10, Name = "Cmdr", DiscordUserId = 111UL };
        var vm = new CoLocationChannelViewModel(
            channel, _coLocService.Object,
            Array.Empty<Army>(), new[] { commander },
            _channelMgr.Object);

        vm.SelectedCommanderToAdd = commander;
        await vm.AddCommanderCommand.ExecuteAsync(null);

        _channelMgr.Verify(
            m => m.OnCommanderAddedToCoLocationAsync(channel, commander),
            Times.Once,
            "Adding a commander should trigger OnCommanderAddedToCoLocationAsync");

        _coLocService.Verify(
            s => s.AddCommanderAsync(1, 10),
            Times.Once,
            "Should persist the commander addition via service");
    }

    [Test]
    public async Task RemoveCommander_TriggersOnCommanderRemovedFromCoLocation()
    {
        var commander = new Commander { Id = 10, Name = "Cmdr", DiscordUserId = 111UL };
        var channel = new CoLocationChannel
        {
            Id = 1, Name = "Test Channel", DiscordChannelId = 500UL,
            Commanders = new List<Commander> { commander }
        };
        var vm = CreateVM(channel);

        await vm.RemoveCommanderCommand.ExecuteAsync(commander);

        _channelMgr.Verify(
            m => m.OnCommanderRemovedFromCoLocationAsync(channel, commander),
            Times.Once,
            "Removing a commander should trigger OnCommanderRemovedFromCoLocationAsync");

        _coLocService.Verify(
            s => s.RemoveCommanderAsync(1, 10),
            Times.Once,
            "Should persist the commander removal via service");
    }

    [Test]
    public async Task NameChange_TriggersDebounceDiscordRename()
    {
        var channel = new CoLocationChannel { Id = 1, Name = "Old Name", DiscordChannelId = 500UL };
        var vm = CreateVM(channel);

        vm.Name = "New Name";

        // Debounce is 1.5s
        await Task.Delay(2000);

        _channelMgr.Verify(
            m => m.OnCoLocationChannelUpdatedAsync(channel),
            Times.Once,
            "Name change should trigger debounced OnCoLocationChannelUpdatedAsync");
    }

    [Test]
    public async Task RapidNameChanges_DebouncesSingleRename()
    {
        var channel = new CoLocationChannel { Id = 1, Name = "Original", DiscordChannelId = 500UL };
        var vm = CreateVM(channel);

        vm.Name = "First";
        vm.Name = "Second";
        vm.Name = "Third";

        await Task.Delay(2000);

        _channelMgr.Verify(
            m => m.OnCoLocationChannelUpdatedAsync(channel),
            Times.Once,
            "Rapid name changes should debounce into a single Discord rename call");

        Assert.That(channel.Name, Is.EqualTo("Third"));
    }

    [Test]
    public async Task NameChange_NoDiscordChannel_DoesNotTriggerRename()
    {
        var channel = new CoLocationChannel { Id = 1, Name = "Old Name", DiscordChannelId = null };
        var vm = CreateVM(channel);

        vm.Name = "New Name";

        await Task.Delay(2000);

        _channelMgr.Verify(
            m => m.OnCoLocationChannelUpdatedAsync(It.IsAny<CoLocationChannel>()),
            Times.Never,
            "No Discord channel means no rename attempt");
    }

    [Test]
    public void FollowingArmy_Set_ClearsHexCoords()
    {
        var channel = new CoLocationChannel
        {
            Id = 1, Name = "Test",
            FollowingHexQ = 5, FollowingHexR = 10
        };
        var vm = CreateVM(channel);

        var army = new Army { Id = 2, Name = "Army" };
        vm.FollowingArmy = army;

        Assert.That(channel.FollowingArmyId, Is.EqualTo(2));
        Assert.That(channel.FollowingHexQ, Is.Null, "Setting army should clear hex Q");
        Assert.That(channel.FollowingHexR, Is.Null, "Setting army should clear hex R");
    }

    [Test]
    public void FollowingHex_Set_ClearsArmy()
    {
        var army = new Army { Id = 2, Name = "Army" };
        var channel = new CoLocationChannel
        {
            Id = 1, Name = "Test",
            FollowingArmyId = 2, FollowingArmy = army
        };
        var vm = CreateVM(channel);

        vm.FollowingHexQ = 5;

        Assert.That(channel.FollowingArmyId, Is.Null, "Setting hex should clear army ID");
        Assert.That(channel.FollowingArmy, Is.Null, "Setting hex should clear army reference");
    }

    [Test]
    public void NoDiscordManager_DoesNotThrow()
    {
        var channel = new CoLocationChannel { Id = 1, Name = "Test", DiscordChannelId = 500UL };
        var vm = new CoLocationChannelViewModel(
            channel, _coLocService.Object,
            Array.Empty<Army>(), Array.Empty<Commander>(),
            discordChannelManager: null);

        Assert.DoesNotThrow(() => vm.Name = "Changed");
    }

    #endregion

    #region HexMapViewModel — CoLocation CRUD Discord Integration

    private HexMapViewModel CreateHexMapVM()
    {
        var mapService = new Mock<IMapService>();
        var armyService = new Mock<IArmyService>();
        var orderService = new Mock<IOrderService>();
        var messageService = new Mock<IMessageService>();
        var gameStateService = new Mock<IGameStateService>();
        var timeAdvanceService = new Mock<ITimeAdvanceService>();
        var pathfindingService = new Mock<IPathfindingService>();
        var botService = new Mock<IDiscordBotService>();

        armyService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Army>());
        orderService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Order>());
        messageService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Message>());
        _factionService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Faction>());
        _commanderService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Commander>());
        _coLocService.Setup(s => s.GetAllWithCommandersAsync()).ReturnsAsync(new List<CoLocationChannel>());

        return new HexMapViewModel(
            mapService.Object,
            _factionService.Object,
            armyService.Object,
            _commanderService.Object,
            orderService.Object,
            messageService.Object,
            gameStateService.Object,
            timeAdvanceService.Object,
            pathfindingService.Object,
            _coLocService.Object,
            botService.Object,
            _channelMgr.Object,
            new Mock<IDiscordMessageHandler>().Object);
    }

    [Test]
    public async Task HexMapViewModel_AddCoLocationChannel_CallsOnCreated()
    {
        _coLocService.Setup(s => s.CreateAsync(It.IsAny<CoLocationChannel>()))
            .ReturnsAsync((CoLocationChannel c) => c);

        var vm = CreateHexMapVM();
        await vm.AddCoLocationChannelCommand.ExecuteAsync(null);

        _channelMgr.Verify(
            m => m.OnCoLocationChannelCreatedAsync(It.Is<CoLocationChannel>(c => c.Name == "New Channel")),
            Times.Once,
            "Adding a co-location channel should trigger OnCoLocationChannelCreatedAsync");
    }

    [Test]
    public async Task HexMapViewModel_DeleteCoLocationChannel_CallsOnDeleted()
    {
        var channel = new CoLocationChannel { Id = 5, Name = "Doomed", DiscordChannelId = 999UL };

        var vm = CreateHexMapVM();
        await vm.DeleteCoLocationChannelCommand.ExecuteAsync(channel);

        _channelMgr.Verify(
            m => m.OnCoLocationChannelDeletedAsync(channel),
            Times.Once,
            "Deleting a co-location channel should trigger OnCoLocationChannelDeletedAsync");

        _coLocService.Verify(
            s => s.DeleteAsync(5),
            Times.Once,
            "Should delete the channel via service");
    }

    #endregion
}
