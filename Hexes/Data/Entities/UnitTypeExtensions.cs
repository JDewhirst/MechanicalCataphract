using System;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Data.Entities;

/// <summary>
/// Single source of truth for all per-unit-type stats.
/// Values are read from GameRules.Current, which is loaded from game_rules.json at startup.
/// </summary>
public static class UnitTypeExtensions
{
    private static UnitTypeStats GetStats(UnitType t) => t switch
    {
        UnitType.Infantry    => GameRules.Current.UnitStats.Infantry,
        UnitType.Skirmishers => GameRules.Current.UnitStats.Skirmishers,
        UnitType.Cavalry     => GameRules.Current.UnitStats.Cavalry,
        _ => throw new ArgumentOutOfRangeException(nameof(t), t, "Unhandled UnitType")
    };

    public static int SupplyConsumptionPerMan(this UnitType t)    => GetStats(t).SupplyConsumptionPerMan;
    public static int CarryCapacityPerMan(this UnitType t)        => GetStats(t).CarryCapacityPerMan;
    public static int CombatPowerPerMan(this UnitType t)          => GetStats(t).CombatPowerPerMan;
    public static int ScoutingRange(this UnitType t)              => GetStats(t).ScoutingRange;
    public static int MarchingColumnCapacity(this UnitType t)     => GetStats(t).MarchingColumnCapacity;

    /// <summary>False for cavalry — they ford rivers without column-length penalty.</summary>
    public static bool CountsForFordingLength(this UnitType t)    => GetStats(t).CountsForFordingLength;
}
