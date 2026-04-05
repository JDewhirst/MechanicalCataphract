using System;
using System.Linq;

namespace MechanicalCataphract.Services.Calendar;

public class CalendarService : ICalendarService
{
    private readonly CalendarDefinition _cal;
    private readonly int _daysPerYear;
    private readonly int _epochDayOfYear; // 0-indexed day within year at worldHour=0

    public CalendarService(ICalendarDefinitionService calendarDefinitionService)
    {
        _cal = calendarDefinitionService.GetCalendarDefinition();
        _daysPerYear = _cal.Months.Sum(m => m.Days);

        // Days elapsed within the epoch year before the epoch day (0-indexed)
        _epochDayOfYear = 0;
        for (int i = 0; i < _cal.EpochMonth - 1; i++)
            _epochDayOfYear += _cal.Months[i].Days;
        _epochDayOfYear += _cal.EpochDay - 1;
    }

    public int GetHourOfDay(long worldHour) => (int)FloorMod(worldHour, _cal.HoursPerDay);

    public long GetAbsoluteDayIndex(long worldHour) => FloorDiv(worldHour, _cal.HoursPerDay);

    public CalendarDate GetDate(long worldHour)
    {
        int H = _cal.HoursPerDay;
        long absoluteDayIndex = FloorDiv(worldHour, H);
        int hourOfDay = (int)FloorMod(worldHour, H);

        int W = _cal.WeekdayNames.Count;
        int weekdayIndex = (int)(((_cal.EpochWeekday + absoluteDayIndex) % W + W) % W);

        // Total elapsed days from the start of epochYear
        long totalDaysFromYearStart = _epochDayOfYear + absoluteDayIndex;

        long yearOffset = FloorDiv(totalDaysFromYearStart, _daysPerYear);
        int dayOfYearZeroBased = (int)FloorMod(totalDaysFromYearStart, _daysPerYear);

        int year = (int)(_cal.EpochYear + yearOffset);

        // Find month and day-of-month from dayOfYearZeroBased
        int monthNumber = 1;
        string monthName = _cal.Months[0].Name;
        int dayOfMonth = dayOfYearZeroBased + 1;

        int remaining = dayOfYearZeroBased;
        for (int i = 0; i < _cal.Months.Count; i++)
        {
            if (remaining < _cal.Months[i].Days)
            {
                monthNumber = i + 1;
                monthName = _cal.Months[i].Name;
                dayOfMonth = remaining + 1;
                break;
            }
            remaining -= _cal.Months[i].Days;
        }

        return new CalendarDate(
            Year: year,
            MonthNumber: monthNumber,
            MonthName: monthName,
            DayOfMonth: dayOfMonth,
            DayOfYear: dayOfYearZeroBased + 1,
            WeekdayIndex: weekdayIndex,
            WeekdayName: _cal.WeekdayNames[weekdayIndex],
            HourOfDay: hourOfDay,
            AbsoluteDayIndex: absoluteDayIndex);
    }

    public bool CrossedHourOfDay(long oldWorldHour, long newWorldHour, int targetHour)
    {
        if (newWorldHour <= oldWorldHour) return false;

        int H = _cal.HoursPerDay;

        // Range spans at least a full day — guaranteed to hit every hour-of-day
        if (newWorldHour - oldWorldHour >= H) return true;

        // Find the first worldHour >= oldWorldHour+1 with (worldHour % H) == targetHour
        long adjustedOld = oldWorldHour + 1;
        long dayIndex = adjustedOld / H;
        long candidate = dayIndex * H + targetHour;
        if (candidate < adjustedOld)
            candidate += H;

        return candidate <= newWorldHour;
    }

    public bool CrossedDayBoundary(long oldWorldHour, long newWorldHour)
        => newWorldHour / _cal.HoursPerDay > oldWorldHour / _cal.HoursPerDay;

    public string FormatDateTime(long worldHour)
    {
        var d = GetDate(worldHour);
        return $"Year {d.Year}, {d.MonthName} {d.DayOfMonth}, {d.WeekdayName}, {d.HourOfDay:D2}:00";
    }

    private static long FloorDiv(long a, long b) => a / b - (a % b < 0 ? 1 : 0);
    private static long FloorMod(long a, long b) => ((a % b) + b) % b;

    public long GetWorldHour(int year, int monthNumber, int dayOfMonth, int hourOfDay)
    {
        long yearOffset = year - _cal.EpochYear;
        long totalDaysFromYearStart = yearOffset * _daysPerYear;

        for (int i = 0; i < monthNumber - 1; i++)
            totalDaysFromYearStart += _cal.Months[i].Days;

        totalDaysFromYearStart += (dayOfMonth - 1);

        long absoluteDayIndex = totalDaysFromYearStart - _epochDayOfYear;

        return absoluteDayIndex * _cal.HoursPerDay + hourOfDay;
    }
}
