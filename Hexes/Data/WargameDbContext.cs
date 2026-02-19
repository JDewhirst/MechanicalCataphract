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

        // Seed default faction
        modelBuilder.Entity<Faction>().HasData(
            new Faction { Id = 1, Name = "No Faction", ColorHex = "#808080" }
            );

        // Terrain types are loaded from Assets/classic-icons/classic.properties at startup
        // See App.axaml.cs OnFrameworkInitializationCompleted()

        // Seed default weather types
        modelBuilder.Entity<Weather>().HasData(
            new Weather { Id = 1, Name = "Clear", MovementModifier = 1.0, CombatModifier = 1.0 },
            new Weather { Id = 2, Name = "Rain", MovementModifier = 0.75, CombatModifier = 0.9 },
            new Weather { Id = 3, Name = "Storm", MovementModifier = 0.5, CombatModifier = 0.7 },
            new Weather { Id = 4, Name = "Snow", MovementModifier = 0.5, CombatModifier = 0.8 },
            new Weather { Id = 5, Name = "Fog", MovementModifier = 0.75, CombatModifier = 0.8 }
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
