using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public class FactionRuleService : IFactionRuleService
{
    private readonly WargameDbContext _context;
    private readonly ConcurrentDictionary<(int factionId, string key), double> _cache = new();

    public FactionRuleService(WargameDbContext context)
    {
        _context = context;
    }

    public async Task<IList<FactionRule>> GetRulesForFactionAsync(int factionId)
    {
        return await _context.FactionRules
            .Where(r => r.FactionId == factionId)
            .ToListAsync();
    }

    public async Task<double> GetRuleValueAsync(int factionId, string key, double defaultValue = 0.0)
    {
        var rule = await _context.FactionRules
            .FirstOrDefaultAsync(r => r.FactionId == factionId && r.RuleKey == key);
        return rule?.Value ?? defaultValue;
    }

    public async Task PreloadForFactionAsync(int factionId)
    {
        var rules = await GetRulesForFactionAsync(factionId);
        foreach (var rule in rules)
            _cache[(factionId, rule.RuleKey)] = rule.Value;
    }

    public double GetCachedRuleValue(int factionId, string key, double defaultValue = 0.0)
    {
        return _cache.TryGetValue((factionId, key), out var value) ? value : defaultValue;
    }

    public async Task<FactionRule> AddRuleAsync(FactionRule rule)
    {
        _context.FactionRules.Add(rule);
        await _context.SaveChangesAsync();
        InvalidateCache(rule.FactionId);
        return rule;
    }

    public async Task UpdateRuleAsync(FactionRule rule)
    {
        _context.FactionRules.Update(rule);
        await _context.SaveChangesAsync();
        InvalidateCache(rule.FactionId);
    }

    public async Task DeleteRuleAsync(int ruleId)
    {
        var rule = await _context.FactionRules.FindAsync(ruleId);
        if (rule != null)
        {
            _context.FactionRules.Remove(rule);
            await _context.SaveChangesAsync();
            InvalidateCache(rule.FactionId);
        }
    }

    private void InvalidateCache(int factionId)
    {
        var keysToRemove = _cache.Keys.Where(k => k.factionId == factionId).ToList();
        foreach (var key in keysToRemove)
            _cache.TryRemove(key, out _);
    }
}
