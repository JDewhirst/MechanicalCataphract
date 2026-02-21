using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MechanicalCataphract.Services;

public class WeatherService : IWeatherService
{
    private readonly WargameDbContext _context;
    private readonly IGameRulesService _gameRulesService;
    private readonly Random _random = new();

    public WeatherService(WargameDbContext context, IGameRulesService gameRulesService)
    {
        _context = context;
        _gameRulesService = gameRulesService;
    }

    public async Task<int> UpdateDailyWeatherAsync(DateTime gameDate)
    {
        var updateDate = gameDate.Date;

        // Gate: skip if already updated for this game date
        bool alreadyUpdated = await _context.WeatherUpdateRecords
            .AnyAsync(r => r.UpdateDate == updateDate);
        if (alreadyUpdated)
            return 0;

        // Load all weather types and build per-row transition lookup
        var allWeatherTypes = await _context.WeatherTypes.ToListAsync();
        var transitions = _gameRulesService.Rules.Weather.Transitions;

        var transitionLookup = new Dictionary<string, List<(Weather, double)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (fromName, targetProbabilities) in transitions)
        {
            var pool = new List<(Weather, double)>();
            foreach (var (toName, weight) in targetProbabilities)
            {
                var match = allWeatherTypes.FirstOrDefault(w =>
                    string.Equals(w.Name, toName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    pool.Add((match, weight));
            }
            if (pool.Count > 0)
                transitionLookup[fromName] = pool;
        }

        if (transitionLookup.Count == 0)
            return 0;

        // Fallback row: "Clear" if present, else the first available row
        if (!transitionLookup.TryGetValue("Clear", out var fallbackRow))
            fallbackRow = transitionLookup.Values.First();

        // Load hexes with current weather and apply Markov transition
        var hexes = await _context.MapHexes.Include(h => h.Weather).ToListAsync();
        foreach (var hex in hexes)
        {
            var currentName = hex.Weather?.Name ?? "";
            if (!transitionLookup.TryGetValue(currentName, out var row))
                row = fallbackRow;

            double totalWeight = row.Sum(p => p.Item2);
            hex.WeatherId = PickWeightedRandom(row, totalWeight).Id;
        }

        _context.WeatherUpdateRecords.Add(new WeatherUpdateRecord { UpdateDate = updateDate });
        await _context.SaveChangesAsync();

        return hexes.Count;
    }

    private Weather PickWeightedRandom(List<(Weather weather, double weight)> pool, double totalWeight)
    {
        double roll = _random.NextDouble() * totalWeight;
        double cumulative = 0;
        foreach (var (weather, weight) in pool)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return weather;
        }
        return pool[^1].weather;
    }
}
