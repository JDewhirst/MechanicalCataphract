using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services.Operations;

public class RefereeActionRequest
{
    public RefereeActionType ActionType { get; set; }
    public RefereeActionTriggerType TriggerType { get; set; }
    public string? RequestedBy { get; set; }
    public string? CorrelationId { get; set; }
    public string? IdempotencyKey { get; set; }
    public int? ScheduledActionId { get; set; }
    public System.DateTime? ScheduledFireTimeUtc { get; set; }
    public string ParametersJson { get; set; } = "{}";
    public bool PublishOutboxImmediately { get; set; } = true;
}

public class RefereeActionExecutionResult
{
    public int RunId { get; set; }
    public RefereeActionRunStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public int OutboxMessagesCreated { get; set; }
    public int OutboxMessagesSent { get; set; }
    public int OutboxMessagesFailed { get; set; }
}

public class RefereeActionHandlerResult
{
    public bool Success { get; set; } = true;
    public object? Summary { get; set; }
    public string? ErrorMessage { get; set; }
    public int OutboxMessagesCreated { get; set; }
}
