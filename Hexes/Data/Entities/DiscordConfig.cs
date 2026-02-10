namespace MechanicalCataphract.Data.Entities;

public class DiscordConfig
{
    public int Id { get; set; } = 1; // Singleton - always ID 1
    public string? BotToken { get; set; }
    public ulong? GuildId { get; set; }
    public ulong? AdminRoleId { get; set; }
}
