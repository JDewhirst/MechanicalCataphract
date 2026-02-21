using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public interface INewsService
{
    /// <summary>
    /// Creates a new event at the given origin hex. Runs a Dijkstra flood-fill across
    /// the map (roads/rivers = half cost) and stores all hex arrival times as JSON.
    /// </summary>
    Task<NewsItem> CreateEventAsync(
        string title,
        int originQ, int originR,
        DateTime createdAtGameTime,
        Dictionary<int, string> factionMessages);

    Task<IList<NewsItem>> GetAllActiveAsync();

    /// <summary>Persists changes to Title and/or FactionMessages on an existing item.</summary>
    Task UpdateAsync(NewsItem item);

    /// <summary>
    /// Checks all active events, delivers Discord messages to commanders whose hex
    /// the wavefront has now reached, and records deliveries.
    /// Returns the total number of deliveries made.
    /// </summary>
    Task<int> ProcessEventDeliveriesAsync(DateTime currentGameTime);

    Task DeactivateEventAsync(int eventId);
    Task DeleteEventAsync(int eventId);

    /// <summary>Returns ALL news items (active + inactive), newest first.</summary>
    Task<IList<NewsItem>> GetAllAsync() =>
        Task.FromResult<IList<NewsItem>>(Array.Empty<NewsItem>());

    /// <summary>Reverses DeactivateEventAsync.</summary>
    Task ReactivateEventAsync(int eventId) => Task.CompletedTask;
}
