using System.Threading.Tasks;
using NetCord.Gateway;

namespace MechanicalCataphract.Discord;

public interface IDiscordBotService
{
    GatewayClient? Client { get; }
    bool IsConnected { get; }
    string StatusMessage { get; }

    /// <summary>
    /// Starts the bot if a config exists in the database. No-op if not configured.
    /// </summary>
    Task TryAutoStartAsync();

    /// <summary>
    /// Starts (or restarts) the bot with the current DiscordConfig from the database.
    /// </summary>
    Task StartBotAsync();

    /// <summary>
    /// Stops the bot gracefully.
    /// </summary>
    Task StopBotAsync();
}
