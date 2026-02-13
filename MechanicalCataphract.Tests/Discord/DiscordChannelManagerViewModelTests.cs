using Moq;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Discord;
using MechanicalCataphract.Services;
using GUI.ViewModels;
using GUI.ViewModels.EntityViewModels;

namespace MechanicalCataphract.Tests.Discord;

/// <summary>
/// Tests that ViewModels invoke the correct IDiscordChannelManager methods
/// in response to property changes and commands.
/// </summary>
[TestFixture]
public class DiscordChannelManagerViewModelTests
{
    private Mock<IDiscordChannelManager> _channelMgr = null!;
    private Mock<IFactionService> _factionService = null!;
    private Mock<ICommanderService> _commanderService = null!;
    private Mock<ICoLocationChannelService> _coLocService = null!;

    [SetUp]
    public void SetUp()
    {
        _channelMgr = new Mock<IDiscordChannelManager>();
        _factionService = new Mock<IFactionService>();
        _commanderService = new Mock<ICommanderService>();
        _coLocService = new Mock<ICoLocationChannelService>();
    }

    #region FactionViewModel Tests

    [Test]
    public async Task FactionViewModel_NameChange_TriggersDiscordUpdate()
    {
        var faction = new Faction { Id = 5, Name = "Old Name", ColorHex = "#FF0000", Armies = [], Commanders = [] };
        var vm = new FactionViewModel(faction, _factionService.Object, _channelMgr.Object);

        vm.Name = "New Name";

        // Debounce is 1.5s — wait for it to fire
        await Task.Delay(2000);

        _channelMgr.Verify(
            m => m.OnFactionUpdatedAsync(faction, "Old Name", null),
            Times.Once,
            "Name change should trigger OnFactionUpdatedAsync with old name");
    }

    [Test]
    public async Task FactionViewModel_ColorChange_TriggersDiscordUpdate()
    {
        var faction = new Faction { Id = 5, Name = "Test", ColorHex = "#FF0000", Armies = [], Commanders = [] };
        var vm = new FactionViewModel(faction, _factionService.Object, _channelMgr.Object);

        vm.ColorHex = "#00FF00";

        await Task.Delay(2000);

        _channelMgr.Verify(
            m => m.OnFactionUpdatedAsync(faction, null, "#FF0000"),
            Times.Once,
            "Color change should trigger OnFactionUpdatedAsync with old color");
    }

    [Test]
    public async Task FactionViewModel_NameAndColorChange_DebouncesIntoSingleCall()
    {
        var faction = new Faction { Id = 5, Name = "Old", ColorHex = "#FF0000", Armies = [], Commanders = [] };
        var vm = new FactionViewModel(faction, _factionService.Object, _channelMgr.Object);

        vm.Name = "New";
        vm.ColorHex = "#00FF00";

        await Task.Delay(2000);

        // Should be a single call with both old values
        _channelMgr.Verify(
            m => m.OnFactionUpdatedAsync(faction, "Old", "#FF0000"),
            Times.Once,
            "Rapid name+color changes should debounce into a single Discord update");
    }

    [Test]
    public async Task FactionViewModel_RapidNameChanges_DebouncesFinalValue()
    {
        var faction = new Faction { Id = 5, Name = "Original", ColorHex = "#FF0000", Armies = [], Commanders = [] };
        var vm = new FactionViewModel(faction, _factionService.Object, _channelMgr.Object);

        vm.Name = "First";
        vm.Name = "Second";
        vm.Name = "Third";

        await Task.Delay(2000);

        // Should fire once with the ORIGINAL name as oldName (captured on first keystroke)
        _channelMgr.Verify(
            m => m.OnFactionUpdatedAsync(faction, "Original", null),
            Times.Once,
            "Rapid changes should debounce — oldName captures the pre-burst value");

        // The entity should hold the final value
        Assert.That(faction.Name, Is.EqualTo("Third"));
    }

