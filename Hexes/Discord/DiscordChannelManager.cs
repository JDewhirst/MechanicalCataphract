using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NetCord;
using NetCord.Rest;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Discord;

public class DiscordChannelManager : IDiscordChannelManager
{
    private readonly IDiscordBotService _botService;
    private readonly IServiceProvider _serviceProvider;

    public DiscordChannelManager(IDiscordBotService botService, IServiceProvider serviceProvider)
    {
        _botService = botService;
        _serviceProvider = serviceProvider;
    }

    public async Task OnFactionCreatedAsync(Faction faction)
    {
        if (!_botService.IsConnected) return;

        try
        {
            var rest = _botService.Client!.Rest;
            var guildId = await GetGuildIdAsync();
            if (guildId == null) return;

            // 1. Create faction role with faction color
            var color = ParseColor(faction.ColorHex);
            var role = await rest.CreateGuildRoleAsync(guildId.Value, new RoleProperties
            {
                Name = faction.Name,
                Color = color,
            });
            faction.DiscordRoleId = role.Id;

            // 2. Create channel category for the faction
            var category = await rest.CreateGuildChannelAsync(guildId.Value,
                new GuildChannelProperties(faction.Name, ChannelType.CategoryChannel));
            faction.DiscordCategoryId = category.Id;

            // 3. Create read-only text channel in the category
            var channelOverwrites = new List<PermissionOverwriteProperties>
            {
                // Deny @everyone from seeing the channel
                new(guildId.Value, PermissionOverwriteType.Role)
                {
                    Denied = Permissions.ViewChannel,
                },
                // Allow faction role to view but not send
                new(role.Id, PermissionOverwriteType.Role)
                {
                    Allowed = Permissions.ViewChannel | Permissions.ReadMessageHistory,
                    Denied = Permissions.SendMessages,
                },
            };

            // Grant the bot itself access so it can manage the channel
            AddBotOverwrite(channelOverwrites);

            // If admin role is configured, grant full access
            var adminRoleId = await GetAdminRoleIdAsync();
            if (adminRoleId != null)
            {
                channelOverwrites.Add(new(adminRoleId.Value, PermissionOverwriteType.Role)
                {
                    Allowed = Permissions.ViewChannel | Permissions.SendMessages
                            | Permissions.ReadMessageHistory | Permissions.ManageChannels,
                });
            }

            var channel = await rest.CreateGuildChannelAsync(guildId.Value,
                new GuildChannelProperties($"{faction.Name.ToLowerInvariant()}-general", ChannelType.TextGuildChannel)
                {
                    ParentId = faction.DiscordCategoryId,
                    PermissionOverwrites = channelOverwrites,
                });
            faction.DiscordChannelId = channel.Id;

            // Save Discord IDs back to the faction
            await SaveFactionAsync(faction);

            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Faction '{faction.Name}' — role {role.Id}, category {faction.DiscordCategoryId}, channel {faction.DiscordChannelId}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] OnFactionCreated failed: {ex.Message}");
        }
    }

    public async Task OnFactionDeletedAsync(Faction faction)
    {
        if (!_botService.IsConnected) return;

        try
        {
            var rest = _botService.Client!.Rest;
            var guildId = await GetGuildIdAsync();
            if (guildId == null) return;

            // 1. Remove faction role from all commanders' Discord users
            //    and move their private channels out of this faction's category.
            var commanders = await GetCommandersByFactionAsync(faction.Id);
            foreach (var commander in commanders)
            {
                try
                {
                    if (commander.DiscordUserId.HasValue && faction.DiscordRoleId.HasValue)
                    {
                        await rest.RemoveGuildUserRoleAsync(guildId.Value, commander.DiscordUserId.Value, faction.DiscordRoleId.Value);
                    }

                    // Move private channel to top-level (no parent category)
                    if (commander.DiscordChannelId.HasValue)
                    {
                        await rest.ModifyGuildChannelAsync(commander.DiscordChannelId.Value, options =>
                        {
                            options.ParentId = null;
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Failed to clean up commander '{commander.Name}': {ex.Message}");
                }
            }

            // 2. Delete channel, category, and role (order matters — children before parent)
            if (faction.DiscordChannelId.HasValue)
                await rest.DeleteChannelAsync(faction.DiscordChannelId.Value);

            if (faction.DiscordCategoryId.HasValue)
                await rest.DeleteChannelAsync(faction.DiscordCategoryId.Value);

            if (faction.DiscordRoleId.HasValue)
                await rest.DeleteGuildRoleAsync(guildId.Value, faction.DiscordRoleId.Value);

            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Faction '{faction.Name}' Discord resources deleted ({commanders.Count} commanders cleaned up).");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] OnFactionDeleted failed: {ex.Message}");
        }
    }

    public async Task OnCommanderCreatedAsync(Commander commander, Faction faction)
    {
        if (!_botService.IsConnected) return;
        if (commander.DiscordUserId == null) return; // No Discord user linked
        if (faction.DiscordCategoryId == null) return; // Faction has no Discord category

        try
        {
            var rest = _botService.Client!.Rest;
            var guildId = await GetGuildIdAsync();
            if (guildId == null) return;

            // 1. Create private channel in faction category
            var channelOverwrites = new List<PermissionOverwriteProperties>
            {
                // Deny @everyone
                new(guildId.Value, PermissionOverwriteType.Role)
                {
                    Denied = Permissions.ViewChannel,
                },
                // Allow the commander's Discord user read+write
                new(commander.DiscordUserId.Value, PermissionOverwriteType.User)
                {
                    Allowed = Permissions.ViewChannel | Permissions.SendMessages
                            | Permissions.ReadMessageHistory,
                },
            };

            // Grant the bot itself access so it can manage the channel
            AddBotOverwrite(channelOverwrites);

            // If admin role is configured, grant full access
            var adminRoleId = await GetAdminRoleIdAsync();
            if (adminRoleId != null)
            {
                channelOverwrites.Add(new(adminRoleId.Value, PermissionOverwriteType.Role)
                {
                    Allowed = Permissions.ViewChannel | Permissions.SendMessages
                            | Permissions.ReadMessageHistory | Permissions.ManageChannels,
                });
            }

            var channel = await rest.CreateGuildChannelAsync(guildId.Value,
                new GuildChannelProperties($"cmd-{commander.Name.ToLowerInvariant().Replace(' ', '-')}", ChannelType.TextGuildChannel)
                {
                    ParentId = faction.DiscordCategoryId,
                    PermissionOverwrites = channelOverwrites,
                });
            commander.DiscordChannelId = channel.Id;

            // 2. Assign faction role to Discord user
            if (faction.DiscordRoleId.HasValue)
            {
                await rest.AddGuildUserRoleAsync(guildId.Value, commander.DiscordUserId.Value, faction.DiscordRoleId.Value);
            }

            // Save Discord channel ID back to commander
            await SaveCommanderAsync(commander);

            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Commander '{commander.Name}' — channel {commander.DiscordChannelId}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] OnCommanderCreated failed: {ex.Message}");
        }
    }

    public async Task OnCommanderDeletedAsync(Commander commander)
    {
        if (!_botService.IsConnected) return;

        try
        {
            var rest = _botService.Client!.Rest;
            var guildId = await GetGuildIdAsync();
            if (guildId == null) return;

            // Delete commander's channel
            if (commander.DiscordChannelId.HasValue)
                await rest.DeleteChannelAsync(commander.DiscordChannelId.Value);

            // Remove faction role from Discord user
            if (commander.DiscordUserId.HasValue && commander.Faction?.DiscordRoleId != null)
            {
                await rest.RemoveGuildUserRoleAsync(guildId.Value, commander.DiscordUserId.Value, commander.Faction.DiscordRoleId.Value);
            }

            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Commander '{commander.Name}' Discord resources deleted.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] OnCommanderDeleted failed: {ex.Message}");
        }
    }

    public async Task OnCommanderFactionChangedAsync(Commander commander, Faction oldFaction, Faction newFaction)
    {
        if (!_botService.IsConnected) return;
        if (commander.DiscordUserId == null) return;

        try
        {
            var rest = _botService.Client!.Rest;
            var guildId = await GetGuildIdAsync();
            if (guildId == null) return;

            // 1. Swap roles
            if (oldFaction.DiscordRoleId.HasValue)
                await rest.RemoveGuildUserRoleAsync(guildId.Value, commander.DiscordUserId.Value, oldFaction.DiscordRoleId.Value);

            if (newFaction.DiscordRoleId.HasValue)
                await rest.AddGuildUserRoleAsync(guildId.Value, commander.DiscordUserId.Value, newFaction.DiscordRoleId.Value);

            // 2. Move channel to new faction's category
            if (commander.DiscordChannelId.HasValue && newFaction.DiscordCategoryId.HasValue)
            {
                await rest.ModifyGuildChannelAsync(commander.DiscordChannelId.Value, options =>
                {
                    options.ParentId = newFaction.DiscordCategoryId.Value;
                });
            }

            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Commander '{commander.Name}' moved from '{oldFaction.Name}' to '{newFaction.Name}'.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] OnCommanderFactionChanged failed: {ex.Message}");
        }
    }

    public async Task OnFactionUpdatedAsync(Faction faction, string? oldName, string? oldColorHex)
    {
        if (!_botService.IsConnected) return;

        var rest = _botService.Client!.Rest;
        var guildId = await GetGuildIdAsync();
        if (guildId == null) return;

        bool nameChanged = oldName != null && oldName != faction.Name;
        bool colorChanged = oldColorHex != null && oldColorHex != faction.ColorHex;

        System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] OnFactionUpdated — name:{nameChanged}, color:{colorChanged}, RoleId:{faction.DiscordRoleId}, CatId:{faction.DiscordCategoryId}, ChanId:{faction.DiscordChannelId}");

        // Update role name and/or color
        if (faction.DiscordRoleId.HasValue && (nameChanged || colorChanged))
        {
            try
            {
                await rest.ModifyGuildRoleAsync(guildId.Value, faction.DiscordRoleId.Value, o =>
                {
                    if (nameChanged) o.Name = faction.Name;
                    if (colorChanged) o.Color = ParseColor(faction.ColorHex);
                });
                System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Role {faction.DiscordRoleId} updated.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Role update failed: {ex.Message}");
            }
        }

        // Update category name
        if (nameChanged && faction.DiscordCategoryId.HasValue)
        {
            try
            {
                await rest.ModifyGuildChannelAsync(faction.DiscordCategoryId.Value, o =>
                {
                    o.Name = faction.Name;
                });
                System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Category {faction.DiscordCategoryId} updated.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Category update failed: {ex.Message}");
            }
        }

        // Update general channel name
        if (nameChanged && faction.DiscordChannelId.HasValue)
        {
            try
            {
                await rest.ModifyGuildChannelAsync(faction.DiscordChannelId.Value, o =>
                {
                    o.Name = $"{faction.Name.ToLowerInvariant()}-general";
                });
                System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Channel {faction.DiscordChannelId} updated.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Channel update failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Adds a permission overwrite for the bot user so it can view, send, and manage
    /// channels that deny @everyone. Without this, the bot is locked out by explicit denies.
    /// </summary>
    private void AddBotOverwrite(List<PermissionOverwriteProperties> overwrites)
    {
        if (_botService.Client == null) return;

        overwrites.Add(new(_botService.Client.Id, PermissionOverwriteType.User)
        {
            Allowed = Permissions.ViewChannel | Permissions.SendMessages
                    | Permissions.ReadMessageHistory | Permissions.ManageChannels,
        });
    }

    private async Task<IList<Commander>> GetCommandersByFactionAsync(int factionId)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(db.Commanders.Where(c => c.FactionId == factionId));
    }

    private async Task<ulong?> GetGuildIdAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
        var config = await db.DiscordConfigs.FindAsync(1);
        return config?.GuildId;
    }

    private async Task<ulong?> GetAdminRoleIdAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
        var config = await db.DiscordConfigs.FindAsync(1);
        return config?.AdminRoleId;
    }

    private async Task SaveFactionAsync(Faction faction)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
        db.Factions.Attach(faction);
        db.Entry(faction).Property(f => f.DiscordRoleId).IsModified = true;
        db.Entry(faction).Property(f => f.DiscordCategoryId).IsModified = true;
        db.Entry(faction).Property(f => f.DiscordChannelId).IsModified = true;
        await db.SaveChangesAsync();
    }

    private async Task SaveCommanderAsync(Commander commander)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
        db.Commanders.Attach(commander);
        db.Entry(commander).Property(c => c.DiscordChannelId).IsModified = true;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Parses a hex color string like "#FF0000" into a NetCord Color.
    /// </summary>
    private static Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return new Color(0x808080); // fallback grey

        var r = Convert.ToByte(hex.Substring(0, 2), 16);
        var g = Convert.ToByte(hex.Substring(2, 2), 16);
        var b = Convert.ToByte(hex.Substring(4, 2), 16);
        return new Color(r, g, b);
    }
}
