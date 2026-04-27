using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using GUI.ViewModels.EntityViewModels;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Discord;
using Microsoft.Extensions.DependencyInjection;

namespace GUI.ViewModels.HexMap;

public sealed class EntityViewModelFactory(
    IServiceScopeFactory scopeFactory,
    IDiscordChannelManager discordChannelManager,
    Func<int> getMapRows,
    Func<int> getMapColumns,
    Func<ObservableCollection<Commander>> getCommanders,
    Func<ObservableCollection<Faction>> getFactions,
    Func<ObservableCollection<Army>> getArmies,
    Func<ObservableCollection<LocationType>> getLocationTypes,
    Func<ObservableCollection<Weather>> getWeatherTypes)
{
    public FactionViewModel CreateFaction(
        Faction faction,
        Action<Army> selectArmy,
        Action<Commander> selectCommander,
        Action<Faction> saved)
    {
        var vm = new FactionViewModel(faction, scopeFactory, enableFactionRules: true, discordChannelManager);
        vm.ArmySelected += selectArmy;
        vm.CommanderSelected += selectCommander;
        vm.Saved += () => saved(vm.Entity);
        return vm;
    }

    public ArmyViewModel CreateArmy(
        Army army,
        Func<Brigade, Task<Army?>> transferRequested,
        Action<Army> pathSelectionRequested,
        Func<Task> pathSelectionConfirmRequested,
        Action pathSelectionCancelRequested,
        Func<Army, Task> scoutingReportRequested,
        Func<Army, Task> armyReportRequested,
        Action<Army> saved)
    {
        var vm = new ArmyViewModel(
            army,
            scopeFactory,
            getCommanders(),
            getFactions(),
            getMapRows(),
            getMapColumns());

        vm.TransferRequested += transferRequested;
        vm.PathSelectionRequested += pathSelectionRequested;
        vm.PathSelectionConfirmRequested += pathSelectionConfirmRequested;
        vm.PathSelectionCancelRequested += pathSelectionCancelRequested;
        vm.ScoutingReportRequested += scoutingReportRequested;
        vm.ArmyReportRequested += armyReportRequested;
        vm.Saved += () => saved(vm.Entity);
        return vm;
    }

    public CommanderViewModel CreateCommander(
        Commander commander,
        Action<Commander> pathSelectionRequested,
        Func<Task> pathSelectionConfirmRequested,
        Action pathSelectionCancelRequested,
        Action mapRefreshRequested,
        Action<Commander> saved)
    {
        var vm = new CommanderViewModel(
            commander,
            scopeFactory,
            getArmies(),
            getFactions(),
            getMapRows(),
            getMapColumns(),
            discordChannelManager);

        vm.PathSelectionRequested += pathSelectionRequested;
        vm.PathSelectionConfirmRequested += pathSelectionConfirmRequested;
        vm.PathSelectionCancelRequested += pathSelectionCancelRequested;
        vm.MapRefreshRequested += mapRefreshRequested;
        vm.Saved += () => saved(vm.Entity);
        return vm;
    }

    public MessageViewModel CreateMessage(
        Message message,
        Action<Message> pathSelectionRequested,
        Func<Task> pathSelectionConfirmRequested,
        Action pathSelectionCancelRequested,
        Action<Message> saved)
    {
        var vm = new MessageViewModel(
            message,
            scopeFactory,
            getCommanders(),
            getMapRows(),
            getMapColumns(),
            discordChannelManager);

        vm.PathSelectionRequested += pathSelectionRequested;
        vm.PathSelectionConfirmRequested += pathSelectionConfirmRequested;
        vm.PathSelectionCancelRequested += pathSelectionCancelRequested;
        vm.Saved += () => saved(vm.Entity);
        return vm;
    }

    public CoLocationChannelViewModel CreateCoLocationChannel(CoLocationChannel channel, Action<CoLocationChannel> saved)
    {
        var vm = new CoLocationChannelViewModel(channel, scopeFactory, getArmies(), getCommanders(), discordChannelManager);
        vm.Saved += () => saved(vm.Entity);
        return vm;
    }

    public NavyViewModel CreateNavy(Navy navy, Func<Navy, Task> navyReportRequested, Action<Navy> saved)
    {
        var vm = new NavyViewModel(
            navy,
            scopeFactory,
            getCommanders(),
            getArmies(),
            getFactions(),
            getMapRows(),
            getMapColumns());

        vm.NavyReportRequested += navyReportRequested;
        vm.Saved += () => saved(vm.Entity);
        return vm;
    }

    public MapHexViewModel CreateMapHex(MapHex mapHex, Action<MapHex> saved)
    {
        var vm = new MapHexViewModel(mapHex, scopeFactory, getFactions(), getLocationTypes(), getWeatherTypes());
        vm.Saved += () => saved(vm.Entity);
        return vm;
    }
}
