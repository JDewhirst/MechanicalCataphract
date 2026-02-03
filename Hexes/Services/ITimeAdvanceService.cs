using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MechanicalCataphract.Services
{
    public interface ITimeAdvanceService
    {
        Task<TimeAdvanceResult> AdvanceTimeAsync(TimeSpan amount);
    }

    public class TimeAdvanceResult
    {
        public bool Success { get; init; }
        public DateTime NewGameTime { get; init; }
        public string? Error { get; init; }

        // Summary of what happened (for UI feedback)
        public int MessagesDelivered { get; init; }
        public int ArmiesMoved { get; init; }
        public int CommandersMoved { get; init; }
        public int OrdersExecuted { get; init; }
        public int ArmiesSupplied { get; init; }
    }
}
