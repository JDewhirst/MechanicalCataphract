using System.Collections.Generic;

namespace MechanicalCataphract.Data.Entities;

public class Commander
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Age { get; set; }
    public string? DiscordHandle { get; set; }
    public ulong? DiscordUserId { get; set; }

    // Traits stored as comma-separated trait IDs
    public string? TraitIds { get; set; }

    // Faction
    public int FactionId { get; set; }
    public Faction? Faction { get; set; }

    // Navigation
    public ICollection<Army> CommandedArmies { get; set; } = new List<Army>();
}
