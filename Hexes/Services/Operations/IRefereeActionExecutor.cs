using System.Threading.Tasks;

namespace MechanicalCataphract.Services.Operations;

public interface IRefereeActionExecutor
{
    Task<RefereeActionExecutionResult> ExecuteAsync(RefereeActionRequest request);
}
