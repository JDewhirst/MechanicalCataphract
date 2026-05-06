using System.Collections.Generic;
using MechanicalCataphract.Data.Entities;

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
    int WagonCarryCapacity,
    int ForageMultiplierPerDensity,
    int DailyUsageHour);

public record ArmiesRules(
    int DailyReportHour);

public record UnitTypeStats(
    int SupplyConsumptionPerMan,
    int CarryCapacityPerMan,
    int CombatPowerPerMan,
    int ScoutingRange,
    int MarchingColumnCapacity,
    bool CountsForFordingLength,
    double MovementRate);

public record NewsRules(
    double OffRoadHoursPerHex,
    double RoadHoursPerHex,
    double WaterHoursPerHex);

public record WeatherRules(
    int DailyUpdateHour,
    Dictionary<string, Dictionary<string, double>> Transitions);

public record ShipTypeStats(double CapacityMultiplier);

public record ShipRules(
    int TransportInfantryCapacity,
    int TransportCavalryCapacity,
    int TransportSupplyCapacity,
    int TransportWagonCapacity,
    int CrewSupplyConsumptionPerShip,
    int SeaOrDownriverHexesPerDay,
    int UpriverMovementCostMultiplier,
    int RowingBonusHexesPerDay,
    IReadOnlyDictionary<ShipType, ShipTypeStats> ShipTypes);

public record GameRulesData(
    MovementRules Movement,
    MovementRateRules MovementRates,
    SupplyRules Supply,
    ArmiesRules Armies,
    IReadOnlyDictionary<UnitType, UnitTypeStats> UnitStats,
    NewsRules News,
    WeatherRules Weather,
    ShipRules Ships);
