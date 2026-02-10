using System.Threading.Tasks;
using NetCord.Gateway;

namespace MechanicalCataphract.Discord;

public interface IDiscordBotService
{
    GatewayClient? Client { get; }
    bool IsConnected { get; }
    string StatusMessage { get; }

    /// <summary>
    /// Starts (or restarts) the bot with the current DiscordConfig from the database.
    /// </summary>
    Task StartBotAsync();

    /// <summary>
    /// Stops the bot gracefully.
    /// </summary>
    Task StopBotAsync();
}
