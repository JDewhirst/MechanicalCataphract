using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services.Operations;

public class RefereeActionExecutor : IRefereeActionExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WargameDbContext _context;
    private readonly IGameStateService _gameStateService;
    private readonly IEnumerable<IRefereeActionHandler> _handlers;
    private readonly IDiscordOutboxPublisher _outboxPublisher;

    public RefereeActionExecutor(
        WargameDbContext context,
        IGameStateService gameStateService,
        IEnumerable<IRefereeActionHandler> handlers,
        IDiscordOutboxPublisher outboxPublisher)
    {
        _context = context;
        _gameStateService = gameStateService;
        _handlers = handlers;
        _outboxPublisher = outboxPublisher;
    }

    public async Task<RefereeActionExecutionResult> ExecuteAsync(RefereeActionRequest request)
    {
        var handler = _handlers.FirstOrDefault(h => h.ActionType == request.ActionType);
        if (handler == null)
            throw new InvalidOperationException($"No referee action handler is registered for {request.ActionType}.");

        var gameState = await _gameStateService.GetGameStateAsync();
        var run = new RefereeActionRun
        {
            ActionType = request.ActionType,
            TriggerType = request.TriggerType,
            Status = RefereeActionRunStatus.Running,
            CorrelationId = request.CorrelationId,
            IdempotencyKey = request.IdempotencyKey,
            ScheduledActionId = request.ScheduledActionId,
            ScheduledFireTimeUtc = request.ScheduledFireTimeUtc,
            RequestedBy = request.RequestedBy,
            ParametersJson = request.ParametersJson,
            QueuedAtUtc = DateTime.UtcNow,
            StartedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            GameTimeBeforeWorldHour = gameState.CurrentWorldHour
        };

        _context.RefereeActionRuns.Add(run);

        RefereeActionHandlerResult handlerResult;
        await using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            await _context.SaveChangesAsync();
            handlerResult = await handler.ExecuteAsync(run, request);

            var gameStateAfter = await _gameStateService.GetGameStateAsync();
            run.GameTimeAfterWorldHour = gameStateAfter.CurrentWorldHour;
            run.FinishedAtUtc = DateTime.UtcNow;
            run.SummaryJson = handlerResult.Summary == null
                ? null
                : JsonSerializer.Serialize(handlerResult.Summary, JsonOptions);
            run.ErrorMessage = handlerResult.ErrorMessage;
            run.Status = handlerResult.Success ? RefereeActionRunStatus.Succeeded : RefereeActionRunStatus.Failed;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        var executionResult = new RefereeActionExecutionResult
        {
            RunId = run.Id,
            Status = run.Status,
            ErrorMessage = run.ErrorMessage,
            OutboxMessagesCreated = handlerResult.OutboxMessagesCreated
        };

        if (handlerResult.Success && request.PublishOutboxImmediately && handlerResult.OutboxMessagesCreated > 0)
        {
            var publish = await _outboxPublisher.PublishRunAsync(run.Id);
            executionResult.OutboxMessagesSent = publish.Sent;
            executionResult.OutboxMessagesFailed = publish.Failed;

            run.Status = DeterminePublishedRunStatus(publish.Sent, publish.Failed);
            run.SummaryJson = MergePublishSummary(run.SummaryJson, publish);
            await _context.SaveChangesAsync();

            executionResult.Status = run.Status;
        }

        return executionResult;
    }

    private static RefereeActionRunStatus DeterminePublishedRunStatus(int sent, int failed)
    {
        if (failed == 0) return RefereeActionRunStatus.Succeeded;
        if (sent == 0) return RefereeActionRunStatus.Failed;
        return RefereeActionRunStatus.PartiallySucceeded;
    }

    private static string MergePublishSummary(string? existingSummaryJson, DiscordOutboxPublishResult publish)
    {
        var summary = string.IsNullOrWhiteSpace(existingSummaryJson)
            ? new Dictionary<string, object?>()
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(existingSummaryJson, JsonOptions)
                ?? new Dictionary<string, object?>();

        summary["outboxMessagesSent"] = publish.Sent;
        summary["outboxMessagesFailed"] = publish.Failed;
        return JsonSerializer.Serialize(summary, JsonOptions);
    }
}
