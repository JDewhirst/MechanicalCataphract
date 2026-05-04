using System.Threading.Tasks;

namespace MechanicalCataphract.Services.Operations;

public interface IDiscordOutboxPublisher
{
    Task<DiscordOutboxPublishResult> PublishRunAsync(int runId);
}

public class DiscordOutboxPublishResult
{
    public int Sent { get; set; }
    public int Failed { get; set; }
}
