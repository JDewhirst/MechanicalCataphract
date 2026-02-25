using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Tests.Data.Entities;

/// <summary>
/// Unit tests for Navy and Army computed properties that depend on GameRules.
/// No database required — entities are instantiated directly.
/// </summary>
[TestFixture]
public class NavyComputedPropertiesTests
{
    // Default ship rules: Transport=100 inf, 20 cav, 10000 supply, 5 wagons
    //                     Warship=0.5x capacity, 20 supply/ship/day

    [SetUp]
    public void SetUp()
    {
        GameRules.SetForTesting(GameRulesService.CreateDefaults());
    }

    // ──────────────────────────────────────────────
    // MaxCarryUnits
    // ──────────────────────────────────────────────

    [Test]
    public void MaxCarryUnits_TwoTransports_Returns200()
    {
        var navy = NavyWith(transports: 2, warships: 0);
        Assert.That(navy.MaxCarryUnits, Is.EqualTo(200));
    }

    [Test]
    public void MaxCarryUnits_OneWarship_Returns50()
    {
        var navy = NavyWith(transports: 0, warships: 1);
        Assert.That(navy.MaxCarryUnits, Is.EqualTo(50));
    }

    [Test]
    public void MaxCarryUnits_TwoTransportsOneWarship_Returns250()
    {
        var navy = NavyWith(transports: 2, warships: 1);
        Assert.That(navy.MaxCarryUnits, Is.EqualTo(250));
    }

    [Test]
    public void MaxCarryUnits_NoShips_Returns0()
    {
        var navy = NavyWith(transports: 0, warships: 0);
        Assert.That(navy.MaxCarryUnits, Is.EqualTo(0));
    }

    // ──────────────────────────────────────────────
    // Ship type counts
    // ──────────────────────────────────────────────

    [Test]
    public void TransportCount_CountsOnlyTransports()
    {
        var navy = NavyWith(transports: 3, warships: 2);
        Assert.That(navy.TransportCount, Is.EqualTo(3));
        Assert.That(navy.WarshipCount, Is.EqualTo(2));
    }

    // ──────────────────────────────────────────────
    // DailySupplyConsumption
    // ──────────────────────────────────────────────

    [Test]
    public void DailySupplyConsumption_ThreeShips_Returns60()
    {
        // 3 ships × 20 crew supply/ship/day = 60
        var navy = NavyWith(transports: 2, warships: 1);
        Assert.That(navy.DailySupplyConsumption, Is.EqualTo(60));
    }

    [Test]
    public void DailySupplyConsumption_NoShips_Returns0()
    {
        var navy = NavyWith(transports: 0, warships: 0);
        Assert.That(navy.DailySupplyConsumption, Is.EqualTo(0));
    }

    // ──────────────────────────────────────────────
    // DaysOfSupply
    // ──────────────────────────────────────────────

    [Test]
    public void DaysOfSupply_CalculatesCorrectly()
    {
        // 3 ships × 20 = 60/day; 300 supply → 5 days
        var navy = NavyWith(transports: 2, warships: 1, carriedSupply: 300);
        Assert.That(navy.DaysOfSupply, Is.EqualTo(5.0).Within(0.001));
    }

    [Test]
    public void DaysOfSupply_NoShips_ReturnsZero()
    {
        var navy = NavyWith(transports: 0, warships: 0, carriedSupply: 500);
        Assert.That(navy.DaysOfSupply, Is.EqualTo(0));
    }

    // ──────────────────────────────────────────────
    // TotalCarryUnits — navy supply only
    // ──────────────────────────────────────────────

    [Test]
    public void TotalCarryUnits_NavySupplyOnly_UsesSupplyWeight()
    {
        // 10000 supply × (1 / 10000) = 1.0 cargo unit
        var navy = NavyWith(transports: 1, warships: 0, carriedSupply: 10000);
        Assert.That(navy.TotalCarryUnits, Is.EqualTo(1.0).Within(0.001));
    }

    [Test]
    public void TotalCarryUnits_NoCargoNoArmy_ReturnsZero()
    {
        var navy = NavyWith(transports: 1, warships: 0, carriedSupply: 0);
        Assert.That(navy.TotalCarryUnits, Is.EqualTo(0.0));
    }

    // ──────────────────────────────────────────────
    // TotalCarryUnits — with embarked army
    // ──────────────────────────────────────────────

