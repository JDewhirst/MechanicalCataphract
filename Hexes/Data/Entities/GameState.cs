namespace MechanicalCataphract.Data.Entities;

public class GameState
{
    public int Id { get; set; } = 1; // Singleton - always ID 1

    // Referees (comma-separated discord handles)
    public string? Referees { get; set; }

    // Current world-hour since campaign epoch (hour 0 = epoch start)
    public long CurrentWorldHour { get; set; } = 0;

    // Map dimensions
    public int MapRows { get; set; }
    public int MapColumns { get; set; }
}
