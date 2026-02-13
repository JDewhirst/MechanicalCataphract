using System;
using System.Linq;
using System.Threading.Tasks;
using Hexes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Discord;

/// <summary>
/// Handles incoming Discord messages from commander private channels.
/// Parses :envelope: messages and :scroll: orders, creating the
/// corresponding database entities.
/// </summary>
public class DiscordMessageHandler : IDiscordMessageHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDiscordBotService _botService;

    public event Action? EntitiesChanged;

    public DiscordMessageHandler(IServiceProvider serviceProvider, IDiscordBotService botService)
    {
        _serviceProvider = serviceProvider;
        _botService = botService;
    }

    /// <summary>
    /// Called by the bot service when a message is received.
    /// Identifies the sending commander by channel, parses the command, and creates entities.
    /// </summary>
    public async Task HandleMessageAsync(NetCord.Gateway.Message discordMessage)
    {
        // Ignore bot messages (including our own)
        if (discordMessage.Author.IsBot) return;

        var content = discordMessage.Content;
        var parsed = DiscordMessageParser.Parse(content);
        if (parsed == null) return; // Not a recognized command format

        var channelId = discordMessage.ChannelId;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();

            // Find the commander who owns this channel
            var commander = await db.Commanders
                .Include(c => c.Faction)
                .FirstOrDefaultAsync(c => c.DiscordChannelId == channelId);

            if (commander == null)
            {
                System.Diagnostics.Debug.WriteLine($"[MessageHandler] No commander found for channel {channelId}");
                return;
            }

            switch (parsed.Type)
            {
                case CommandType.Envelope:
                    await HandleEnvelopeAsync(db, commander, parsed, discordMessage);
                    break;

                case CommandType.Scroll:
                    await HandleScrollAsync(db, commander, parsed, discordMessage);
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MessageHandler] Error handling message: {ex.Message}");
        }
    }

    private async Task HandleEnvelopeAsync(
        WargameDbContext db, Commander sender, ParsedCommand parsed, NetCord.Gateway.Message discordMessage)
    {
        var message = new Message
        {
            SenderCommanderId = sender.Id,
            Content = parsed.Content,
            CoordinateQ = sender.CoordinateQ,
            CoordinateR = sender.CoordinateR,
            CreatedAt = DateTime.UtcNow,
        };

        // Resolve target commander by name (case-insensitive partial match)
        if (!string.IsNullOrWhiteSpace(parsed.TargetCommanderName))
        {
            var targetName = parsed.TargetCommanderName;
            var target = await db.Commanders
                .FirstOrDefaultAsync(c => c.Name.ToLower() == targetName.ToLower());

            // Fall back to partial match
            target ??= await db.Commanders
                .FirstOrDefaultAsync(c => c.Name.ToLower().Contains(targetName.ToLower()));

            if (target != null)
            {
                message.TargetCommanderId = target.Id;
                message.TargetCoordinateQ = target.CoordinateQ;
                message.TargetCoordinateR = target.CoordinateR;
            }
        }

        // Resolve target location from col,row → q,r
        if (parsed.TargetLocationCol.HasValue && parsed.TargetLocationRow.HasValue)
        {
            var hex = OffsetCoord.QoffsetToCube(OffsetCoord.ODD,
                new OffsetCoord(parsed.TargetLocationCol.Value, parsed.TargetLocationRow.Value));
            message.TargetCoordinateQ = hex.q;
            message.TargetCoordinateR = hex.r;
        }

        // Calculate path from sender to target location if we have both
        if (message.CoordinateQ.HasValue && message.CoordinateR.HasValue
            && message.TargetCoordinateQ.HasValue && message.TargetCoordinateR.HasValue)
        {
            using var pathScope = _serviceProvider.CreateScope();
            var pathService = pathScope.ServiceProvider.GetRequiredService<IPathfindingService>();
            var start = new Hex(message.CoordinateQ.Value, message.CoordinateR.Value,
                -(message.CoordinateQ.Value + message.CoordinateR.Value));
            var end = new Hex(message.TargetCoordinateQ.Value, message.TargetCoordinateR.Value,
                -(message.TargetCoordinateQ.Value + message.TargetCoordinateR.Value));

            var pathResult = await pathService.FindPathAsync(start, end, TravelEntityType.Message);
            if (pathResult.Success)
            {
                message.Path = pathResult.Path.ToList();
            }
        }

        db.Messages.Add(message);
        await db.SaveChangesAsync();
        EntitiesChanged?.Invoke();

        // React with ✉️ to acknowledge
        await ReactAsync(discordMessage, "\u2709\uFE0F");

        System.Diagnostics.Debug.WriteLine(
            $"[MessageHandler] Envelope from '{sender.Name}'" +
            $" target:'{parsed.TargetCommanderName ?? "none"}'" +
            $" location:{parsed.TargetLocationCol},{parsed.TargetLocationRow}" +
            $" → Message #{message.Id}");
    }

    private async Task HandleScrollAsync(
        WargameDbContext db, Commander sender, ParsedCommand parsed, NetCord.Gateway.Message discordMessage)
    {
        if (string.IsNullOrWhiteSpace(parsed.Content))
        {
            System.Diagnostics.Debug.WriteLine("[MessageHandler] Scroll with no content, ignoring.");
            return;
        }

        var order = new Order
        {
            CommanderId = sender.Id,
            Contents = parsed.Content,
            CreatedAt = DateTime.UtcNow,
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        EntitiesChanged?.Invoke();

        // React with 📜 to acknowledge
        await ReactAsync(discordMessage, "\U0001F4DC");

        System.Diagnostics.Debug.WriteLine(
            $"[MessageHandler] Scroll from '{sender.Name}' → Order #{order.Id}");
    }

    private async Task ReactAsync(NetCord.Gateway.Message message, string emoji)
    {
        if (_botService.Client == null) return;

        try
        {
            await _botService.Client.Rest.AddMessageReactionAsync(
                message.ChannelId, message.Id, new NetCord.Rest.ReactionEmojiProperties(emoji));
        }
        catch (Exception ex)
        {
            // Reaction failure is non-fatal
            System.Diagnostics.Debug.WriteLine($"[MessageHandler] React failed: {ex.Message}");
        }
    }
}
