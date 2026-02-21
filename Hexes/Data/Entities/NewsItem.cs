using System;
using System.Collections.Generic;

namespace MechanicalCataphract.Data.Entities;

/// <summary>
/// Pre-computed Dijkstra arrival data for one hex in an event's flood-fill.
/// Stored as a JSON-serialized list on NewsItem — not a separate EF entity.
/// </summary>
public record HexArrivalData(int Q, int R, double Hours);

/// <summary>
/// A referee-dropped event (news/rumour) that spreads outward from an origin hex.
/// Faction-specific messages are sent to commanders when the wavefront reaches their hex.
/// </summary>
public class NewsItem
{
    public int Id { get; set; }

    /// <summary>Origin hex coordinates (plain ints, no FK to MapHex).</summary>
    public int OriginQ { get; set; }
    public int OriginR { get; set; }

    /// <summary>Game-clock time when the event was dropped.</summary>
    public DateTime CreatedAtGameTime { get; set; }

    /// <summary>Referee can archive/stop an event to prevent further deliveries.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Short referee-given name displayed in the sidebar list.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// factionId → message text. Factions with no entry receive no notification.
    /// Stored as JSON column.
    /// </summary>
    public Dictionary<int, string>? FactionMessages { get; set; }

    /// <summary>
    /// Pre-computed Dijkstra flood-fill results: every reachable hex with its arrival
    /// time in game-hours from the origin. Stored as JSON column.
    /// </summary>
    public List<HexArrivalData>? HexArrivals { get; set; }

    /// <summary>
    /// Ids of commanders who have already received their notification.
    /// Prevents re-delivery. Stored as JSON column.
    /// </summary>
    public List<int>? DeliveredCommanderIds { get; set; }
}
