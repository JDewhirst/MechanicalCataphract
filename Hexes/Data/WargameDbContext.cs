using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MechanicalCataphract.Data.Entities;
using Hexes;

namespace MechanicalCataphract.Data;

public class WargameDbContext : DbContext
{
    public DbSet<MapHex> MapHexes { get; set; }
    public DbSet<TerrainType> TerrainTypes { get; set; }
    public DbSet<Weather> WeatherTypes { get; set; }
    public DbSet<Faction> Factions { get; set; }
    public DbSet<Army> Armies { get; set; }
    public DbSet<Brigade> Brigades { get; set; }
    public DbSet<Commander> Commanders { get; set; }
    public DbSet<LocationType> LocationTypes { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<GameState> GameStates { get; set; }
    public DbSet<DiscordConfig> DiscordConfigs { get; set; }
    public DbSet<CoLocationChannel> CoLocationChannels { get; set; }
    public DbSet<WeatherUpdateRecord> WeatherUpdateRecords { get; set; }
    public DbSet<FactionRule> FactionRules { get; set; }
    public DbSet<NewsItem> NewsItems { get; set; }
    public DbSet<Navy> Navies { get; set; }
    public DbSet<Ship> Ships { get; set; }

    public WargameDbContext(DbContextOptions<WargameDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // MapHex composite key
        modelBuilder.Entity<MapHex>()
            .HasKey(h => new { h.Q, h.R });

        // MapHex -> TerrainType
        modelBuilder.Entity<MapHex>()
            .HasOne(h => h.TerrainType)
            .WithMany(t => t.Hexes)
            .HasForeignKey(h => h.TerrainTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        // MapHex -> ControllingFaction
        modelBuilder.Entity<MapHex>()
            .HasOne(h => h.ControllingFaction)
            .WithMany(f => f.ControlledHexes)
            .HasForeignKey(h => h.ControllingFactionId)
            .OnDelete(DeleteBehavior.SetNull);

        // MapHex -> Weather
        modelBuilder.Entity<MapHex>()
            .HasOne(h => h.Weather)
            .WithMany(w => w.AffectedHexes)
            .HasForeignKey(h => h.WeatherId)
            .OnDelete(DeleteBehavior.SetNull);

        // Army -> MapHex (coordinate)
        modelBuilder.Entity<Army>()
            .HasOne(a => a.MapHex)
            .WithMany(h => h.Armies)
            .HasForeignKey(a => new { a.CoordinateQ, a.CoordinateR })
            .OnDelete(DeleteBehavior.Restrict);

        // Army -> Faction
        modelBuilder.Entity<Army>()
            .HasOne(a => a.Faction)
            .WithMany(f => f.Armies)
            .HasForeignKey(a => a.FactionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Army -> Commander
        modelBuilder.Entity<Army>()
            .HasOne(a => a.Commander)
            .WithMany(c => c.CommandedArmies)
            .HasForeignKey(a => a.CommanderId)
            .OnDelete(DeleteBehavior.SetNull);

        // Brigade -> Army
        modelBuilder.Entity<Brigade>()
            .HasOne(b => b.Army)
            .WithMany(a => a.Brigades)
            .HasForeignKey(b => b.ArmyId)
            .OnDelete(DeleteBehavior.Cascade);

        // ScoutingRange is computed from UnitType, not stored
        modelBuilder.Entity<Brigade>()
            .Ignore(b => b.ScoutingRange);

        // Commander -> Faction
        modelBuilder.Entity<Commander>()
            .HasOne(c => c.Faction)
            .WithMany(f => f.Commanders)
            .HasForeignKey(c => c.FactionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Commander -> FollowingArmy (physical location coupling)
        modelBuilder.Entity<Commander>()
            .HasOne(c => c.FollowingArmy)
            .WithMany()
            .HasForeignKey(c => c.FollowingArmyId)
            .OnDelete(DeleteBehavior.SetNull);

        // Commander -> MapHex (coordinate)
        modelBuilder.Entity<Commander>()
            .HasOne(c => c.MapHex)
            .WithMany(h => h.Commanders)
            .HasForeignKey(c => new { c.CoordinateQ, c.CoordinateR })
            .OnDelete(DeleteBehavior.SetNull);

        // CoLocationChannel -> Army (following)
        modelBuilder.Entity<CoLocationChannel>()
            .HasOne(c => c.FollowingArmy)
            .WithMany()
            .HasForeignKey(c => c.FollowingArmyId)
            .OnDelete(DeleteBehavior.SetNull);

        // CoLocationChannel -> MapHex (following hex)
        modelBuilder.Entity<CoLocationChannel>()
            .HasOne(c => c.FollowingHex)
            .WithMany()
            .HasForeignKey(c => new { c.FollowingHexQ, c.FollowingHexR })
            .OnDelete(DeleteBehavior.SetNull);

        // CoLocationChannel <-> Commander (many-to-many)
        modelBuilder.Entity<CoLocationChannel>()
            .HasMany(c => c.Commanders)
            .WithMany(c => c.CoLocationChannels)
            .UsingEntity("CoLocationChannelCommander");

        // MapHex -> LocationFaction (for embedded location)
        modelBuilder.Entity<MapHex>()
            .HasOne(h => h.LocationFaction)
            .WithMany()
            .HasForeignKey(h => h.LocationFactionId)
            .OnDelete(DeleteBehavior.SetNull);

        // MapHex -> LocationType
        modelBuilder.Entity<MapHex>()
            .HasOne(h => h.LocationType)
            .WithMany()
            .HasForeignKey(h => h.LocationTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        // FactionRule -> Faction (cascade delete: removing a faction removes its rules)
        modelBuilder.Entity<FactionRule>()
            .HasOne(r => r.Faction)
            .WithMany()
            .HasForeignKey(r => r.FactionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Order -> Commander
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Commander)
            .WithMany()
            .HasForeignKey(o => o.CommanderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Message -> MapHex (coordinate)
        modelBuilder.Entity<Message>()
            .HasOne(a => a.MapHex)
            .WithMany(h => h.Messages)
            .HasForeignKey(a => new { a.CoordinateQ, a.CoordinateR })
            .OnDelete(DeleteBehavior.Restrict);

        // Message.Path -> JSON serialization for List<Hex>
        // Uses custom converter because Hex struct has readonly fields
        var hexJsonOptions = new JsonSerializerOptions();
        hexJsonOptions.Converters.Add(new HexJsonConverter());

        // Value comparer for List<Hex> - required for EF Core to detect changes
        var hexListComparer = new ValueComparer<List<Hex>?>(
            (c1, c2) => c1 == null && c2 == null || c1 != null && c2 != null && c1.SequenceEqual(c2),
            c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.q, v.r, v.s)),
            c => c == null ? null : c.ToList());

        modelBuilder.Entity<Message>()
            .Property(m => m.Path)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, hexJsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<List<Hex>>(v, hexJsonOptions))
            .Metadata.SetValueComparer(hexListComparer);

        // Army.Path -> JSON serialization for List<Hex>
        modelBuilder.Entity<Army>()
            .Property(a => a.Path)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, hexJsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<List<Hex>>(v, hexJsonOptions))
            .Metadata.SetValueComparer(hexListComparer);

        // Commander.Path -> JSON serialization for List<Hex>
        modelBuilder.Entity<Commander>()
            .Property(c => c.Path)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, hexJsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<List<Hex>>(v, hexJsonOptions))
            .Metadata.SetValueComparer(hexListComparer);

        // NewsItem JSON columns
        var jsonOptions = new JsonSerializerOptions();

        var dictComparer = new ValueComparer<Dictionary<int, string>?>(
            (a, b) => a == null && b == null || a != null && b != null && JsonSerializer.Serialize(a, jsonOptions) == JsonSerializer.Serialize(b, jsonOptions),
            c => c == null ? 0 : JsonSerializer.Serialize(c, jsonOptions).GetHashCode(),
            c => c == null ? null : new Dictionary<int, string>(c));

        modelBuilder.Entity<NewsItem>()
            .Property(e => e.FactionMessages)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, jsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<Dictionary<int, string>>(v, jsonOptions))
            .Metadata.SetValueComparer(dictComparer);

        var hexArrivalListComparer = new ValueComparer<List<HexArrivalData>?>(
            (a, b) => a == null && b == null || a != null && b != null && a.Count == b.Count,
            c => c == null ? 0 : c.Count,
            c => c == null ? null : c.ToList());

        modelBuilder.Entity<NewsItem>()
            .Property(e => e.HexArrivals)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, jsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<List<HexArrivalData>>(v, jsonOptions))
            .Metadata.SetValueComparer(hexArrivalListComparer);

