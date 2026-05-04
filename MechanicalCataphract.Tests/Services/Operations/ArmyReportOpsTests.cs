using System.Text.Json;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using MechanicalCataphract.Services.Calendar;
using MechanicalCataphract.Services.Operations;
using MechanicalCataphract.Tests.Services.Integration;
using Moq;

namespace MechanicalCataphract.Tests.Services.Operations;

public class ArmyReportOpsTests : IntegrationTestBase
{
    [Test]
    public async Task BuildForCommanderAsync_UsesPersistedBrigadeSortOrder()
    {
        var commander = await SeedCommanderArmyAndBrigadesAsync();
        Context.ChangeTracker.Clear();

        var service = new ArmyReportService(Context, MockCalendarService());
        var result = await service.BuildForCommanderAsync(commander.Id);

        var brigadeField = result.Batches.Single().Embeds.Single().Fields.Single(f => f.Name == "Brigades");
        Assert.That(brigadeField.Value, Does.Contain("Charlie"));
        Assert.That(brigadeField.Value, Does.Contain("Alpha"));
        Assert.That(brigadeField.Value, Does.Contain("Bravo"));
        Assert.That(brigadeField.Value.IndexOf("Charlie"), Is.LessThan(brigadeField.Value.IndexOf("Alpha")));
        Assert.That(brigadeField.Value.IndexOf("Alpha"), Is.LessThan(brigadeField.Value.IndexOf("Bravo")));
    }

    [Test]
    public async Task SendArmyReportsAction_ReloadsStateFromIds()
    {
        var commander = await SeedCommanderArmyAndBrigadesAsync();
        var request = BuildRequest(commander.Id);

        var army = Context.Armies.Single(a => a.CommanderId == commander.Id);
        army.Name = "Fresh Name";
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var executor = BuildExecutor();
        var result = await executor.ExecuteAsync(request);

        Assert.That(result.Status, Is.EqualTo(RefereeActionRunStatus.Succeeded));
        var outbox = Context.DiscordOutboxMessages.Single(m => m.RunId == result.RunId);
        Assert.That(outbox.PayloadJson, Does.Contain("Fresh Name"));
    }

    [Test]
    public async Task SendArmyReportsAction_StoresImmutableOutboxPayload()
    {
        var commander = await SeedCommanderArmyAndBrigadesAsync();
        var executor = BuildExecutor();

        var result = await executor.ExecuteAsync(BuildRequest(commander.Id));
        var outbox = Context.DiscordOutboxMessages.Single(m => m.RunId == result.RunId);
        var originalPayload = outbox.PayloadJson;

        var brigades = Context.Brigades.Where(b => b.Army!.CommanderId == commander.Id).ToList();
        brigades.Single(b => b.Name == "Charlie").SortOrder = 2;
        brigades.Single(b => b.Name == "Bravo").SortOrder = 0;
        await Context.SaveChangesAsync();

        Assert.That(outbox.PayloadJson, Is.EqualTo(originalPayload));

        var payload = JsonSerializer.Deserialize<DiscordOutboxEmbedBatchPayload>(
            outbox.PayloadJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var brigadeField = payload!.Embeds.Single().Fields.Single(f => f.Name == "Brigades");
        Assert.That(brigadeField.Value.IndexOf("Charlie"), Is.LessThan(brigadeField.Value.IndexOf("Alpha")));
    }

    [Test]
    public async Task SendArmyReportsAction_FailsWhenCommanderHasNoDiscordChannel()
    {
        var commander = await SeedCommanderArmyAndBrigadesAsync();
        commander.DiscordChannelId = null;
        await Context.SaveChangesAsync();

        var executor = BuildExecutor();
        var result = await executor.ExecuteAsync(BuildRequest(commander.Id));

        Assert.That(result.Status, Is.EqualTo(RefereeActionRunStatus.Failed));
        Assert.That(result.ErrorMessage, Does.Contain("has no Discord channel"));
        Assert.That(Context.DiscordOutboxMessages.Count(m => m.RunId == result.RunId), Is.EqualTo(0));

        var run = Context.RefereeActionRuns.Single(r => r.Id == result.RunId);
        Assert.That(run.SummaryJson, Does.Contain("has no Discord channel"));
    }

    private RefereeActionExecutor BuildExecutor()
    {
        var calendar = MockCalendarService();
        var reportService = new ArmyReportService(Context, calendar);
        var handler = new SendArmyReportsAction(Context, reportService);
        return new RefereeActionExecutor(
            Context,
            new GameStateService(Context),
            new[] { handler },
            new NoopOutboxPublisher());
    }

    private static RefereeActionRequest BuildRequest(int commanderId)
        => new()
        {
            ActionType = RefereeActionType.SendArmyReports,
            TriggerType = RefereeActionTriggerType.Manual,
            ParametersJson = JsonSerializer.Serialize(new SendArmyReportsParameters
            {
                CommanderId = commanderId,
                SourceArmyId = 1
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            PublishOutboxImmediately = false
        };

    private async Task<Commander> SeedCommanderArmyAndBrigadesAsync()
    {
        var commander = new Commander
        {
            Name = "Commander",
            FactionId = 1,
            DiscordChannelId = 123
        };
        Context.Commanders.Add(commander);
        await Context.SaveChangesAsync();

        var army = new Army
        {
            Name = "Army",
            FactionId = 1,
            CommanderId = commander.Id,
            CoordinateQ = MapHex.SentinelQ,
            CoordinateR = MapHex.SentinelR,
            Morale = 9
        };
        Context.Armies.Add(army);
        await Context.SaveChangesAsync();

        Context.Brigades.AddRange(
            new Brigade { ArmyId = army.Id, FactionId = 1, Name = "Alpha", UnitType = UnitType.Infantry, Number = 100, SortOrder = 1 },
            new Brigade { ArmyId = army.Id, FactionId = 1, Name = "Bravo", UnitType = UnitType.Cavalry, Number = 50, SortOrder = 2 },
            new Brigade { ArmyId = army.Id, FactionId = 1, Name = "Charlie", UnitType = UnitType.Infantry, Number = 75, SortOrder = 0 });
        await Context.SaveChangesAsync();

        return commander;
    }

    private static ICalendarService MockCalendarService()
    {
        var calendar = new Mock<ICalendarService>();
        calendar.Setup(c => c.FormatDateTime(It.IsAny<long>())).Returns("Test Time");
        return calendar.Object;
    }

    private sealed class NoopOutboxPublisher : IDiscordOutboxPublisher
    {
        public Task<DiscordOutboxPublishResult> PublishRunAsync(int runId)
            => Task.FromResult(new DiscordOutboxPublishResult());
    }
}
