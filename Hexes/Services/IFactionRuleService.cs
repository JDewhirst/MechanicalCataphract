using System.Collections.Generic;
using System.Threading.Tasks;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public interface IFactionRuleService
{
    Task<IList<FactionRule>> GetRulesForFactionAsync(int factionId);
    Task<double> GetRuleValueAsync(int factionId, string key, double defaultValue = 0.0);

    /// <summary>Loads all rules for a faction into a synchronous cache. Call before A* or movement loops.</summary>
    Task PreloadForFactionAsync(int factionId);

    /// <summary>Synchronous lookup — only valid after PreloadForFactionAsync has been called.</summary>
    double GetCachedRuleValue(int factionId, string key, double defaultValue = 0.0);

    Task<FactionRule> AddRuleAsync(FactionRule rule);
    Task UpdateRuleAsync(FactionRule rule);
    Task DeleteRuleAsync(int ruleId);
}
