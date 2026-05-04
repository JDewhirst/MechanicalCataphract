using System.Linq;
using Microsoft.EntityFrameworkCore;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public static class QueryExtensions
{
    // ── Army ──
    public static IQueryable<Army> WithStandardIncludes(this IQueryable<Army> q)
        => q.Include(a => a.Faction).Include(a => a.Commander);

    public static IQueryable<Army> WithDetailIncludes(this IQueryable<Army> q)
        => q.WithStandardIncludes().Include(a => a.MapHex);

    public static IQueryable<Army> WithBrigades(this IQueryable<Army> q)
        => q.Include(a => a.Brigades.OrderBy(b => b.SortOrder).ThenBy(b => b.Id));

    // ── Navy ──
    public static IQueryable<Navy> WithStandardIncludes(this IQueryable<Navy> q)
        => q.Include(n => n.Faction).Include(n => n.Commander).Include(n => n.Ships);

    public static IQueryable<Navy> WithDetailIncludes(this IQueryable<Navy> q)
        => q.WithStandardIncludes().Include(n => n.MapHex);

    // ── Commander ──
    public static IQueryable<Commander> WithStandardIncludes(this IQueryable<Commander> q)
        => q.Include(c => c.Faction).Include(c => c.FollowingArmy);

    // ── Message ──
    public static IQueryable<Message> WithStandardIncludes(this IQueryable<Message> q)
        => q.Include(m => m.SenderCommander).Include(m => m.TargetCommander);

    // ── Order ──
    public static IQueryable<Order> WithStandardIncludes(this IQueryable<Order> q)
        => q.Include(o => o.Commander);

    // ── CoLocationChannel ──
    public static IQueryable<CoLocationChannel> WithStandardIncludes(this IQueryable<CoLocationChannel> q)
        => q.Include(c => c.Commanders).Include(c => c.FollowingArmy).Include(c => c.FollowingHex);
}
