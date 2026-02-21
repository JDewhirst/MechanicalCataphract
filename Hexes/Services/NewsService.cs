using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hexes;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Discord;
using Microsoft.EntityFrameworkCore;

namespace MechanicalCataphract.Services;

public class NewsService(
    WargameDbContext context,
    IMapService mapService,
    IDiscordChannelManager discordChannelManager) : INewsService
{
    public async Task<NewsItem> CreateEventAsync(
        string title,
        int originQ, int originR,
        DateTime createdAtGameTime,
        Dictionary<int, string> factionMessages)
    {
        // Load all hexes and build adjacency lookup
        var allHexes = await mapService.GetAllHexesAsync();
        var hexLookup = allHexes.ToDictionary(h => (h.Q, h.R));

        // Dijkstra flood-fill from origin
        var dist = new Dictionary<(int q, int r), double>();
        var pq = new PriorityQueue<(int q, int r), double>();

        var origin = (originQ, originR);
        dist[origin] = 0.0;
        pq.Enqueue(origin, 0.0);

        while (pq.Count > 0)
        {
            var current = pq.Dequeue();
            double currentDist = dist[current];

            if (!hexLookup.TryGetValue(current, out var currentHex))
                continue;

            for (int dir = 0; dir < 6; dir++)
            {
                var hex = new Hex(current.Item1, current.Item2, -current.Item1 - current.Item2);
                var neighborHex = hex.Neighbor(dir);
                var neighborKey = (neighborHex.q, neighborHex.r);

                if (!hexLookup.ContainsKey(neighborKey))
                    continue;

                // Roads halve movement cost
                double edgeCost = currentHex.HasRoadInDirection(dir)
                    ? GameRules.Current.News.RoadHoursPerHex
                    : GameRules.Current.News.OffRoadHoursPerHex;
                double newDist = currentDist + edgeCost;

                if (!dist.TryGetValue(neighborKey, out double existingDist) || newDist < existingDist)
                {
                    dist[neighborKey] = newDist;
                    pq.Enqueue(neighborKey, newDist);
                }
            }
        }

        var newsItem = new NewsItem
        {
            Title = title,
            OriginQ = originQ,
            OriginR = originR,
            CreatedAtGameTime = createdAtGameTime,
            IsActive = true,
            FactionMessages = factionMessages,
            HexArrivals = dist.Select(kv => new HexArrivalData(kv.Key.q, kv.Key.r, kv.Value)).ToList(),
            DeliveredCommanderIds = new List<int>()
        };

        context.NewsItems.Add(newsItem);
        await context.SaveChangesAsync();
        return newsItem;
    }

    public async Task<IList<NewsItem>> GetAllActiveAsync()
    {
        return await context.NewsItems
            .Where(e => e.IsActive)
            .OrderByDescending(e => e.CreatedAtGameTime)
            .ToListAsync();
    }

    public async Task<int> ProcessEventDeliveriesAsync(DateTime currentGameTime)
    {
        var activeEvents = await context.NewsItems
            .Where(e => e.IsActive)
            .ToListAsync();

        int totalDeliveries = 0;

        foreach (var newsItem in activeEvents)
        {
            if (newsItem.HexArrivals == null || newsItem.FactionMessages == null)
                continue;

            double elapsedHours = (currentGameTime - newsItem.CreatedAtGameTime).TotalHours;
            if (elapsedHours < 0) continue;

            var reachedCoords = newsItem.HexArrivals
                .Where(a => a.Hours <= elapsedHours)
                .Select(a => (a.Q, a.R))
                .ToHashSet();

            var delivered = newsItem.DeliveredCommanderIds ?? new List<int>();

            // Find commanders in reached hexes
            var commanders = await context.Commanders
                .Include(c => c.Faction)
                .Where(c => c.CoordinateQ.HasValue && c.CoordinateR.HasValue)
                .ToListAsync();

            var newDeliveries = new List<int>();

            foreach (var commander in commanders)
            {
                if (delivered.Contains(commander.Id)) continue;

                var coordKey = (commander.CoordinateQ!.Value, commander.CoordinateR!.Value);
                if (!reachedCoords.Contains(coordKey)) continue;

                if (!newsItem.FactionMessages.TryGetValue(commander.FactionId, out var message))
                    continue;

                await discordChannelManager.SendMessageToCommanderChannelAsync(commander, message);
                newDeliveries.Add(commander.Id);
                totalDeliveries++;
            }

            if (newDeliveries.Count > 0)
            {
                newsItem.DeliveredCommanderIds = delivered.Concat(newDeliveries).ToList();
                context.NewsItems.Update(newsItem);
            }
        }

        if (totalDeliveries > 0)
            await context.SaveChangesAsync();

        return totalDeliveries;
    }

    public async Task UpdateAsync(NewsItem item)
    {
        context.NewsItems.Update(item);
        await context.SaveChangesAsync();
    }

    public async Task<IList<NewsItem>> GetAllAsync()
    {
        return await context.NewsItems
            .OrderByDescending(e => e.CreatedAtGameTime)
            .ToListAsync();
    }

    public async Task ReactivateEventAsync(int eventId)
    {
        var newsItem = await context.NewsItems.FindAsync(eventId);
        if (newsItem == null) return;
        newsItem.IsActive = true;
        await context.SaveChangesAsync();
    }

    public async Task DeactivateEventAsync(int eventId)
    {
        var newsItem = await context.NewsItems.FindAsync(eventId);
        if (newsItem == null) return;
        newsItem.IsActive = false;
        await context.SaveChangesAsync();
    }

    public async Task DeleteEventAsync(int eventId)
    {
        var newsItem = await context.NewsItems.FindAsync(eventId);
        if (newsItem == null) return;
        context.NewsItems.Remove(newsItem);
        await context.SaveChangesAsync();
    }
}
