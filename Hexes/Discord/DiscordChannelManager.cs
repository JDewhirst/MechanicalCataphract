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

    public async Task EnsureSentinelFactionResourcesAsync()
    {
        if (!_botService.IsConnected) return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
            var sentinel = await db.Factions.FindAsync(1);
            if (sentinel == null) return;

            // Already has Discord resources — nothing to do
            if (sentinel.DiscordCategoryId.HasValue) return;

            System.Diagnostics.Debug.WriteLine("[DiscordChannelManager] Creating Discord resources for 'No Faction' sentinel...");
            await OnFactionCreatedAsync(sentinel);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] EnsureSentinelFactionResources failed: {ex.Message}");
        }
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
                    Denied = Permissions.ViewChannel
                          | Permissions.CreatePublicThreads
                          | Permissions.CreatePrivateThreads,
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
        await SetupCommanderDiscordAsync(commander, faction);
    }

    public async Task OnCommanderDiscordLinkedAsync(Commander commander, Faction faction)
    {
        // Skip if the commander already has a private channel
        if (commander.DiscordChannelId.HasValue) return;

        await SetupCommanderDiscordAsync(commander, faction);
    }

    /// <summary>
    /// Shared logic for creating a commander's private Discord channel and assigning faction role.
    /// Called by both OnCommanderCreatedAsync and OnCommanderDiscordLinkedAsync.
    /// </summary>
    private async Task SetupCommanderDiscordAsync(Commander commander, Faction faction)
    {
        if (!_botService.IsConnected) return;
        if (commander.DiscordUserId == null) return; // No Discord user linked

        // The faction object may come from a ViewModel whose DbContext cached
        // the entity before Discord IDs were set (e.g. EnsureSentinelFactionResourcesAsync
        // ran in a different scope). Reload from a fresh scope if stale.
        if (faction.DiscordCategoryId == null)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
            var freshFaction = await db.Factions.FindAsync(faction.Id);
            if (freshFaction?.DiscordCategoryId != null)
            {
                faction.DiscordCategoryId = freshFaction.DiscordCategoryId;
                faction.DiscordRoleId = freshFaction.DiscordRoleId;
                faction.DiscordChannelId = freshFaction.DiscordChannelId;
            }
        }

        if (faction.DiscordCategoryId == null) return; // Faction genuinely has no Discord category

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
                    Denied = Permissions.ViewChannel
                          | Permissions.CreatePublicThreads
                          | Permissions.CreatePrivateThreads,
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
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] SetupCommanderDiscord failed: {ex.Message}");
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

    public async Task OnCommanderUpdatedAsync(Commander commander)
    {
        if (!_botService.IsConnected) return;
        if (!commander.DiscordChannelId.HasValue) return;

        try
        {
            var rest = _botService.Client!.Rest;
            await rest.ModifyGuildChannelAsync(commander.DiscordChannelId.Value, o =>
            {
                o.Name = $"cmd-{commander.Name.ToLowerInvariant().Replace(' ', '-')}";
            });
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Commander channel renamed to 'cmd-{commander.Name}'.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] OnCommanderUpdated failed: {ex.Message}");
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

    public async Task EnsureCoLocationCategoryAsync()
    {
        if (!_botService.IsConnected) return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
            var config = await db.DiscordConfigs.FindAsync(1);
            if (config == null) return;

            // Already has a co-location category
            if (config.CoLocationCategoryId.HasValue) return;

            var rest = _botService.Client!.Rest;
            var guildId = config.GuildId;
            if (guildId == null) return;

            var category = await rest.CreateGuildChannelAsync(guildId.Value,
                new GuildChannelProperties("Co-Location", ChannelType.CategoryChannel));
            config.CoLocationCategoryId = category.Id;
            await db.SaveChangesAsync();

            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Co-Location category created: {category.Id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] EnsureCoLocationCategory failed: {ex.Message}");
        }
    }

    public async Task OnCoLocationChannelCreatedAsync(CoLocationChannel channel)
    {
        if (!_botService.IsConnected) return;

        try
        {
            var rest = _botService.Client!.Rest;
            var guildId = await GetGuildIdAsync();
            if (guildId == null) return;

            var categoryId = await GetCoLocationCategoryIdAsync();
            if (categoryId == null) return;

            var channelOverwrites = new List<PermissionOverwriteProperties>
            {
                new(guildId.Value, PermissionOverwriteType.Role)
                {
                    Denied = Permissions.ViewChannel
                          | Permissions.CreatePublicThreads
                          | Permissions.CreatePrivateThreads,
                },
            };

            AddBotOverwrite(channelOverwrites);

            var adminRoleId = await GetAdminRoleIdAsync();
            if (adminRoleId != null)
            {
                channelOverwrites.Add(new(adminRoleId.Value, PermissionOverwriteType.Role)
                {
                    Allowed = Permissions.ViewChannel | Permissions.SendMessages
                            | Permissions.ReadMessageHistory | Permissions.ManageChannels,
                });
            }

            var discordChannel = await rest.CreateGuildChannelAsync(guildId.Value,
                new GuildChannelProperties($"coloc-{channel.Name.ToLowerInvariant().Replace(' ', '-')}", ChannelType.TextGuildChannel)
                {
                    ParentId = categoryId,
                    PermissionOverwrites = channelOverwrites,
                });
            channel.DiscordChannelId = discordChannel.Id;

            await SaveCoLocationChannelAsync(channel);

            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Co-location channel '{channel.Name}' created: {discordChannel.Id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] OnCoLocationChannelCreated failed: {ex.Message}");
        }
    }

    public async Task OnCoLocationChannelDeletedAsync(CoLocationChannel channel)
    {
        if (!_botService.IsConnected) return;
        if (!channel.DiscordChannelId.HasValue) return;

        try
        {
            var rest = _botService.Client!.Rest;
            await rest.DeleteChannelAsync(channel.DiscordChannelId.Value);
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Co-location channel '{channel.Name}' deleted.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] OnCoLocationChannelDeleted failed: {ex.Message}");
        }
    }

    public async Task OnCoLocationChannelUpdatedAsync(CoLocationChannel channel)
    {
        if (!_botService.IsConnected) return;
        if (!channel.DiscordChannelId.HasValue) return;

        try
        {
            var rest = _botService.Client!.Rest;
            await rest.ModifyGuildChannelAsync(channel.DiscordChannelId.Value, o =>
            {
                o.Name = $"coloc-{channel.Name.ToLowerInvariant().Replace(' ', '-')}";
            });
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Co-location channel renamed to 'coloc-{channel.Name}'.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] OnCoLocationChannelUpdated failed: {ex.Message}");
        }
    }

    public async Task OnCommanderAddedToCoLocationAsync(CoLocationChannel channel, Commander commander)
    {
        if (!_botService.IsConnected) return;
        if (!channel.DiscordChannelId.HasValue) return;
        if (!commander.DiscordUserId.HasValue) return;

        try
        {
            var rest = _botService.Client!.Rest;
            await rest.ModifyGuildChannelPermissionsAsync(channel.DiscordChannelId.Value,
                new PermissionOverwriteProperties(commander.DiscordUserId.Value, PermissionOverwriteType.User)
                {
                    Allowed = Permissions.ViewChannel | Permissions.SendMessages
                            | Permissions.ReadMessageHistory,
                });
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Commander '{commander.Name}' added to co-location '{channel.Name}'.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] OnCommanderAddedToCoLocation failed: {ex.Message}");
        }
    }

    public async Task OnCommanderRemovedFromCoLocationAsync(CoLocationChannel channel, Commander commander)
    {
        if (!_botService.IsConnected) return;
        if (!channel.DiscordChannelId.HasValue) return;
        if (!commander.DiscordUserId.HasValue) return;

        try
        {
            var rest = _botService.Client!.Rest;
            await rest.DeleteGuildChannelPermissionAsync(channel.DiscordChannelId.Value, commander.DiscordUserId.Value);
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Commander '{commander.Name}' removed from co-location '{channel.Name}'.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] OnCommanderRemovedFromCoLocation failed: {ex.Message}");
        }
    }

    public async Task SendMessageToCommanderChannelAsync(Commander target, string content)
    {
        if (!_botService.IsConnected) return;
        if (!target.DiscordChannelId.HasValue) return;

        try
        {
            var rest = _botService.Client!.Rest;
            await rest.SendMessageAsync(target.DiscordChannelId.Value,
                new NetCord.Rest.MessageProperties { Content = content });
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Message sent to '{target.Name}' channel.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] SendMessageToCommanderChannel failed: {ex.Message}");
        }
    }

    public async Task SendEmbedToCommanderChannelAsync(Commander target, EmbedProperties embed)
    {
        if (!_botService.IsConnected) return;
        if (!target.DiscordChannelId.HasValue) return;

        try
        {
            var rest = _botService.Client!.Rest;
            await rest.SendMessageAsync(target.DiscordChannelId.Value,
                new NetCord.Rest.MessageProperties { Embeds = [embed] });
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Embed sent to '{target.Name}' channel.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] SendEmbedToCommanderChannel failed: {ex.Message}");
        }
    }

    public async Task SendArmyReportsToCommanderAsync(int commanderId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var commanderService = scope.ServiceProvider.GetRequiredService<Services.ICommanderService>();
            var gameStateService = scope.ServiceProvider.GetRequiredService<Services.IGameStateService>();

            var commander = await commanderService.GetCommanderWithArmiesAsync(commanderId);
            if (commander == null || !commander.DiscordChannelId.HasValue) return;

            var gameState = await gameStateService.GetGameStateAsync();

            foreach (var army in commander.CommandedArmies)
            {
                var embed = ArmyReportEmbedBuilder.BuildArmyReport(army, commander, gameState.CurrentGameTime);
                await SendEmbedToCommanderChannelAsync(commander, embed);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] SendArmyReportsToCommander failed: {ex.Message}");
        }
    }

    public async Task SendAllArmyReportsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
            var commanderIds = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(db.Commanders
                    .Where(c => c.DiscordChannelId != null)
                    .Select(c => c.Id));

            foreach (var id in commanderIds)
            {
                await SendArmyReportsToCommanderAsync(id);
            }

            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Daily army reports sent to {commanderIds.Count} commanders.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] SendAllArmyReports failed: {ex.Message}");
        }
    }

    private async Task<ulong?> GetCoLocationCategoryIdAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
        var config = await db.DiscordConfigs.FindAsync(1);
        return config?.CoLocationCategoryId;
    }

    private async Task SaveCoLocationChannelAsync(CoLocationChannel channel)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
        db.CoLocationChannels.Attach(channel);
        db.Entry(channel).Property(c => c.DiscordChannelId).IsModified = true;
        await db.SaveChangesAsync();
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
