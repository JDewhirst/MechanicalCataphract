using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MechanicalCataphract.Services.Calendar;

public class CalendarDefinitionService : ICalendarDefinitionService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly CalendarDefinition _definition;

    public CalendarDefinitionService()
    {
        _definition = Load();
    }

    public CalendarDefinition GetCalendarDefinition() => _definition;

    private static CalendarDefinition Load()
    {
        var assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");
        var primaryPath = Path.Combine(assetsDir, "calendar_rules.json");
        var defaultPath = Path.Combine(assetsDir, "calendar_rules_default.json");

        string? json = null;

        if (File.Exists(primaryPath))
        {
            json = File.ReadAllText(primaryPath);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine(
                "[CalendarDefinitionService] calendar_rules.json not found — using bundled default.");
            if (File.Exists(defaultPath))
                json = File.ReadAllText(defaultPath);
        }

        if (json == null)
        {
            System.Diagnostics.Debug.WriteLine(
                "[CalendarDefinitionService] No calendar JSON found — using hardcoded default.");
            return CreateHardcodedDefault();
        }

        CalendarDefinitionDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<CalendarDefinitionDto>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            FatalError($"calendar_rules.json is malformed JSON: {ex.Message}");
            return null!; // unreachable
        }

        if (dto == null)
        {
            FatalError("calendar_rules.json could not be parsed (deserialized to null).");
            return null!;
        }

        var (isValid, errorMessage) = Validate(dto);
        if (!isValid)
        {
            FatalError($"calendar_rules.json validation failed: {errorMessage}");
            return null!;
        }

        return FromDto(dto);
    }

    private static void FatalError(string message)
    {
        var fullMessage = $"[CalendarDefinitionService] FATAL: {message}";
        Console.Error.WriteLine(fullMessage);
        System.Diagnostics.Debug.WriteLine(fullMessage);
        Environment.Exit(1);
    }

    private static (bool isValid, string error) Validate(CalendarDefinitionDto dto)
    {
        if (dto.HoursPerDay < 1)
            return (false, $"hoursPerDay must be >= 1 (got {dto.HoursPerDay})");

        if (dto.WeekdayNames == null || dto.WeekdayNames.Count < 1)
            return (false, "weekdayNames must have at least 1 entry");

        for (int i = 0; i < dto.WeekdayNames.Count; i++)
            if (string.IsNullOrWhiteSpace(dto.WeekdayNames[i]))
                return (false, $"weekdayNames[{i}] is empty");

        if (dto.Months == null || dto.Months.Count < 1)
            return (false, "months must have at least 1 entry");

        for (int i = 0; i < dto.Months.Count; i++)
        {
            var m = dto.Months[i];
            if (string.IsNullOrWhiteSpace(m.Name))
                return (false, $"months[{i}].name is empty");
            if (m.Days < 1)
                return (false, $"months[{i}].days must be >= 1 (got {m.Days})");
        }

        if (dto.EpochMonth < 1 || dto.EpochMonth > dto.Months.Count)
            return (false, $"epochMonth {dto.EpochMonth} out of range [1, {dto.Months.Count}]");

        int maxDay = dto.Months[dto.EpochMonth - 1].Days;
        if (dto.EpochDay < 1 || dto.EpochDay > maxDay)
            return (false, $"epochDay {dto.EpochDay} out of range [1, {maxDay}]");

        if (dto.EpochWeekday < 0 || dto.EpochWeekday >= dto.WeekdayNames.Count)
            return (false, $"epochWeekday {dto.EpochWeekday} out of range [0, {dto.WeekdayNames.Count - 1}]");

        return (true, string.Empty);
    }

    private static CalendarDefinition FromDto(CalendarDefinitionDto dto)
    {
        var months = new List<CalendarMonthDefinition>(dto.Months!.Count);
        foreach (var m in dto.Months!)
            months.Add(new CalendarMonthDefinition(m.Name!, m.Days));

        return new CalendarDefinition(
            Name: dto.Name ?? "Unknown Calendar",
            HoursPerDay: dto.HoursPerDay,
            WeekdayNames: new List<string>(dto.WeekdayNames!),
            Months: months,
            EpochYear: dto.EpochYear,
            EpochMonth: dto.EpochMonth,
            EpochDay: dto.EpochDay,
            EpochWeekday: dto.EpochWeekday);
    }

    public static CalendarDefinition CreateHardcodedDefault() => new(
        Name: "Imperial Reckoning",
        HoursPerDay: 24,
        WeekdayNames: new List<string> { "Firstday", "Secondday", "Thirdday", "Fourthday", "Fifthday", "Sixthday", "Seventhday" },
        Months: new List<CalendarMonthDefinition>
        {
            new("Dawnmarch", 30), new("Rainfall", 30), new("Highsun", 30),
            new("Harvest", 30), new("Frostwane", 30), new("Yearsend", 30)
        },
        EpochYear: 1000,
        EpochMonth: 1,
        EpochDay: 1,
        EpochWeekday: 0);

    // DTO for JSON deserialization
    private class CalendarDefinitionDto
    {
        public string? Name { get; set; }
        public int HoursPerDay { get; set; }
        public List<string>? WeekdayNames { get; set; }
        public List<MonthDto>? Months { get; set; }
        public int EpochYear { get; set; }
        public int EpochMonth { get; set; }
        public int EpochDay { get; set; }
        public int EpochWeekday { get; set; }
    }

    private class MonthDto
    {
        public string? Name { get; set; }
        public int Days { get; set; }
    }
}