    [Test]
    public void TotalCarryUnits_InfantryBrigade_CountsAt1PerMan()
    {
        // 50 infantry = 50 cargo units
        var navy = NavyWith(transports: 1, warships: 0, carriedSupply: 0);
        navy.CarriedArmy = new Army
        {
            Brigades = new List<Brigade>
            {
                new Brigade { UnitType = UnitType.Infantry, Number = 50 }
            }
        };

        Assert.That(navy.TotalCarryUnits, Is.EqualTo(50.0).Within(0.001));
    }

    [Test]
    public void TotalCarryUnits_CavalryBrigade_CountsAt5PerMan()
    {
        // TransportInfantryCapacity=100, TransportCavalryCapacity=20 → weight=100/20=5
        // 20 cavalry = 20 × 5 = 100 cargo units
        var navy = NavyWith(transports: 1, warships: 0, carriedSupply: 0);
        navy.CarriedArmy = new Army
        {
            Brigades = new List<Brigade>
            {
                new Brigade { UnitType = UnitType.Cavalry, Number = 20 }
            }
        };

        Assert.That(navy.TotalCarryUnits, Is.EqualTo(100.0).Within(0.001));
    }

    [Test]
    public void TotalCarryUnits_ArmyWithSupplyAndWagons()
    {
        // Supply weight = 1/10000 per unit, Wagon weight = 100/5 = 20 per wagon
        // 1000 supply × 0.0001 = 0.1
        // 2 wagons × 20 = 40
        // Total = 40.1
        var navy = NavyWith(transports: 1, warships: 0, carriedSupply: 0);
        navy.CarriedArmy = new Army
        {
            CarriedSupply = 1000,
            Wagons = 2,
            Brigades = new List<Brigade>()
        };

        Assert.That(navy.TotalCarryUnits, Is.EqualTo(40.1).Within(0.001));
    }

    [Test]
    public void TotalCarryUnits_Mixed_SumsAllSources()
    {
        // Navy supply: 5000 × 0.0001 = 0.5
        // Army: 10 infantry (10) + 4 cavalry (4×5=20) + 500 supply (0.05) + 1 wagon (20)
        // Total = 0.5 + 10 + 20 + 0.05 + 20 = 50.55
        var navy = NavyWith(transports: 1, warships: 0, carriedSupply: 5000);
        navy.CarriedArmy = new Army
        {
            CarriedSupply = 500,
            Wagons = 1,
            Brigades = new List<Brigade>
            {
                new Brigade { UnitType = UnitType.Infantry, Number = 10 },
                new Brigade { UnitType = UnitType.Cavalry, Number = 4 }
            }
        };

        Assert.That(navy.TotalCarryUnits, Is.EqualTo(50.55).Within(0.001));
    }

    // ──────────────────────────────────────────────
    // Army.IsEmbarked
    // ──────────────────────────────────────────────

    [Test]
    public void Army_IsEmbarked_TrueWhenNavyIdSet()
    {
        var army = new Army { NavyId = 42, FactionId = 1 };
        Assert.That(army.IsEmbarked, Is.True);
    }

    [Test]
    public void Army_IsEmbarked_FalseWhenNavyIdNull()
    {
        var army = new Army { NavyId = null, FactionId = 1 };
        Assert.That(army.IsEmbarked, Is.False);
    }

    // ──────────────────────────────────────────────
    // GameRules responsiveness
    // ──────────────────────────────────────────────

    [Test]
    public void MaxCarryUnits_RespondsToGameRulesChange()
    {
        // Override TransportInfantryCapacity to 200 → 1 transport = 200 units
        var modified = GameRulesService.CreateDefaults();
        modified = modified with
        {
            Ships = modified.Ships with { TransportInfantryCapacity = 200 }
        };
        GameRules.SetForTesting(modified);

        var navy = NavyWith(transports: 1, warships: 0);
        Assert.That(navy.MaxCarryUnits, Is.EqualTo(200));
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static Navy NavyWith(int transports, int warships, int carriedSupply = 0)
    {
        var ships = new List<Ship>();
        if (transports > 0) ships.Add(new Ship { ShipType = ShipType.Transport, Count = transports });
        if (warships > 0)   ships.Add(new Ship { ShipType = ShipType.Warship,   Count = warships  });

        return new Navy { CarriedSupply = carriedSupply, Ships = ships };
    }
}
