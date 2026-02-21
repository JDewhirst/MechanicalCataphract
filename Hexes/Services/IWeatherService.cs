using System;
using System.Threading.Tasks;

namespace MechanicalCataphract.Services;

public interface IWeatherService
{
    /// <summary>
    /// Assigns random weather to all hexes based on game_rules.json probabilities.
    /// Skips if weather has already been updated for the given game date.
    /// Returns the number of hexes updated (0 if skipped).
    /// </summary>
    Task<int> UpdateDailyWeatherAsync(DateTime gameDate);
}
