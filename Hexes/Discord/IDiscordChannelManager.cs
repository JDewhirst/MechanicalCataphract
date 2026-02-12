using System.Threading.Tasks;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Discord;

public interface IDiscordChannelManager
{
    Task OnFactionCreatedAsync(Faction faction);
    Task OnFactionDeletedAsync(Faction faction);
    Task OnCommanderCreatedAsync(Commander commander, Faction faction);
    Task OnCommanderDeletedAsync(Commander commander);
    Task OnCommanderFactionChangedAsync(Commander commander, Faction oldFaction, Faction newFaction);
    Task OnFactionUpdatedAsync(Faction faction, string? oldName, string? oldColorHex);
}
