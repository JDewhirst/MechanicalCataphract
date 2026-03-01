using System.Threading.Tasks;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public interface IGameStateService
{
    Task<GameState> GetGameStateAsync();
    Task<long> GetCurrentWorldHourAsync();
    Task SetCurrentWorldHourAsync(long worldHour);
    Task AdvanceWorldHourAsync(int hours);
    Task UpdateGameStateAsync(GameState gameState);
}
