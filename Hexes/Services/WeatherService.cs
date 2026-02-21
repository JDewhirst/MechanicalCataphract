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

        // Load all weather types and build weighted pool from rules
        var allWeatherTypes = await _context.WeatherTypes.ToListAsync();
        var probabilities = _gameRulesService.Rules.Weather.Probabilities;

        var weightedPool = new List<(Weather weather, double weight)>();
        foreach (var weather in allWeatherTypes)
        {
            if (probabilities.TryGetValue(weather.Name, out double weight))
                weightedPool.Add((weather, weight));
        }

        if (weightedPool.Count == 0)
            return 0;

        double totalWeight = weightedPool.Sum(p => p.weight);

        // Load all hexes and assign random weather
        var hexes = await _context.MapHexes.ToListAsync();
        foreach (var hex in hexes)
        {
            hex.WeatherId = PickWeightedRandom(weightedPool, totalWeight).Id;
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
