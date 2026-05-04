using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hexes;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services.Calendar;
using Microsoft.EntityFrameworkCore;

namespace MechanicalCataphract.Services.Operations;

public class ArmyReportService : IArmyReportService
{
    private const int MaxEmbedsPerDiscordMessage = 10;

    private readonly WargameDbContext _context;
    private readonly ICalendarService _calendarService;

    public ArmyReportService(WargameDbContext context, ICalendarService calendarService)
    {
        _context = context;
        _calendarService = calendarService;
    }

    public async Task<ArmyReportGenerationResult> BuildForCommanderAsync(int commanderId, int? sourceArmyId = null)
    {
        var result = new ArmyReportGenerationResult { CommanderId = commanderId };

        var commander = await _context.Commanders
            .Include(c => c.CommandedArmies)
                .ThenInclude(a => a.Brigades)
            .FirstOrDefaultAsync(c => c.Id == commanderId);

        if (commander == null)
        {
            result.Warnings.Add($"Commander {commanderId} was not found.");
            return result;
        }

        result.CommanderName = commander.Name;
        result.TargetChannelId = commander.DiscordChannelId;

        if (!commander.DiscordChannelId.HasValue)
        {
            result.Warnings.Add($"Commander '{commander.Name}' has no Discord channel.");
            return result;
        }

        var gameState = await _context.GameStates.FindAsync(1);
        result.GameWorldHour = gameState?.CurrentWorldHour ?? 0;
        result.FormattedGameTime = _calendarService.FormatDateTime(result.GameWorldHour);

        var armies = commander.CommandedArmies
            .OrderBy(a => a.Name)
            .ThenBy(a => a.Id)
            .ToList();

        if (armies.Count == 0)
        {
            result.Warnings.Add($"Commander '{commander.Name}' commands no armies.");
            return result;
        }

        var embeds = armies
            .Select(army => BuildArmyReportPayload(army, commander, result.FormattedGameTime))
            .ToList();

        result.ReportCount = embeds.Count;

        for (var i = 0; i < embeds.Count; i += MaxEmbedsPerDiscordMessage)
        {
            result.Batches.Add(new DiscordOutboxEmbedBatchPayload
            {
                Embeds = embeds
                    .Skip(i)
                    .Take(MaxEmbedsPerDiscordMessage)
                    .ToList()
            });
        }

        if (sourceArmyId.HasValue && armies.All(a => a.Id != sourceArmyId.Value))
            result.Warnings.Add($"Source army {sourceArmyId.Value} is not commanded by '{commander.Name}'.");

        return result;
    }

    private static DiscordOutboxEmbedPayload BuildArmyReportPayload(Army army, Commander commander, string formattedGameTime)
    {
        var payload = new DiscordOutboxEmbedPayload
        {
            Title = $"Army Report: {army.Name}",
            Description = $"Commander: **{commander.Name}**",
            FooterText = $"Game Time: {formattedGameTime}"
        };

        var location = "Unknown";
        if (army.CoordinateQ.HasValue && army.CoordinateR.HasValue)
        {
            var hex = new Hex(army.CoordinateQ.Value, army.CoordinateR.Value,
                -(army.CoordinateQ.Value + army.CoordinateR.Value));
            var offset = OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, hex);
            location = $"Col {offset.col}, Row {offset.row}";
        }

        payload.Fields.Add(new DiscordOutboxEmbedFieldPayload { Name = "Location", Value = location, Inline = true });
        payload.Fields.Add(new DiscordOutboxEmbedFieldPayload { Name = "Morale", Value = army.Morale.ToString(), Inline = true });

        var status = army.IsGarrison ? "Garrison" : army.IsResting ? "Resting" : "Active";
        payload.Fields.Add(new DiscordOutboxEmbedFieldPayload { Name = "Status", Value = status, Inline = true });
        payload.Fields.Add(new DiscordOutboxEmbedFieldPayload { Name = "Combat Strength", Value = army.CombatStrength.ToString(), Inline = true });
        payload.Fields.Add(new DiscordOutboxEmbedFieldPayload { Name = "Carry Capacity", Value = army.CarryCapacity.ToString(), Inline = true });
        payload.Fields.Add(new DiscordOutboxEmbedFieldPayload
        {
            Name = "Supply",
            Value = $"{army.CarriedSupply} carried ({army.DaysOfSupply:F1} days)",
            Inline = true
        });
        payload.Fields.Add(new DiscordOutboxEmbedFieldPayload { Name = "Non-Combatants", Value = army.NonCombatants.ToString(), Inline = true });
        payload.Fields.Add(new DiscordOutboxEmbedFieldPayload { Name = "Wagons", Value = army.Wagons.ToString(), Inline = true });
        payload.Fields.Add(new DiscordOutboxEmbedFieldPayload { Name = "Loot", Value = army.CarriedLoot.ToString(), Inline = true });
        payload.Fields.Add(new DiscordOutboxEmbedFieldPayload { Name = "Coins", Value = army.CarriedCoins.ToString(), Inline = true });

        if (army.Brigades.Count != 0)
        {
            var sb = new StringBuilder();
            foreach (var brigade in army.Brigades.OrderBy(b => b.SortOrder).ThenBy(b => b.Id))
                sb.AppendLine($"**{brigade.Name}** - {brigade.Number} {brigade.UnitType}");

            payload.Fields.Add(new DiscordOutboxEmbedFieldPayload
            {
                Name = "Brigades",
                Value = sb.ToString().TrimEnd(),
                Inline = false
            });
        }

        return payload;
    }
}
