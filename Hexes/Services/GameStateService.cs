using System;
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

    public async Task<DateTime> GetCurrentGameTimeAsync()
    {
        var gameState = await GetGameStateAsync();
        return gameState.CurrentGameTime;
    }

    public async Task AdvanceGameTimeAsync(TimeSpan amount)
    {
        var gameState = await GetGameStateAsync();
        gameState.CurrentGameTime = gameState.CurrentGameTime.Add(amount);
        await _context.SaveChangesAsync();
    }

    public async Task SetGameTimeAsync(DateTime gameTime)
    {
        var gameState = await GetGameStateAsync();
        gameState.CurrentGameTime = gameTime;
        await _context.SaveChangesAsync();
    }

    public async Task UpdateGameStateAsync(GameState gameState)
    {
        _context.GameStates.Update(gameState);
        await _context.SaveChangesAsync();
    }
}
