using GUI.ViewModels.EntityViewModels;
using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace MechanicalCataphract.Tests.ViewModels;

[TestFixture]
public class ArmyViewModelBrigadeViewTests
{
    private static ArmyViewModel CreateViewModel()
    {
        GameRules.SetForTesting(GameRulesService.CreateDefaults());

        var army = new Army
        {
            Id = 42,
            Name = "Test Army",
            FactionId = 1,
            Brigades = new List<Brigade>
            {
                new() { Id = 1, ArmyId = 42, Name = "Alpha", UnitType = UnitType.Infantry, Number = 300, SortOrder = 0 },
                new() { Id = 2, ArmyId = 42, Name = "Bravo", UnitType = UnitType.Cavalry, Number = 100, SortOrder = 1 },
                new() { Id = 3, ArmyId = 42, Name = "Charlie", UnitType = UnitType.Skirmishers, Number = 200, SortOrder = 2 }
            }
        };

        var armyService = new Mock<IArmyService>();
        var factionRuleService = new Mock<IFactionRuleService>();
        factionRuleService
            .Setup(s => s.GetRuleValueAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<double>()))
            .ReturnsAsync((int _, string _, double defaultValue) => defaultValue);

        var services = new ServiceCollection();
        services.AddScoped(_ => armyService.Object);
        services.AddScoped(_ => factionRuleService.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        return new ArmyViewModel(army, scopeFactory, Array.Empty<Commander>(), Array.Empty<Faction>());
    }

    [Test]
    public void BrigadeSearchText_FiltersByName()
    {
        var vm = CreateViewModel();

        vm.BrigadeSearchText = "avo";

        Assert.That(vm.BrigadesView.Select(b => b.Name), Is.EqualTo(new[] { "Bravo" }));
    }

    [Test]
    public void SelectedBrigadeUnitTypeFilter_FiltersByUnitType()
    {
        var vm = CreateViewModel();

        vm.SelectedBrigadeUnitTypeFilter = UnitType.Cavalry.ToString();

        Assert.That(vm.BrigadesView.Select(b => b.Name), Is.EqualTo(new[] { "Bravo" }));
    }

    [Test]
    public void BrigadeSortMode_SortsDisplayedViewOnly()
    {
        var vm = CreateViewModel();
        var originalSortOrders = vm.Brigades.Select(b => b.SortOrder).ToArray();

        vm.BrigadeSortMode = BrigadeSortMode.Number;

        Assert.That(vm.BrigadesView.Select(b => b.Name), Is.EqualTo(new[] { "Bravo", "Charlie", "Alpha" }));
        Assert.That(vm.Brigades.Select(b => b.SortOrder), Is.EqualTo(originalSortOrders));
        Assert.That(vm.Brigades.Select(b => b.Name), Is.EqualTo(new[] { "Alpha", "Bravo", "Charlie" }));
    }
}
