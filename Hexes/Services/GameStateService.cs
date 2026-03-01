using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public class GameStateService : IGameStateService
{
    private readonly WargameDbContext _context;

    public GameStateService(WargameDbContext context)
    {
        _context = context;
    }

    public async Task<GameState> GetGameStateAsync()
    {
        var gameState = await _context.GameStates.FindAsync(1);
        if (gameState == null)
        {
            gameState = new GameState { Id = 1 };
            _context.GameStates.Add(gameState);
            await _context.SaveChangesAsync();
        }
        return gameState;
    }

    public async Task<long> GetCurrentWorldHourAsync()
    {
        var gameState = await GetGameStateAsync();
        return gameState.CurrentWorldHour;
    }

    public async Task SetCurrentWorldHourAsync(long worldHour)
    {
        var gameState = await GetGameStateAsync();
        gameState.CurrentWorldHour = worldHour;
        await _context.SaveChangesAsync();
    }

    public async Task AdvanceWorldHourAsync(int hours)
    {
        var gameState = await GetGameStateAsync();
        gameState.CurrentWorldHour += hours;
        await _context.SaveChangesAsync();
    }

    public async Task UpdateGameStateAsync(GameState gameState)
    {
        _context.GameStates.Update(gameState);
        await _context.SaveChangesAsync();
    }
}
