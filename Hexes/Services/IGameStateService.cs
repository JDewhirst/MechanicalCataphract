using System;
using System.Threading.Tasks;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public interface IGameStateService
{
    Task<GameState> GetGameStateAsync();
    Task<DateTime> GetCurrentGameTimeAsync();
    Task AdvanceGameTimeAsync(TimeSpan amount);
    Task SetGameTimeAsync(DateTime gameTime);
    Task UpdateGameStateAsync(GameState gameState);
}
