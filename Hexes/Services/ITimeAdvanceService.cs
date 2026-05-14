using System.Threading.Tasks;

namespace MechanicalCataphract.Services
{
    public interface ITimeAdvanceService
    {
        Task<TimeAdvanceResult> AdvanceTimeAsync(int hours);
    }

    public class TimeAdvanceResult
    {
        public bool Success { get; init; }
        public string FormattedTime { get; init; } = string.Empty;
        public string? Error { get; init; }

        // Summary of what happened (for UI feedback)
        public int MessagesDelivered { get; init; }
        public int ArmiesMoved { get; init; }
        public int NaviesMoved { get; init; }
        public int CommandersMoved { get; init; }
        public int OrdersExecuted { get; init; }
        public int ArmiesSupplied { get; init; }
        public int CoLocationRemovals { get; init; }
        public int NewsProcessed { get; init; }
        public int WeatherUpdated { get; init; }
    }
}
