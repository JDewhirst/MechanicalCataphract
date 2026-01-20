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

        // Order -> Commander
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Commander)
            .WithMany()
            .HasForeignKey(o => o.CommanderId)
            .OnDelete(DeleteBehavior.Cascade);

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
    }
}
