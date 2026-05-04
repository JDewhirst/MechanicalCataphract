using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services.Operations;

public class SendArmyReportsParameters
{
    public int CommanderId { get; set; }
    public int? SourceArmyId { get; set; }
}

public class SendArmyReportsSummary
{
    public int CommanderId { get; set; }
    public string CommanderName { get; set; } = string.Empty;
    public int? SourceArmyId { get; set; }
    public int ReportsGenerated { get; set; }
    public int OutboxMessagesCreated { get; set; }
    public string[] Warnings { get; set; } = [];
}

public class SendArmyReportsAction : IRefereeActionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WargameDbContext _context;
    private readonly IArmyReportService _armyReportService;

    public SendArmyReportsAction(WargameDbContext context, IArmyReportService armyReportService)
    {
        _context = context;
        _armyReportService = armyReportService;
    }

    public RefereeActionType ActionType => RefereeActionType.SendArmyReports;

    public async Task<RefereeActionHandlerResult> ExecuteAsync(RefereeActionRun run, RefereeActionRequest request)
    {
        var parameters = JsonSerializer.Deserialize<SendArmyReportsParameters>(request.ParametersJson, JsonOptions)
            ?? new SendArmyReportsParameters();

        if (parameters.CommanderId <= 0)
        {
            return new RefereeActionHandlerResult
            {
                Success = false,
                ErrorMessage = "SendArmyReports requires a commanderId parameter."
            };
        }

        var generation = await _armyReportService.BuildForCommanderAsync(parameters.CommanderId, parameters.SourceArmyId);
        var created = 0;

        var summary = new SendArmyReportsSummary
        {
            CommanderId = parameters.CommanderId,
            CommanderName = generation.CommanderName,
            SourceArmyId = parameters.SourceArmyId,
            ReportsGenerated = generation.ReportCount,
            Warnings = generation.Warnings.ToArray()
        };

        if (!generation.TargetChannelId.HasValue)
        {
            return new RefereeActionHandlerResult
            {
                Success = false,
                Summary = summary,
                ErrorMessage = generation.Warnings.FirstOrDefault()
                    ?? "SendArmyReports could not determine a Discord target channel."
            };
        }

        if (generation.TargetChannelId.HasValue)
        {
            for (var i = 0; i < generation.Batches.Count; i++)
            {
                _context.DiscordOutboxMessages.Add(new DiscordOutboxMessage
                {
                    RunId = run.Id,
                    TargetChannelId = generation.TargetChannelId.Value,
                    TargetType = DiscordOutboxTargetType.CommanderChannel,
                    MessageType = DiscordOutboxMessageType.EmbedBatch,
                    PayloadJson = JsonSerializer.Serialize(generation.Batches[i], JsonOptions),
                    Status = DiscordOutboxMessageStatus.Pending,
                    DeduplicationKey = $"run:{run.Id}:commander:{parameters.CommanderId}:army-reports:{i}",
                    CreatedAtUtc = System.DateTime.UtcNow
                });
                created++;
            }
        }

        summary.OutboxMessagesCreated = created;

        return new RefereeActionHandlerResult
        {
            Success = true,
            Summary = summary,
            OutboxMessagesCreated = created
        };
    }
}
