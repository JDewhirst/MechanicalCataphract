using System.Collections.Generic;

namespace MechanicalCataphract.Services;

public record MovementRules(
    int RoadCost,
    int OffRoadCost,
    double ArmyMovementMultiplier,
    int MarchDayStartHour,
    int MarchDayEndHour,
    int LongColumnThreshold,
    double LongColumnSpeedCap,
    double ForcedMarchMultiplier,
    int RiverFordingCostPerColumnUnit);

public record MovementRateRules(
    double ArmyBaseRate,
    double MessengerBaseRate,
    double CommanderBaseRate);

public record SupplyRules(
    int WagonSupplyMultiplier,
    int ForageMultiplierPerDensity);

public record UnitTypeStats(
    int SupplyConsumptionPerMan,
    int CarryCapacityPerMan,
    int CombatPowerPerMan,
    int ScoutingRange,
    int MarchingColumnCapacity,
    bool CountsForFordingLength);

public record UnitStatsRules(
    UnitTypeStats Infantry,
    UnitTypeStats Skirmishers,
    UnitTypeStats Cavalry);

public record NewsRules(
    double OffRoadHoursPerHex,
    double RoadHoursPerHex);

public record WeatherRules(
    int DailyUpdateHour,
    Dictionary<string, double> Probabilities);

public record GameRulesData(
    MovementRules Movement,
    MovementRateRules MovementRates,
    SupplyRules Supply,
    UnitStatsRules UnitStats,
    NewsRules News,
    WeatherRules Weather);
