using System.Threading.Tasks;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services.Operations;

public interface IRefereeActionHandler
{
    RefereeActionType ActionType { get; }
    Task<RefereeActionHandlerResult> ExecuteAsync(RefereeActionRun run, RefereeActionRequest request);
}