    [Test]
    public void FactionViewModel_NoDiscordManager_DoesNotThrow()
    {
        var faction = new Faction { Id = 5, Name = "Test", ColorHex = "#FF0000", Armies = [], Commanders = [] };
        var vm = new FactionViewModel(faction, _factionService.Object, discordChannelManager: null);

        Assert.DoesNotThrow(() => vm.Name = "Changed");
        Assert.DoesNotThrow(() => vm.ColorHex = "#00FF00");
    }

    [Test]
    public async Task FactionViewModel_RulesChange_DoesNotTriggerDiscordUpdate()
    {
        var faction = new Faction { Id = 5, Name = "Test", ColorHex = "#FF0000", Armies = [], Commanders = [] };
        var vm = new FactionViewModel(faction, _factionService.Object, _channelMgr.Object);

        vm.Rules = "Some rules text";

        await Task.Delay(2000);

        _channelMgr.Verify(
            m => m.OnFactionUpdatedAsync(It.IsAny<Faction>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never,
            "Rules changes should not trigger Discord updates");
    }

    #endregion

    #region CommanderViewModel Tests

    private CommanderViewModel CreateCommanderVM(Commander commander, Faction? faction = null)
    {
        var armies = Array.Empty<Army>();
        var factions = faction != null ? new[] { faction } : Array.Empty<Faction>();

        return new CommanderViewModel(
            commander,
            _commanderService.Object,
            armies,
            factions,
            pathfindingService: null,
            discordChannelManager: _channelMgr.Object);
    }

    [Test]
    public async Task CommanderViewModel_SetDiscordUserId_TriggersChannelCreation()
    {
        var faction = new Faction { Id = 2, Name = "Red", ColorHex = "#FF0000", DiscordCategoryId = 100 };
        var commander = new Commander { Id = 1, Name = "Test Cmdr", Faction = faction, FactionId = 2 };
        var vm = CreateCommanderVM(commander, faction);

        vm.DiscordUserId = "216992826120994816";

        // SaveAndLinkDiscordAsync runs fire-and-forget — give it time
        await Task.Delay(500);

        _channelMgr.Verify(
            m => m.OnCommanderDiscordLinkedAsync(commander, faction),
            Times.Once,
            "Setting DiscordUserId from null should trigger OnCommanderDiscordLinkedAsync");
    }

    [Test]
    public async Task CommanderViewModel_ChangeDiscordUserId_DoesNotRetriggerChannelCreation()
    {
        var faction = new Faction { Id = 2, Name = "Red", ColorHex = "#FF0000", DiscordCategoryId = 100 };
        var commander = new Commander { Id = 1, Name = "Test", Faction = faction, FactionId = 2, DiscordUserId = 111UL };
        var vm = CreateCommanderVM(commander, faction);

        // Change from one ID to another (not null→value)
        vm.DiscordUserId = "222";

        await Task.Delay(500);

        _channelMgr.Verify(
            m => m.OnCommanderDiscordLinkedAsync(It.IsAny<Commander>(), It.IsAny<Faction>()),
            Times.Never,
            "Changing an existing Discord ID should NOT trigger channel creation");
    }

    [Test]
    public async Task CommanderViewModel_ClearDiscordUserId_DoesNotTriggerChannelCreation()
    {
        var faction = new Faction { Id = 2, Name = "Red", ColorHex = "#FF0000", DiscordCategoryId = 100 };
        var commander = new Commander { Id = 1, Name = "Test", Faction = faction, FactionId = 2, DiscordUserId = 111UL };
        var vm = CreateCommanderVM(commander, faction);

        vm.DiscordUserId = "";

        await Task.Delay(500);

        _channelMgr.Verify(
            m => m.OnCommanderDiscordLinkedAsync(It.IsAny<Commander>(), It.IsAny<Faction>()),
            Times.Never,
            "Clearing Discord ID should NOT trigger channel creation");
    }

    [Test]
    public async Task CommanderViewModel_SetDiscordUserId_NoFaction_DoesNotThrow()
    {
        var commander = new Commander { Id = 1, Name = "Test", Faction = null, FactionId = 1 };
        var vm = CreateCommanderVM(commander);

        // Should not throw even with no faction
        vm.DiscordUserId = "216992826120994816";

        await Task.Delay(500);

        _channelMgr.Verify(
            m => m.OnCommanderDiscordLinkedAsync(It.IsAny<Commander>(), It.IsAny<Faction>()),
            Times.Never,
            "No faction means no channel creation attempt");
    }

    [Test]
    public async Task CommanderViewModel_NameChange_TriggersDiscordRename()
    {
        var commander = new Commander { Id = 1, Name = "Old Name", DiscordChannelId = 999UL };
        var vm = CreateCommanderVM(commander);

        vm.Name = "New Name";

        // Debounce is 1.5s
        await Task.Delay(2000);

        _channelMgr.Verify(
            m => m.OnCommanderUpdatedAsync(commander),
            Times.Once,
            "Name change should trigger debounced OnCommanderUpdatedAsync for channel rename");
    }

    [Test]
    public async Task CommanderViewModel_NameChange_NoChannel_DoesNotTriggerRename()
    {
        var commander = new Commander { Id = 1, Name = "Old Name", DiscordChannelId = null };
        var vm = CreateCommanderVM(commander);

        vm.Name = "New Name";

        await Task.Delay(2000);

        _channelMgr.Verify(
            m => m.OnCommanderUpdatedAsync(It.IsAny<Commander>()),
            Times.Never,
            "No Discord channel means no rename attempt");
    }

    [Test]
    public async Task CommanderViewModel_RapidNameChanges_DebouncesSingleRename()
    {
        var commander = new Commander { Id = 1, Name = "Original", DiscordChannelId = 999UL };
        var vm = CreateCommanderVM(commander);

        vm.Name = "First";
        vm.Name = "Second";
        vm.Name = "Third";

        await Task.Delay(2000);

        _channelMgr.Verify(
            m => m.OnCommanderUpdatedAsync(commander),
            Times.Once,
            "Rapid name changes should debounce into a single Discord rename call");

        Assert.That(commander.Name, Is.EqualTo("Third"));
    }

    [Test]
    public async Task CommanderViewModel_FactionChange_TriggersDiscordFactionChange()
    {
        var oldFaction = new Faction { Id = 2, Name = "Red", ColorHex = "#FF0000", DiscordRoleId = 50 };
        var newFaction = new Faction { Id = 3, Name = "Blue", ColorHex = "#0000FF", DiscordRoleId = 60 };
        var commander = new Commander { Id = 1, Name = "Test", Faction = oldFaction, FactionId = 2, DiscordUserId = 111UL };
        var vm = CreateCommanderVM(commander, oldFaction);

        vm.Faction = newFaction;

        await Task.Delay(500);

        _channelMgr.Verify(
            m => m.OnCommanderFactionChangedAsync(commander, oldFaction, newFaction),
            Times.Once,
            "Faction change should trigger OnCommanderFactionChangedAsync with both old and new factions");
    }

    [Test]
    public void CommanderViewModel_NoDiscordManager_DoesNotThrow()
    {
        var commander = new Commander { Id = 1, Name = "Test", DiscordChannelId = 999UL };
        var vm = new CommanderViewModel(
            commander, _commanderService.Object,
            Array.Empty<Army>(), Array.Empty<Faction>(),
            pathfindingService: null, discordChannelManager: null);

        Assert.DoesNotThrow(() => vm.Name = "Changed");
        Assert.DoesNotThrow(() => vm.DiscordUserId = "12345");
    }

    #endregion

    #region HexMapViewModel — Faction CRUD Discord Integration

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

        // Make non-shared service calls return empty collections so refresh doesn't fail.
        // Note: _factionService and _commanderService are shared — each test sets them up.
        armyService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Army>());
        orderService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Order>());
        messageService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Message>());
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
            _channelMgr.Object);
    }

    [Test]
    public async Task HexMapViewModel_AddFaction_CallsOnFactionCreated()
    {
        _factionService.Setup(s => s.CreateAsync(It.IsAny<Faction>())).ReturnsAsync((Faction f) => f);
        _factionService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Faction>());
        _commanderService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Commander>());
        var vm = CreateHexMapVM();

        await vm.AddFactionCommand.ExecuteAsync(null);

        _channelMgr.Verify(
            m => m.OnFactionCreatedAsync(It.Is<Faction>(f => f.Name == "New Faction")),
            Times.Once,
            "Adding a faction should trigger OnFactionCreatedAsync");
    }

    [Test]
    public async Task HexMapViewModel_DeleteFaction_CallsOnFactionDeleted()
    {
        var faction = new Faction { Id = 5, Name = "Doomed", ColorHex = "#000000" };
        _commanderService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Commander>());
        _factionService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Faction>());
        var vm = CreateHexMapVM();

        await vm.DeleteFactionCommand.ExecuteAsync(faction);

        _channelMgr.Verify(
            m => m.OnFactionDeletedAsync(faction),
            Times.Once,
            "Deleting a faction should trigger OnFactionDeletedAsync");
    }

    [Test]
    public async Task HexMapViewModel_DeleteFaction_ReassignsCommandersToSentinel()
    {
        var faction = new Faction { Id = 5, Name = "Doomed", ColorHex = "#000000" };
        var cmd1 = new Commander { Id = 10, Name = "Cmdr1", FactionId = 5 };
        var cmd2 = new Commander { Id = 11, Name = "Cmdr2", FactionId = 5 };
        var cmdOther = new Commander { Id = 12, Name = "Neutral", FactionId = 1 };

        _commanderService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Commander> { cmd1, cmd2, cmdOther });
        _factionService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Faction>());
        var vm = CreateHexMapVM();

        await vm.DeleteFactionCommand.ExecuteAsync(faction);

        // Commanders belonging to deleted faction should be reassigned to Id=1
        Assert.That(cmd1.FactionId, Is.EqualTo(1), "Commander should be reassigned to sentinel faction");
        Assert.That(cmd2.FactionId, Is.EqualTo(1), "Commander should be reassigned to sentinel faction");
        Assert.That(cmdOther.FactionId, Is.EqualTo(1), "Other faction commander should remain unchanged");

        _commanderService.Verify(s => s.UpdateAsync(cmd1), Times.Once);
        _commanderService.Verify(s => s.UpdateAsync(cmd2), Times.Once);
    }

    #endregion

    #region HexMapViewModel — Commander CRUD Discord Integration

    [Test]
    public async Task HexMapViewModel_AddCommander_CallsOnCommanderCreated()
    {
        var faction = new Faction { Id = 2, Name = "Red", ColorHex = "#FF0000" };
        _factionService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Faction> { faction });
        _commanderService.Setup(s => s.CreateAsync(It.IsAny<Commander>())).ReturnsAsync((Commander c) => c);
        _commanderService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Commander>());

        var vm = CreateHexMapVM();
        // Load factions so Factions collection is populated
        await vm.RefreshFactionsAsync();

        await vm.AddCommanderCommand.ExecuteAsync(null);

        _channelMgr.Verify(
            m => m.OnCommanderCreatedAsync(
                It.Is<Commander>(c => c.Name == "New Commander"),
                It.Is<Faction>(f => f.Id == 2)),
            Times.Once,
            "Adding a commander should trigger OnCommanderCreatedAsync with the default faction");
    }

    [Test]
    public async Task HexMapViewModel_DeleteCommander_CallsOnCommanderDeleted()
    {
        var commander = new Commander { Id = 1, Name = "Doomed Cmdr" };
        _factionService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Faction>());
        _commanderService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Commander>());
        var vm = CreateHexMapVM();

        await vm.DeleteCommanderCommand.ExecuteAsync(commander);

        _channelMgr.Verify(
            m => m.OnCommanderDeletedAsync(commander),
            Times.Once,
            "Deleting a commander should trigger OnCommanderDeletedAsync");
    }

    #endregion
}
