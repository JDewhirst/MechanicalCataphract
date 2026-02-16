using System;

namespace MechanicalCataphract.Data.Entities;

public class GameState
{
    public int Id { get; set; } = 1; // Singleton - always ID 1

    // Referees (comma-separated discord handles)
    public string? Referees { get; set; }

    // Current game time (to hour precision)
    public DateTime CurrentGameTime { get; set; } = DateTime.UtcNow;

    // Scheduled times for automated events
    public TimeSpan SupplyUsageTime { get; set; } = new TimeSpan(21, 0, 0); // 9 PM
    public TimeSpan WeatherUpdateTime { get; set; } = new TimeSpan(0, 0, 0); // Midnight
    public TimeSpan ArmyReportTime { get; set; } = new TimeSpan(6, 0, 0); // 6 AM

    // Map dimensions
    public int MapRows { get; set; }
    public int MapColumns { get; set; }
}
