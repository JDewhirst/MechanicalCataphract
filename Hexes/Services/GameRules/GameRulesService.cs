using System;
using System.IO;
using System.Text.Json;

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

    public static GameRulesData CreateDefaults() => new(
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
            ForageMultiplierPerDensity: 500),
        UnitStats: new UnitStatsRules(
            Infantry: new UnitTypeStats(1, 15, 1, 1, 5000, true),
            Skirmishers: new UnitTypeStats(1, 15, 1, 1, 5000, true),
            Cavalry: new UnitTypeStats(10, 75, 2, 2, 2000, false)),
        News: new NewsRules(OffRoadHoursPerHex: 24.0, RoadHoursPerHex: 12.0),
        Weather: new WeatherRules(
            DailyUpdateHour: 6,
            Transitions: new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, double>>
            {
                ["Clear"]    = new() { ["Clear"]=0.40, ["Overcast"]=0.25, ["Rain"]=0.20, ["Fog"]=0.15 },
                ["Overcast"] = new() { ["Overcast"]=0.40, ["Rain"]=0.25, ["Fog"]=0.20, ["Clear"]=0.15 },
                ["Rain"]     = new() { ["Fog"]=0.40, ["Clear"]=0.25, ["Overcast"]=0.20, ["Rain"]=0.15 },
                ["Fog"]      = new() { ["Fog"]=0.40, ["Clear"]=0.25, ["Overcast"]=0.20, ["Rain"]=0.15 }
            }));

    private static GameRulesData FromDto(GameRulesDto dto)
    {
        var m = dto.Movement ?? new MovementDto();
        var mr = dto.MovementRates ?? new MovementRatesDto();
        var s = dto.Supply ?? new SupplyDto();
        var us = dto.UnitStats ?? new UnitStatsDto();
        var n = dto.News ?? new NewsDto();
        var w = dto.Weather;

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
                ForageMultiplierPerDensity: s.ForageMultiplierPerDensity ?? 500),
            UnitStats: new UnitStatsRules(
                Infantry: FromUnitDto(us.Infantry),
                Skirmishers: FromUnitDto(us.Skirmishers),
                Cavalry: FromCavalryDto(us.Cavalry)),
            News: new NewsRules(
                OffRoadHoursPerHex: n.OffRoadHoursPerHex ?? 24.0,
                RoadHoursPerHex: n.RoadHoursPerHex ?? 12.0),
            Weather: new WeatherRules(
                DailyUpdateHour: w?.DailyUpdateHour ?? defaults.Weather.DailyUpdateHour,
                Transitions: w?.Transitions ?? defaults.Weather.Transitions));
    }

    private static UnitTypeStats FromUnitDto(UnitTypeStatsDto? dto) => dto == null
        ? new UnitTypeStats(1, 15, 1, 1, 5000, true)
        : new UnitTypeStats(
            dto.SupplyConsumptionPerMan ?? 1,
            dto.CarryCapacityPerMan ?? 15,
            dto.CombatPowerPerMan ?? 1,
            dto.ScoutingRange ?? 1,
            dto.MarchingColumnCapacity ?? 5000,
            dto.CountsForFordingLength ?? true);

    private static UnitTypeStats FromCavalryDto(UnitTypeStatsDto? dto) => dto == null
        ? new UnitTypeStats(10, 75, 2, 2, 2000, false)
        : new UnitTypeStats(
            dto.SupplyConsumptionPerMan ?? 10,
            dto.CarryCapacityPerMan ?? 75,
            dto.CombatPowerPerMan ?? 2,
            dto.ScoutingRange ?? 2,
            dto.MarchingColumnCapacity ?? 2000,
            dto.CountsForFordingLength ?? false);

    // DTO classes for deserialization (nullable fields → graceful partial JSON)
    private class GameRulesDto
    {
        public MovementDto? Movement { get; set; }
        public MovementRatesDto? MovementRates { get; set; }
        public SupplyDto? Supply { get; set; }
        public UnitStatsDto? UnitStats { get; set; }
        public NewsDto? News { get; set; }
        public WeatherDto? Weather { get; set; }
    }

    private class WeatherDto
    {
        public int? DailyUpdateHour { get; set; }
        public System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, double>>? Transitions { get; set; }
    }

    private class NewsDto
    {
        public double? OffRoadHoursPerHex { get; set; }
        public double? RoadHoursPerHex { get; set; }
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
        public int? ForageMultiplierPerDensity { get; set; }
    }

    private class UnitStatsDto
    {
        public UnitTypeStatsDto? Infantry { get; set; }
        public UnitTypeStatsDto? Skirmishers { get; set; }
        public UnitTypeStatsDto? Cavalry { get; set; }
    }

    private class UnitTypeStatsDto
    {
        public int? SupplyConsumptionPerMan { get; set; }
        public int? CarryCapacityPerMan { get; set; }
        public int? CombatPowerPerMan { get; set; }
        public int? ScoutingRange { get; set; }
        public int? MarchingColumnCapacity { get; set; }
        public bool? CountsForFordingLength { get; set; }
    }
}
