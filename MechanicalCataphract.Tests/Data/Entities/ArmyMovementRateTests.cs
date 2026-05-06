using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Tests.Data.Entities;

[TestFixture]
public class ArmyMovementRateTests
{
    [SetUp]
    public void Setup()
    {
        GameRules.SetForTesting(GameRulesService.CreateDefaults());
    }

    [Test]
    public void MovementRate_EmptyArmy_UsesArmyBaseRate()
    {
        var army = new Army();

        Assert.That(army.MovementRate, Is.EqualTo(1.0f));
    }

    [Test]
    public void MovementRate_InfantryOnly_UsesInfantryRate()
    {
        var army = new Army
        {
            Brigades =
            {
                new Brigade { UnitType = UnitType.Infantry, Number = 100 }
            }
        };

        Assert.That(army.MovementRate, Is.EqualTo(1.0f));
    }

    [Test]
    public void MovementRate_CavalryOnly_UsesCavalryRate()
    {
        var army = new Army
        {
            Brigades =
            {
                new Brigade { UnitType = UnitType.Cavalry, Number = 100 }
            }
        };

        Assert.That(army.MovementRate, Is.EqualTo(1.5f));
    }

    [Test]
    public void MovementRate_MixedUnits_UsesSlowestRate()
    {
        var army = new Army
        {
            Brigades =
            {
                new Brigade { UnitType = UnitType.Cavalry, Number = 100 },
                new Brigade { UnitType = UnitType.Infantry, Number = 100 }
            }
        };

        Assert.That(army.MovementRate, Is.EqualTo(1.0f));
    }
}
