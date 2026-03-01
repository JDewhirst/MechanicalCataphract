namespace MechanicalCataphract.Services.Calendar;

public interface ICalendarService
{
    /// <summary>Converts an absolute world-hour to a full calendar date.</summary>
    CalendarDate GetDate(long worldHour);

    /// <summary>Returns the hour-of-day (0 to HoursPerDay-1) for the given worldHour.</summary>
    int GetHourOfDay(long worldHour);

    /// <summary>Returns the integer day index since the campaign epoch (worldHour / HoursPerDay).</summary>
    long GetAbsoluteDayIndex(long worldHour);

    /// <summary>
    /// Returns true if any worldHour in the range (oldWorldHour, newWorldHour] has
    /// an hour-of-day value equal to targetHour.
    /// </summary>
    bool CrossedHourOfDay(long oldWorldHour, long newWorldHour, int targetHour);

    /// <summary>Returns true if oldWorldHour and newWorldHour are on different calendar days.</summary>
    bool CrossedDayBoundary(long oldWorldHour, long newWorldHour);

    /// <summary>Formats the world-hour as a human-readable fictional date string.</summary>
    string FormatDateTime(long worldHour);
}
