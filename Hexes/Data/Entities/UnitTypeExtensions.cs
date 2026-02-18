using System;

namespace MechanicalCataphract.Data.Entities;

/// <summary>
/// Single source of truth for all per-unit-type stats.
/// When adding a new UnitType value, add one case to each method here.
/// </summary>
public static class UnitTypeExtensions
{
    public static int SupplyConsumptionPerMan(this UnitType t) => t switch
    {
        UnitType.Infantry    => 1,
        UnitType.Skirmishers => 1,
        UnitType.Cavalry     => 10,
        _ => throw new ArgumentOutOfRangeException(nameof(t), t, "Unhandled UnitType")
    };

    public static int CarryCapacityPerMan(this UnitType t) => t switch
    {
        UnitType.Infantry    => 15,
        UnitType.Skirmishers => 15,
        UnitType.Cavalry     => 75,
        _ => throw new ArgumentOutOfRangeException(nameof(t), t, "Unhandled UnitType")
    };

    public static int CombatPowerPerMan(this UnitType t) => t switch
    {
        UnitType.Infantry    => 1,
        UnitType.Skirmishers => 1,
        UnitType.Cavalry     => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(t), t, "Unhandled UnitType")
    };

    public static int ScoutingRange(this UnitType t) => t switch
    {
        UnitType.Infantry    => 1,
        UnitType.Skirmishers => 1,
        UnitType.Cavalry     => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(t), t, "Unhandled UnitType")
    };

    /// <summary>Men per abstract column unit. Used in MarchingColumnLength.</summary>
    public static int MarchingColumnCapacity(this UnitType t) => t switch
    {
        UnitType.Infantry    => 5000,
        UnitType.Skirmishers => 5000,
        UnitType.Cavalry     => 2000,
        _ => throw new ArgumentOutOfRangeException(nameof(t), t, "Unhandled UnitType")
    };

    /// <summary>False for cavalry — they ford rivers without column-length penalty.</summary>
    public static bool CountsForFordingLength(this UnitType t) => t switch
    {
        UnitType.Infantry    => true,
        UnitType.Skirmishers => true,
        UnitType.Cavalry     => false,
        _ => throw new ArgumentOutOfRangeException(nameof(t), t, "Unhandled UnitType")
    };
}
