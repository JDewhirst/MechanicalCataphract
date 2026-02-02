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

        // Army -> MapHex (location)
        modelBuilder.Entity<Army>()
            .HasOne(a => a.Location)
            .WithMany(h => h.Armies)
            .HasForeignKey(a => new { a.LocationQ, a.LocationR })
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

        // Commander -> Faction
        modelBuilder.Entity<Commander>()
            .HasOne(c => c.Faction)
            .WithMany(f => f.Commanders)
            .HasForeignKey(c => c.FactionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Commander -> MapHex (location)
        modelBuilder.Entity<Commander>()
            .HasOne(c => c.Location)
            .WithMany(h => h.Commanders)
            .HasForeignKey(c => new { c.LocationQ, c.LocationR })
            .OnDelete(DeleteBehavior.SetNull);

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

        // Order -> Commander
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Commander)
            .WithMany()
            .HasForeignKey(o => o.CommanderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Message -> MapHex (location)
        modelBuilder.Entity<Message>()
            .HasOne(a => a.Location)
            .WithMany(h => h.Messages)
            .HasForeignKey(a => new { a.LocationQ, a.LocationR })
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

        // Seed default location types
        modelBuilder.Entity<LocationType>().HasData(
            new LocationType { Id = 1, Name = "City", ColorHex = "#8B0000" },
            new LocationType { Id = 2, Name = "Town", ColorHex = "#CD5C5C" },
            new LocationType { Id = 3, Name = "Fort", ColorHex = "#4B0082" },
            new LocationType { Id = 4, Name = "Village", ColorHex = "#DAA520" }
        );
    }
}
