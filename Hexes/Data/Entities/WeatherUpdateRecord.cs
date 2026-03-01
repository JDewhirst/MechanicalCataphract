namespace MechanicalCataphract.Data.Entities;

public class WeatherUpdateRecord
{
    public int Id { get; set; }

    /// <summary>
    /// The absolute day index (worldHour / hoursPerDay) for which weather was updated.
    /// Used for idempotency gating — one weather update per calendar day.
    /// </summary>
    public long AbsoluteDayIndex { get; set; }
}
