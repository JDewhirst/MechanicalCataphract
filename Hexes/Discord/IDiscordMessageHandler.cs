using System;
using System.Threading.Tasks;

namespace MechanicalCataphract.Discord;

public interface IDiscordMessageHandler
{
    /// <summary>
    /// Processes an incoming Discord message. Parses :envelope: and :scroll:
    /// commands from commander private channels and creates the corresponding entities.
    /// </summary>
    Task HandleMessageAsync(NetCord.Gateway.Message discordMessage);

    /// <summary>
    /// Raised after a new Message or Order entity is created from a Discord command.
    /// Subscribers should use this to refresh UI collections.
    /// </summary>
    event Action EntitiesChanged;
}