        var intListComparer = new ValueComparer<List<int>?>(
            (a, b) => a == null && b == null || a != null && b != null && a.SequenceEqual(b),
            c => c == null ? 0 : c.Aggregate(0, HashCode.Combine),
            c => c == null ? null : c.ToList());

        modelBuilder.Entity<NewsItem>()
            .Property(e => e.DeliveredCommanderIds)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, jsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<List<int>>(v, jsonOptions))
            .Metadata.SetValueComparer(intListComparer);

        // Navy -> MapHex (coordinate, Restrict on delete)
        modelBuilder.Entity<Navy>()
            .HasOne(n => n.MapHex)
            .WithMany()
            .HasForeignKey(n => new { n.CoordinateQ, n.CoordinateR })
            .OnDelete(DeleteBehavior.Restrict);

        // Navy -> Commander (SetNull on delete)
        modelBuilder.Entity<Navy>()
            .HasOne(n => n.Commander)
            .WithMany()
            .HasForeignKey(n => n.CommanderId)
            .OnDelete(DeleteBehavior.SetNull);

        // Ship -> Navy (cascade delete)
        modelBuilder.Entity<Ship>()
            .HasOne(s => s.Navy)
            .WithMany(n => n.Ships)
            .HasForeignKey(s => s.NavyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Army -> Navy (SetNull on delete; CarriedArmy back-reference)
        modelBuilder.Entity<Army>()
            .HasOne(a => a.Navy)
            .WithOne(n => n.CarriedArmy)
            .HasForeignKey<Army>(a => a.NavyId)
            .OnDelete(DeleteBehavior.SetNull);

        // Computed properties on Navy not stored in DB
        modelBuilder.Entity<Navy>()
            .Ignore(n => n.TransportCount)
            .Ignore(n => n.WarshipCount)
            .Ignore(n => n.DailySupplyConsumption)
            .Ignore(n => n.DaysOfSupply)
            .Ignore(n => n.MaxCarryUnits)
            .Ignore(n => n.TotalCarryUnits);

        // Computed properties on Army not stored in DB
        modelBuilder.Entity<Army>()
            .Ignore(a => a.IsEmbarked);

        // Seed default faction
        modelBuilder.Entity<Faction>().HasData(
            new Faction { Id = 1, Name = "No Faction", ColorHex = "#808080" }
            );

        // Terrain types are loaded from Assets/classic-icons/classic.properties at startup
        // See App.axaml.cs OnFrameworkInitializationCompleted()

        // Seed default weather types
        modelBuilder.Entity<Weather>().HasData(
            new Weather { Id = 1, Name = "Clear",   IconPath = "avares://MechanicalCataphract/Assets/weather-icons/clear-day.svg", MovementModifier = 1.0, CombatModifier = 1.0 },
            new Weather { Id = 2, Name = "Rain",    IconPath = "avares://MechanicalCataphract/Assets/weather-icons/rain.svg",      MovementModifier = 0.75, CombatModifier = 0.9 },
            new Weather { Id = 3, Name = "Storm",   IconPath = "",                                                                  MovementModifier = 0.5, CombatModifier = 0.7 },
            new Weather { Id = 4, Name = "Snow",    IconPath = "",                                                                  MovementModifier = 0.5, CombatModifier = 0.8 },
            new Weather { Id = 5, Name = "Fog",     IconPath = "avares://MechanicalCataphract/Assets/weather-icons/fog.svg",       MovementModifier = 0.75, CombatModifier = 0.8 },
            new Weather { Id = 6, Name = "Overcast",IconPath = "avares://MechanicalCataphract/Assets/weather-icons/overcast.svg",  MovementModifier = 0.9, CombatModifier = 0.95 }
        );

        // Seed default location types (Id=1 is the "No Location" sentinel)
        modelBuilder.Entity<LocationType>().HasData(
            new LocationType { Id = 1, Name = "No Location", ColorHex = "#808080" },
            new LocationType { Id = 2, Name = "Fortress", ColorHex = "#8B0000", IconPath = "avares://MechanicalCataphract/Assets/location-icons/castle.png", ScaleFactor = 0.64 },
            new LocationType { Id = 3, Name = "City", ColorHex = "#CD5C5C", IconPath = "avares://MechanicalCataphract/Assets/location-icons/city.png", ScaleFactor = 0.64 },
            new LocationType { Id = 4, Name = "Fortified Town", ColorHex = "#4B0082", IconPath = "avares://MechanicalCataphract/Assets/location-icons/fort.png", ScaleFactor = 0.64 },
            new LocationType { Id = 5, Name = "Town", ColorHex = "#DAA520", IconPath = "avares://MechanicalCataphract/Assets/location-icons/house.png", ScaleFactor = 0.64 }
        );
    }
}
