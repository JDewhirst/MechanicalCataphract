using System;

namespace MechanicalCataphract.Data.Entities;

public enum DiscordOutboxTargetType
{
    RawChannelId = 1,
    CommanderChannel = 2,
    FactionChannel = 3,
    StaffChannel = 4
}

public enum DiscordOutboxMessageType
{
    PlainText = 1,
    EmbedBatch = 2,
    FileAttachment = 3
}

public enum DiscordOutboxMessageStatus
{
    Pending = 1,
    Sending = 2,
    Sent = 3,
    Failed = 4,
    Cancelled = 5
}

public class DiscordOutboxMessage
{
    public int Id { get; set; }
    public int RunId { get; set; }
    public RefereeActionRun? Run { get; set; }
    public ulong TargetChannelId { get; set; }
    public DiscordOutboxTargetType TargetType { get; set; }
    public DiscordOutboxMessageType MessageType { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DiscordOutboxMessageStatus Status { get; set; } = DiscordOutboxMessageStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? DiscordMessageId { get; set; }
    public string? DeduplicationKey { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
