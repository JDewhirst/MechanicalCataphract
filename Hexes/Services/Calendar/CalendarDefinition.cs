using System.Collections.Generic;

namespace MechanicalCataphract.Services.Calendar;

public record CalendarMonthDefinition(string Name, int Days);

public record CalendarDefinition(
    string Name,
    int HoursPerDay,
    List<string> WeekdayNames,
    List<CalendarMonthDefinition> Months,
    int EpochYear,
    int EpochMonth,
    int EpochDay,
    int EpochWeekday);

public record CalendarDate(
    int Year,
    int MonthNumber,
    string MonthName,
    int DayOfMonth,
    int DayOfYear,
    int WeekdayIndex,
    string WeekdayName,
    int HourOfDay,
    long AbsoluteDayIndex);
