using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Tests.Data.Entities;

[TestFixture]
public class ArmyRoadHexesPerDayTests
{
    [SetUp]
    public void Setup()
    {
        GameRules.SetForTesting(GameRulesService.CreateDefaults());
    }

    [Test]
    public void RoadHexesPerDay_EmptyArmy_UsesArmyBaseRate()
    {
        var army = new Army();

        Assert.That(army.RoadHexesPerDay, Is.EqualTo(2.0f));
    }

    [Test]
    public void RoadHexesPerDay_InfantryOnly_UsesInfantryRate()
    {
        var army = new Army
        {
            Brigades =
            {
                new Brigade { UnitType = UnitType.Infantry, Number = 100 }
            }
        };

        Assert.That(army.RoadHexesPerDay, Is.EqualTo(2.0f));
    }

    [Test]
    public void RoadHexesPerDay_CavalryOnly_UsesCavalryRate()
    {
        var army = new Army
        {
            Brigades =
            {
                new Brigade { UnitType = UnitType.Cavalry, Number = 100 }
            }
        };

        Assert.That(army.RoadHexesPerDay, Is.EqualTo(3.0f));
    }

    [Test]
    public void RoadHexesPerDay_MixedUnits_UsesSlowestRate()
    {
        var army = new Army
        {
            Brigades =
            {
                new Brigade { UnitType = UnitType.Cavalry, Number = 100 },
                new Brigade { UnitType = UnitType.Infantry, Number = 100 }
            }
        };

        Assert.That(army.RoadHexesPerDay, Is.EqualTo(2.0f));
    }
}
