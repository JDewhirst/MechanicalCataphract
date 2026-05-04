using System;

namespace MechanicalCataphract.Data.Entities;

public enum RefereeActionType
{
    SendArmyReports = 1
}

public enum RefereeActionTriggerType
{
    Manual = 1,
    Scheduled = 2,
    Discord = 3,
    Cli = 4,
    Worker = 5
}

public enum RefereeActionRunStatus
{
    Queued = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4,
    PartiallySucceeded = 5,
    Cancelled = 6
}

public class RefereeActionRun
{
    public int Id { get; set; }
    public RefereeActionType ActionType { get; set; }
    public RefereeActionTriggerType TriggerType { get; set; }
    public RefereeActionRunStatus Status { get; set; } = RefereeActionRunStatus.Queued;
    public string? CorrelationId { get; set; }
    public string? IdempotencyKey { get; set; }
    public int? ScheduledActionId { get; set; }
    public DateTime? ScheduledFireTimeUtc { get; set; }
    public string? RequestedBy { get; set; }
    public DateTime QueuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public long? GameTimeBeforeWorldHour { get; set; }
    public long? GameTimeAfterWorldHour { get; set; }
    public string? ParametersJson { get; set; }
    public string? SummaryJson { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
