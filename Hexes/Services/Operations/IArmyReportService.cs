using System.Collections.Generic;
using System.Threading.Tasks;

namespace MechanicalCataphract.Services.Operations;

public interface IArmyReportService
{
    Task<ArmyReportGenerationResult> BuildForCommanderAsync(int commanderId, int? sourceArmyId = null);
}

public class ArmyReportGenerationResult
{
    public int CommanderId { get; set; }
    public string CommanderName { get; set; } = string.Empty;
    public ulong? TargetChannelId { get; set; }
    public long GameWorldHour { get; set; }
    public string FormattedGameTime { get; set; } = string.Empty;
    public int ReportCount { get; set; }
    public List<DiscordOutboxEmbedBatchPayload> Batches { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
