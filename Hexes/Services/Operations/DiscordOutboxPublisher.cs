using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Discord;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;

namespace MechanicalCataphract.Services.Operations;

public class DiscordOutboxPublisher : IDiscordOutboxPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WargameDbContext _context;
    private readonly IDiscordBotService _botService;

    public DiscordOutboxPublisher(WargameDbContext context, IDiscordBotService botService)
    {
        _context = context;
        _botService = botService;
    }

    public async Task<DiscordOutboxPublishResult> PublishRunAsync(int runId)
    {
        var result = new DiscordOutboxPublishResult();
        var messages = await _context.DiscordOutboxMessages
            .Where(m => m.RunId == runId && m.Status == DiscordOutboxMessageStatus.Pending)
            .OrderBy(m => m.Id)
            .ToListAsync();

        foreach (var message in messages)
        {
            message.Status = DiscordOutboxMessageStatus.Sending;
            message.AttemptCount += 1;
            message.LastAttemptAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            try
            {
                if (!_botService.IsConnected || _botService.Client == null)
                    throw new InvalidOperationException("Discord bot is not connected.");

                await _botService.Client.Rest.SendMessageAsync(
                    message.TargetChannelId,
                    BuildMessageProperties(message));

                message.Status = DiscordOutboxMessageStatus.Sent;
                message.SentAtUtc = DateTime.UtcNow;
                message.LastError = null;
                result.Sent++;
            }
            catch (Exception ex)
            {
                message.Status = DiscordOutboxMessageStatus.Failed;
                message.LastError = ex.Message;
                result.Failed++;
            }

            await _context.SaveChangesAsync();
        }

        return result;
    }

    private static MessageProperties BuildMessageProperties(DiscordOutboxMessage message)
    {
        return message.MessageType switch
        {
            DiscordOutboxMessageType.EmbedBatch => BuildEmbedBatchMessage(message.PayloadJson),
            DiscordOutboxMessageType.PlainText => new MessageProperties { Content = message.PayloadJson },
            _ => throw new NotSupportedException($"Unsupported Discord outbox message type: {message.MessageType}")
        };
    }

    private static MessageProperties BuildEmbedBatchMessage(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<DiscordOutboxEmbedBatchPayload>(payloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Discord embed batch payload was empty.");

        return new MessageProperties { Embeds = payload.ToEmbedProperties() };
    }
}
