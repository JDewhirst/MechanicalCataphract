using Microsoft.EntityFrameworkCore;
using MechanicalCataphract.Data.Entities;

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
    public DbSet<Location> Locations { get; set; }
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

        // Location -> MapHex
        modelBuilder.Entity<Location>()
            .HasOne(l => l.Hex)
            .WithMany(h => h.Locations)
            .HasForeignKey(l => new { l.HexQ, l.HexR })
            .OnDelete(DeleteBehavior.Cascade);

        // Order -> Commander
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Commander)
            .WithMany()
            .HasForeignKey(o => o.CommanderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed default terrain types
        modelBuilder.Entity<TerrainType>().HasData(
            new TerrainType { Id = 1, Name = "Plains", ColorHex = "#90EE90", BaseMovementCost = 1 },
            new TerrainType { Id = 2, Name = "Forest", ColorHex = "#228B22", BaseMovementCost = 2 },
            new TerrainType { Id = 3, Name = "Mountains", ColorHex = "#8B4513", BaseMovementCost = 3 },
            new TerrainType { Id = 4, Name = "Water", ColorHex = "#4169E1", BaseMovementCost = 99 },
            new TerrainType { Id = 5, Name = "Desert", ColorHex = "#F4A460", BaseMovementCost = 2 },
            new TerrainType { Id = 6, Name = "Swamp", ColorHex = "#556B2F", BaseMovementCost = 3 },
            new TerrainType { Id = 7, Name = "Hills", ColorHex = "#CD853F", BaseMovementCost = 2 }
        );

        // Seed default weather types
        modelBuilder.Entity<Weather>().HasData(
            new Weather { Id = 1, Name = "Clear", MovementModifier = 1.0, CombatModifier = 1.0 },
            new Weather { Id = 2, Name = "Rain", MovementModifier = 0.75, CombatModifier = 0.9 },
            new Weather { Id = 3, Name = "Storm", MovementModifier = 0.5, CombatModifier = 0.7 },
            new Weather { Id = 4, Name = "Snow", MovementModifier = 0.5, CombatModifier = 0.8 },
            new Weather { Id = 5, Name = "Fog", MovementModifier = 0.75, CombatModifier = 0.8 }
        );
    }
}
