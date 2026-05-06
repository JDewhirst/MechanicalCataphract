using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public class GameRulesService : IGameRulesService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GameRulesData Rules { get; private set; }

    public GameRulesService()
    {
        Rules = Load();
        GameRules.Current = Rules;
    }

    public void Reload()
    {
        Rules = Load();
        GameRules.Current = Rules;
    }

    private static GameRulesData Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "game_rules.json");
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var dto = JsonSerializer.Deserialize<GameRulesDto>(json, JsonOptions);
                if (dto != null)
                    return FromDto(dto);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameRulesService] Failed to load game_rules.json: {ex.Message}. Using defaults.");
            }
        }

        return CreateDefaults();
    }

    public static GameRulesData CreateDefaults()
    {
        var unitStats = new Dictionary<UnitType, UnitTypeStats>
        {
            [UnitType.Infantry]      = new(1, 15, 1, 1, 5000, true, 1.0),
            [UnitType.Skirmishers]   = new(1, 15, 1, 2, 5000, true, 1.0),
            [UnitType.Cavalry]       = new(10, 75, 2, 2, 2000, false, 1.5),
            [UnitType.Engineers]     = new(1, 15, 1, 1, 5000, true, 1.0),
            [UnitType.HeavyInfantry] = new(1, 15, 2, 1, 5000, true, 1.0),
            [UnitType.Huscarls]      = new(1, 15, 3, 1, 5000, true, 1.0),
            [UnitType.Otrangers]     = new(1, 15, 2, 2, 5000, true, 1.0),
            [UnitType.KnightLancers] = new(10, 75, 5, 2, 2000, false, 1.5),
            [UnitType.HeavyCavalry]  = new(10, 75, 4, 2, 2000, false, 1.5),
        };

        var shipTypes = new Dictionary<ShipType, ShipTypeStats>
        {
            [ShipType.Transport] = new(1.0),
            [ShipType.Warship]   = new(0.5),
            [ShipType.Longships] = new(1.0),
        };

        return new GameRulesData(
            Movement: new MovementRules(
                RoadCost: 6,
                OffRoadCost: 12,
                ArmyMovementMultiplier: 1.5,
                MarchDayStartHour: 8,
                MarchDayEndHour: 20,
                LongColumnThreshold: 6,
                LongColumnSpeedCap: 0.5,
                ForcedMarchMultiplier: 2.0,
                RiverFordingCostPerColumnUnit: 6),
            MovementRates: new MovementRateRules(
                ArmyBaseRate: 1.0,
                MessengerBaseRate: 2.0,
                CommanderBaseRate: 2.0),
            Supply: new SupplyRules(
                WagonSupplyMultiplier: 10,
                WagonCarryCapacity: 1000,
                ForageMultiplierPerDensity: 500,
                DailyUsageHour: 21),
            Armies: new ArmiesRules(
                DailyReportHour: 6),
            UnitStats: new ReadOnlyDictionary<UnitType, UnitTypeStats>(unitStats),
            News: new NewsRules(OffRoadHoursPerHex: 24.0, RoadHoursPerHex: 12.0, WaterHoursPerHex: 2.4),
            Weather: new WeatherRules(
                DailyUpdateHour: 6,
                Transitions: new Dictionary<string, Dictionary<string, double>>
                {
                    ["Clear"]    = new() { ["Clear"]=0.40, ["Overcast"]=0.25, ["Rain"]=0.20, ["Fog"]=0.15 },
                    ["Overcast"] = new() { ["Overcast"]=0.40, ["Rain"]=0.25, ["Fog"]=0.20, ["Clear"]=0.15 },
                    ["Rain"]     = new() { ["Fog"]=0.40, ["Clear"]=0.25, ["Overcast"]=0.20, ["Rain"]=0.15 },
                    ["Fog"]      = new() { ["Fog"]=0.40, ["Clear"]=0.25, ["Overcast"]=0.20, ["Rain"]=0.15 }
                }),
            Ships: new ShipRules(
                TransportInfantryCapacity: 100,
                TransportCavalryCapacity: 20,
                TransportSupplyCapacity: 10000,
                TransportWagonCapacity: 5,
                CrewSupplyConsumptionPerShip: 20,
                SeaOrDownriverHexesPerDay: 24,
                UpriverMovementCostMultiplier: 2,
                RowingBonusHexesPerDay: 12,
                ShipTypes: new ReadOnlyDictionary<ShipType, ShipTypeStats>(shipTypes)));
    }

    private static GameRulesData FromDto(GameRulesDto dto)
    {
        var m = dto.Movement ?? new MovementDto();
        var mr = dto.MovementRates ?? new MovementRatesDto();
        var s = dto.Supply ?? new SupplyDto();
        var ar = dto.Armies ?? new ArmiesDto();
        var n = dto.News ?? new NewsDto();
        var w = dto.Weather;
        var sh = dto.Ships;

        var defaults = CreateDefaults();

        return new GameRulesData(
            Movement: new MovementRules(
                RoadCost: m.RoadCost ?? 6,
                OffRoadCost: m.OffRoadCost ?? 12,
                ArmyMovementMultiplier: m.ArmyMovementMultiplier ?? 1.5,
                MarchDayStartHour: m.MarchDayStartHour ?? 8,
                MarchDayEndHour: m.MarchDayEndHour ?? 20,
                LongColumnThreshold: m.LongColumnThreshold ?? 6,
                LongColumnSpeedCap: m.LongColumnSpeedCap ?? 0.5,
                ForcedMarchMultiplier: m.ForcedMarchMultiplier ?? 2.0,
                RiverFordingCostPerColumnUnit: m.RiverFordingCostPerColumnUnit ?? 6),
            MovementRates: new MovementRateRules(
                ArmyBaseRate: mr.ArmyBaseRate ?? 1.0,
                MessengerBaseRate: mr.MessengerBaseRate ?? 2.0,
                CommanderBaseRate: mr.CommanderBaseRate ?? 2.0),
            Supply: new SupplyRules(
                WagonSupplyMultiplier: s.WagonSupplyMultiplier ?? 10,
                WagonCarryCapacity: s.WagonCarryCapacity ?? 1000,
                ForageMultiplierPerDensity: s.ForageMultiplierPerDensity ?? 500,
                DailyUsageHour: s.DailyUsageHour ?? defaults.Supply.DailyUsageHour),
            Armies: new ArmiesRules(
                DailyReportHour: ar.DailyReportHour ?? defaults.Armies.DailyReportHour),
            UnitStats: ParseUnitStats(dto.UnitStats, defaults.UnitStats),
            News: new NewsRules(
                OffRoadHoursPerHex: n.OffRoadHoursPerHex ?? 24.0,
                RoadHoursPerHex: n.RoadHoursPerHex ?? 12.0,
                WaterHoursPerHex: n.WaterHoursPerHex ?? 2.4),
            Weather: new WeatherRules(
                DailyUpdateHour: w?.DailyUpdateHour ?? defaults.Weather.DailyUpdateHour,
                Transitions: w?.Transitions ?? defaults.Weather.Transitions),
            Ships: FromShipsDto(sh, defaults.Ships));
    }

    private static IReadOnlyDictionary<UnitType, UnitTypeStats> ParseUnitStats(
        Dictionary<string, UnitTypeStatsDto>? dtoDict,
        IReadOnlyDictionary<UnitType, UnitTypeStats> defaults)
    {
        if (dtoDict == null || dtoDict.Count == 0)
            return defaults;

        var result = new Dictionary<UnitType, UnitTypeStats>(defaults);

        foreach (var (key, dto) in dtoDict)
        {
            if (!Enum.TryParse<UnitType>(key, ignoreCase: true, out var unitType))
            {
                System.Diagnostics.Debug.WriteLine($"[GameRulesService] Unknown UnitType key '{key}' in unitStats — skipping.");
                continue;
            }

            var fallback = defaults.TryGetValue(unitType, out var fb) ? fb : null;
            result[unitType] = FromUnitDto(dto, fallback);
        }

        return new ReadOnlyDictionary<UnitType, UnitTypeStats>(result);
    }

    private static UnitTypeStats FromUnitDto(UnitTypeStatsDto dto, UnitTypeStats? fallback)
    {
        // Determine defaults based on whether this is a cavalry-type or infantry-type unit.
        // If we have a fallback from CreateDefaults(), use it; otherwise infer from CountsForFordingLength.
        bool isCavalry = fallback != null ? !fallback.CountsForFordingLength
                       : dto.CountsForFordingLength == false;

        int defSupply   = isCavalry ? 10   : 1;
        int defCarry    = isCavalry ? 75   : 15;
        int defCombat   = isCavalry ? 2    : 1;
        int defScouting = isCavalry ? 2    : 1;
        int defColumn   = isCavalry ? 2000 : 5000;
        bool defFording = !isCavalry;
        double defMovementRate = isCavalry ? 1.5 : 1.0;

        return new UnitTypeStats(
            dto.SupplyConsumptionPerMan ?? fallback?.SupplyConsumptionPerMan ?? defSupply,
            dto.CarryCapacityPerMan     ?? fallback?.CarryCapacityPerMan     ?? defCarry,
            dto.CombatPowerPerMan       ?? fallback?.CombatPowerPerMan       ?? defCombat,
            dto.ScoutingRange           ?? fallback?.ScoutingRange           ?? defScouting,
            dto.MarchingColumnCapacity  ?? fallback?.MarchingColumnCapacity  ?? defColumn,
            dto.CountsForFordingLength  ?? fallback?.CountsForFordingLength  ?? defFording,
            dto.MovementRate            ?? fallback?.MovementRate            ?? defMovementRate);
    }

    private static ShipRules FromShipsDto(ShipsDto? dto, ShipRules defaults)
    {
        if (dto == null) return defaults;

        return new ShipRules(
            TransportInfantryCapacity:    dto.TransportInfantryCapacity    ?? defaults.TransportInfantryCapacity,
            TransportCavalryCapacity:     dto.TransportCavalryCapacity     ?? defaults.TransportCavalryCapacity,
            TransportSupplyCapacity:      dto.TransportSupplyCapacity      ?? defaults.TransportSupplyCapacity,
            TransportWagonCapacity:       dto.TransportWagonCapacity       ?? defaults.TransportWagonCapacity,
            CrewSupplyConsumptionPerShip: dto.CrewSupplyConsumptionPerShip ?? defaults.CrewSupplyConsumptionPerShip,
            SeaOrDownriverHexesPerDay:    dto.SeaOrDownriverHexesPerDay    ?? defaults.SeaOrDownriverHexesPerDay,
            UpriverMovementCostMultiplier:dto.UpriverMovementCostMultiplier?? defaults.UpriverMovementCostMultiplier,
            RowingBonusHexesPerDay:       dto.RowingBonusHexesPerDay       ?? defaults.RowingBonusHexesPerDay,
            ShipTypes: ParseShipTypes(dto.ShipTypes, defaults.ShipTypes));
    }

    private static IReadOnlyDictionary<ShipType, ShipTypeStats> ParseShipTypes(
        Dictionary<string, ShipTypeStatsDto>? dtoDict,
        IReadOnlyDictionary<ShipType, ShipTypeStats> defaults)
    {
        if (dtoDict == null || dtoDict.Count == 0)
            return defaults;

        var result = new Dictionary<ShipType, ShipTypeStats>(defaults);

        foreach (var (key, dto) in dtoDict)
        {
            if (!Enum.TryParse<ShipType>(key, ignoreCase: true, out var shipType))
            {
                System.Diagnostics.Debug.WriteLine($"[GameRulesService] Unknown ShipType key '{key}' in shipTypes — skipping.");
                continue;
            }

            var fallback = defaults.TryGetValue(shipType, out var fb) ? fb : null;
            result[shipType] = new ShipTypeStats(dto.CapacityMultiplier ?? fallback?.CapacityMultiplier ?? 1.0);
        }

        return new ReadOnlyDictionary<ShipType, ShipTypeStats>(result);
    }

    // DTO classes for deserialization (nullable fields → graceful partial JSON)
    private class GameRulesDto
    {
        public MovementDto? Movement { get; set; }
        public MovementRatesDto? MovementRates { get; set; }
        public SupplyDto? Supply { get; set; }
        public ArmiesDto? Armies { get; set; }
        public Dictionary<string, UnitTypeStatsDto>? UnitStats { get; set; }
        public NewsDto? News { get; set; }
        public WeatherDto? Weather { get; set; }
        public ShipsDto? Ships { get; set; }
    }

    private class WeatherDto
    {
        public int? DailyUpdateHour { get; set; }
        public Dictionary<string, Dictionary<string, double>>? Transitions { get; set; }
    }

    private class NewsDto
    {
        public double? OffRoadHoursPerHex { get; set; }
        public double? RoadHoursPerHex { get; set; }
        public double? WaterHoursPerHex { get; set; }
    }

    private class MovementDto
    {
        public int? RoadCost { get; set; }
        public int? OffRoadCost { get; set; }
        public double? ArmyMovementMultiplier { get; set; }
        public int? MarchDayStartHour { get; set; }
        public int? MarchDayEndHour { get; set; }
        public int? LongColumnThreshold { get; set; }
        public double? LongColumnSpeedCap { get; set; }
        public double? ForcedMarchMultiplier { get; set; }
        public int? RiverFordingCostPerColumnUnit { get; set; }
    }

    private class MovementRatesDto
    {
        public double? ArmyBaseRate { get; set; }
        public double? MessengerBaseRate { get; set; }
        public double? CommanderBaseRate { get; set; }
    }

    private class SupplyDto
    {
        public int? WagonSupplyMultiplier { get; set; }
        public int? WagonCarryCapacity { get; set; }
        public int? ForageMultiplierPerDensity { get; set; }
        public int? DailyUsageHour { get; set; }
    }

    private class ArmiesDto
    {
        public int? DailyReportHour { get; set; }
    }

    private class UnitTypeStatsDto
    {
        public int? SupplyConsumptionPerMan { get; set; }
        public int? CarryCapacityPerMan { get; set; }
        public int? CombatPowerPerMan { get; set; }
        public int? ScoutingRange { get; set; }
        public int? MarchingColumnCapacity { get; set; }
        public bool? CountsForFordingLength { get; set; }
        public double? MovementRate { get; set; }
    }

    private class ShipsDto
    {
        public int? TransportInfantryCapacity { get; set; }
        public int? TransportCavalryCapacity { get; set; }
        public int? TransportSupplyCapacity { get; set; }
        public int? TransportWagonCapacity { get; set; }
        public int? CrewSupplyConsumptionPerShip { get; set; }
        public int? SeaOrDownriverHexesPerDay { get; set; }
        public int? UpriverMovementCostMultiplier { get; set; }
        public int? RowingBonusHexesPerDay { get; set; }
        public Dictionary<string, ShipTypeStatsDto>? ShipTypes { get; set; }
    }

    private class ShipTypeStatsDto
    {
        public double? CapacityMultiplier { get; set; }
    }
}
