using System.Threading.Tasks;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Discord;

public interface IDiscordChannelManager
{
    /// <summary>
    /// Ensures the "No Faction" sentinel has Discord resources (role, category, channel).
    /// Called on bot connection so all commanders can have channels regardless of faction.
    /// </summary>
    Task EnsureSentinelFactionResourcesAsync();
    Task OnFactionCreatedAsync(Faction faction);
    Task OnFactionDeletedAsync(Faction faction);
    Task OnCommanderCreatedAsync(Commander commander, Faction faction);
    Task OnCommanderDiscordLinkedAsync(Commander commander, Faction faction);
    Task OnCommanderDeletedAsync(Commander commander);
    Task OnCommanderFactionChangedAsync(Commander commander, Faction oldFaction, Faction newFaction);
    Task OnCommanderUpdatedAsync(Commander commander);
    Task OnFactionUpdatedAsync(Faction faction, string? oldName, string? oldColorHex);

    // Co-location channel management
    Task EnsureCoLocationCategoryAsync();
    Task OnCoLocationChannelCreatedAsync(CoLocationChannel channel);
    Task OnCoLocationChannelDeletedAsync(CoLocationChannel channel);
    Task OnCoLocationChannelUpdatedAsync(CoLocationChannel channel);
    Task OnCommanderAddedToCoLocationAsync(CoLocationChannel channel, Commander commander);
    Task OnCommanderRemovedFromCoLocationAsync(CoLocationChannel channel, Commander commander);

    // Message delivery
    /// <summary>
    /// Sends a game message to a commander's private Discord channel.
    /// </summary>
    Task SendMessageToCommanderChannelAsync(Commander target, string content);

    /// <summary>
    /// Sends a Discord embed to a commander's private channel.
    /// </summary>
    Task SendEmbedToCommanderChannelAsync(Commander target, NetCord.Rest.EmbedProperties embed);

    // Army status reports
    /// <summary>
    /// Sends army status report embeds for all armies commanded by the given commander.
    /// </summary>
    Task SendArmyReportsToCommanderAsync(int commanderId);

    /// <summary>
    /// Sends army status reports to all commanders who have Discord channels. Used by daily tick.
    /// </summary>
    Task SendAllArmyReportsAsync();
}
