using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetCord;
using NetCord.Rest;
using SkiaSharp;
using Hexes;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services.Calendar;

namespace MechanicalCataphract.Discord;

public class DiscordChannelManager : IDiscordChannelManager
{
    private readonly IDiscordBotService _botService;
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    /// <summary>
    /// Cached snapshot of guild state (channels + roles), set during sync and cleared after.
    /// Individual calls outside sync get a fresh snapshot via <see cref="GetOrFetchSnapshotAsync"/>.
    /// </summary>
    private GuildSnapshot? _currentSnapshot;

    public DiscordChannelManager(IDiscordBotService botService, IServiceProvider serviceProvider)
    {
        _botService = botService;
        _serviceProvider = serviceProvider;
    }

    // ── Guild snapshot: verify Discord resources actually exist ──────────

    private record GuildSnapshot(
        IReadOnlyList<IGuildChannel> Channels,
        IReadOnlyDictionary<ulong, Role> Roles);

    private async Task<GuildSnapshot?> FetchGuildSnapshotAsync()
    {
        var guildId = await GetGuildIdAsync();
        if (guildId == null) return null;
        var rest = _botService.Client!.Rest;
        var guild = await rest.GetGuildAsync(guildId.Value);
        var channels = await guild.GetChannelsAsync();
        return new GuildSnapshot(channels, guild.Roles);
    }

    private async Task<GuildSnapshot?> GetOrFetchSnapshotAsync()
    {
        if (_currentSnapshot != null) return _currentSnapshot;
        return await FetchGuildSnapshotAsync();
    }

    private static bool CategoryExists(GuildSnapshot snapshot, ulong id)
        => snapshot.Channels.Any(c => c.Id == id && c is CategoryGuildChannel);

    private static bool ChannelExists(GuildSnapshot snapshot, ulong id)
        => snapshot.Channels.Any(c => c.Id == id);

    private static bool RoleExists(GuildSnapshot snapshot, ulong id)
        => snapshot.Roles.ContainsKey(id);

    private static bool IsNotFoundError(Exception ex)
        => ex is RestException restEx && restEx.StatusCode == HttpStatusCode.NotFound;

    public async Task EnsureSentinelFactionResourcesAsync()
    {
        if (!_botService.IsConnected) return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
            var sentinel = await db.Factions.FindAsync(1);
            if (sentinel == null) return;

            // Verify stored IDs still exist in Discord
            var snapshot = await GetOrFetchSnapshotAsync();
            if (snapshot != null)
            {
                bool anyCleared = false;
                if (sentinel.DiscordRoleId.HasValue && !RoleExists(snapshot, sentinel.DiscordRoleId.Value))
                { sentinel.DiscordRoleId = null; anyCleared = true; }
                if (sentinel.DiscordCategoryId.HasValue && !CategoryExists(snapshot, sentinel.DiscordCategoryId.Value))
                { sentinel.DiscordCategoryId = null; anyCleared = true; }
                if (sentinel.DiscordChannelId.HasValue && !ChannelExists(snapshot, sentinel.DiscordChannelId.Value))
                { sentinel.DiscordChannelId = null; anyCleared = true; }
                if (anyCleared) await SaveFactionAsync(sentinel);
            }

            // Already has all Discord resources — nothing to do
            if (sentinel.DiscordRoleId.HasValue && sentinel.DiscordCategoryId.HasValue && sentinel.DiscordChannelId.HasValue)
                return;

            System.Diagnostics.Debug.WriteLine("[DiscordChannelManager] Creating Discord resources for 'No Faction' sentinel...");
            await OnFactionCreatedAsync(sentinel);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] EnsureSentinelFactionResources failed: {ex.Message}");
        }
    }

    public async Task SyncExistingEntitiesAsync()
    {
        if (!_botService.IsConnected) return;

        // Skip if a sync is already running (e.g. auto-start and manual connect racing).
        if (!await _syncLock.WaitAsync(0))
        {
            System.Diagnostics.Debug.WriteLine("[DiscordChannelManager] SyncExistingEntitiesAsync skipped — already in progress.");
            return;
        }

        try
        {
            // Fetch a snapshot of actual Discord state so Ensure* methods can verify
            // stored IDs still exist, without making per-entity API calls.
            _currentSnapshot = await FetchGuildSnapshotAsync();

            // Load all unsynced entities in a short-lived scope, then close it before processing.
            // Keeping the outer scope open across the loop causes SaveFactionAsync (and friends)
            // to hit a SQLite write-lock when they try to open their own write scope.
            List<Faction> unsyncedFactions;
            List<Commander> unsyncedCommanders;
            List<CoLocationChannel> unsyncedCoLocs;

            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();

                unsyncedFactions = await db.Factions
                    .Where(f => f.DiscordRoleId == null || f.DiscordCategoryId == null || f.DiscordChannelId == null)
                    .ToListAsync();

                unsyncedCommanders = await db.Commanders
                    .Where(c => c.DiscordUserId != null && c.DiscordChannelId == null)
                    .Include(c => c.Faction)
                    .ToListAsync();

                unsyncedCoLocs = await db.CoLocationChannels
                    .Where(c => c.DiscordChannelId == null)
                    .Include(c => c.Commanders)
                    .ToListAsync();
            } // scope disposed — connection closed before any writes

            // 0. Ensure Chorister role and Greek Chorus channel exist
            await EnsureChoristerRoleAsync();
            await EnsureChorusChannelAsync();

            // 1. Sync factions.
            foreach (var faction in unsyncedFactions)
                await OnFactionCreatedAsync(faction);

            // 2. Sync commanders.
            //    SetupCommanderDiscordAsync reloads faction from DB if DiscordCategoryId is null,
            //    so it picks up IDs just saved in step 1.
            foreach (var cmd in unsyncedCommanders)
                await OnCommanderCreatedAsync(cmd, cmd.Faction!);

            // 3. Ensure the co-location category, then sync individual channels.
            await EnsureCoLocationCategoryAsync();
            foreach (var ch in unsyncedCoLocs)
                await OnCoLocationChannelCreatedAsync(ch);

            // 4. Retrofit Chorister permissions onto all existing channels
            await SyncChoristerPermissionsAsync();

            System.Diagnostics.Debug.WriteLine("[DiscordChannelManager] SyncExistingEntitiesAsync complete.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] SyncExistingEntitiesAsync failed: {ex.Message}");
        }
        finally
        {
            _currentSnapshot = null;
            _syncLock.Release();
        }
    }

    public async Task OnFactionCreatedAsync(Faction faction)
    {
        if (!_botService.IsConnected) return;

        try
        {
            // Reload from DB — in-memory entity may be stale or from a previous partial attempt
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
                var existing = await db.Factions.FindAsync(faction.Id);
                if (existing != null)
                {
                    faction.DiscordRoleId = existing.DiscordRoleId;
                    faction.DiscordCategoryId = existing.DiscordCategoryId;
                    faction.DiscordChannelId = existing.DiscordChannelId;
                }
            }

            // Verify stored IDs still exist in Discord — clear any stale ones
            var snapshot = await GetOrFetchSnapshotAsync();
            if (snapshot != null)
            {
                bool anyCleared = false;
                if (faction.DiscordRoleId.HasValue && !RoleExists(snapshot, faction.DiscordRoleId.Value))
                { faction.DiscordRoleId = null; anyCleared = true; }
                if (faction.DiscordCategoryId.HasValue && !CategoryExists(snapshot, faction.DiscordCategoryId.Value))
                { faction.DiscordCategoryId = null; anyCleared = true; }
                if (faction.DiscordChannelId.HasValue && !ChannelExists(snapshot, faction.DiscordChannelId.Value))
                { faction.DiscordChannelId = null; anyCleared = true; }
                if (anyCleared) await SaveFactionAsync(faction);
            }

            // All resources already exist — nothing to do
            if (faction.DiscordRoleId.HasValue && faction.DiscordCategoryId.HasValue && faction.DiscordChannelId.HasValue)
                return;
            var rest = _botService.Client!.Rest;
            var guildId = await GetGuildIdAsync();
            if (guildId == null) return;

            // 1. Create faction role (skip if already exists from a partial previous attempt)
            if (!faction.DiscordRoleId.HasValue)
            {
                var color = ParseColor(faction.ColorHex);
                var role = await rest.CreateGuildRoleAsync(guildId.Value, new RoleProperties
                {
                    Name = faction.Name,
                    Color = color,
                });
                faction.DiscordRoleId = role.Id;
                await SaveFactionAsync(faction);
            }

            // 2. Create channel category (skip if already exists)
            if (!faction.DiscordCategoryId.HasValue)
            {
                var category = await rest.CreateGuildChannelAsync(guildId.Value,
                    new GuildChannelProperties(faction.Name, ChannelType.CategoryChannel));
                faction.DiscordCategoryId = category.Id;
                await SaveFactionAsync(faction);
            }

            // 3. Apply Chorister read-only overwrite to the category (non-fatal)
            try
            {
                var categoryChoristerOverwrite = await BuildChoristerOverwriteAsync();
                if (categoryChoristerOverwrite != null)
                    await rest.ModifyGuildChannelPermissionsAsync(faction.DiscordCategoryId!.Value, categoryChoristerOverwrite);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Chorister category overwrite failed (non-fatal): {ex.Message}");
            }

            // 4. Create read-only general channel (skip if already exists)
            if (!faction.DiscordChannelId.HasValue)
            {
                var channelOverwrites = new List<PermissionOverwriteProperties>
                {
                    // Deny @everyone from seeing the channel
                    new(guildId.Value, PermissionOverwriteType.Role)
                    {
                        Denied = Permissions.ViewChannel,
                    },
                    // Allow faction role to view but not send or create threads
                    new(faction.DiscordRoleId!.Value, PermissionOverwriteType.Role)
                    {
                        Allowed = Permissions.ViewChannel | Permissions.ReadMessageHistory,
                        Denied = Permissions.SendMessages | Permissions.CreatePublicThreads
                               | Permissions.CreatePrivateThreads | Permissions.SendMessagesInThreads,
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

                // Chorister role: read-only observer access
                var choristerOverwrite = await BuildChoristerOverwriteAsync();
                if (choristerOverwrite != null)
                    channelOverwrites.Add(choristerOverwrite);

                var channel = await rest.CreateGuildChannelAsync(guildId.Value,
                    new GuildChannelProperties($"{faction.Name.ToLowerInvariant().Replace(' ', '-')}-general", ChannelType.TextGuildChannel)
                    {
                        ParentId = faction.DiscordCategoryId,
                        PermissionOverwrites = channelOverwrites,
                    });
                faction.DiscordChannelId = channel.Id;
                await SaveFactionAsync(faction);
            }

            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Faction '{faction.Name}' — role {faction.DiscordRoleId}, category {faction.DiscordCategoryId}, channel {faction.DiscordChannelId}");
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
            // Idempotency: check DB in case in-memory entity is stale
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
                var existing = await db.Commanders.FindAsync(commander.Id);
                if (existing?.DiscordChannelId.HasValue == true)
                {
                    // Verify the channel still exists in Discord
                    var snapshot = await GetOrFetchSnapshotAsync();
                    if (snapshot != null && ChannelExists(snapshot, existing.DiscordChannelId!.Value))
                    {
                        commander.DiscordChannelId = existing.DiscordChannelId;
                        return;
                    }
                    // Stale — clear and recreate
                    existing.DiscordChannelId = null;
                    await SaveCommanderAsync(existing);
                }
            }
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
                // Allow the commander's Discord user read+write, but deny thread creation
                new(commander.DiscordUserId.Value, PermissionOverwriteType.User)
                {
                    Allowed = Permissions.ViewChannel | Permissions.SendMessages
                            | Permissions.ReadMessageHistory,
                    Denied = Permissions.CreatePublicThreads | Permissions.CreatePrivateThreads,
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

            // Chorister role: read-only observer access
            var choristerOverwrite = await BuildChoristerOverwriteAsync();
            if (choristerOverwrite != null)
                channelOverwrites.Add(choristerOverwrite);

            var channel = await rest.CreateGuildChannelAsync(guildId.Value,
                new GuildChannelProperties($"cmd-{commander.Name.ToLowerInvariant().Replace(' ', '-')}", ChannelType.TextGuildChannel)
                {
                    ParentId = faction.DiscordCategoryId,
                    PermissionOverwrites = channelOverwrites,
                });
            commander.DiscordChannelId = channel.Id;

            // 2. Persist channel ID immediately — before any further Discord calls that could throw
            await SaveCommanderAsync(commander);

            // 3. Assign faction role
            if (faction.DiscordRoleId.HasValue)
            {
                try
                {
                    await rest.AddGuildUserRoleAsync(guildId.Value, commander.DiscordUserId.Value, faction.DiscordRoleId.Value);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[DiscordChannelManager] Role assignment for '{commander.Name}' failed (non-fatal): {ex.Message}");
                }
            }

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

        // Reload stale faction data — ViewModel may have cached these before Discord IDs were set
        if (oldFaction.DiscordRoleId == null)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
            var freshOld = await db.Factions.FindAsync(oldFaction.Id);
            if (freshOld?.DiscordRoleId != null)
                oldFaction.DiscordRoleId = freshOld.DiscordRoleId;
        }

        if (newFaction.DiscordCategoryId == null || newFaction.DiscordRoleId == null)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
            var freshNew = await db.Factions.FindAsync(newFaction.Id);
            if (freshNew != null)
            {
                newFaction.DiscordCategoryId = freshNew.DiscordCategoryId;
                newFaction.DiscordRoleId = freshNew.DiscordRoleId;
                newFaction.DiscordChannelId = freshNew.DiscordChannelId;
            }
        }

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

                // Re-apply Chorister overwrite — category move can reset channel permissions
                var choristerOverwrite = await BuildChoristerOverwriteAsync();
                if (choristerOverwrite != null)
                    await rest.ModifyGuildChannelPermissionsAsync(commander.DiscordChannelId.Value, choristerOverwrite);
            }

            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Commander '{commander.Name}' moved from '{oldFaction.Name}' to '{newFaction.Name}'.");
        }
        catch (Exception ex)
        {
            if (IsNotFoundError(ex) && commander.DiscordChannelId.HasValue)
            {
                commander.DiscordChannelId = null;
                await SaveCommanderAsync(commander);
                System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Cleared stale commander channel ID during faction change: {ex.Message}");
            }
            else
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
            if (IsNotFoundError(ex))
            {
                commander.DiscordChannelId = null;
                await SaveCommanderAsync(commander);
                System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Cleared stale commander channel ID: {ex.Message}");
            }
            else
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
                if (IsNotFoundError(ex))
                {
                    faction.DiscordRoleId = null;
                    await SaveFactionAsync(faction);
                    System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Cleared stale role ID: {ex.Message}");
                }
                else
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
                if (IsNotFoundError(ex))
                {
                    faction.DiscordCategoryId = null;
                    await SaveFactionAsync(faction);
                    System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Cleared stale category ID: {ex.Message}");
                }
                else
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
                    o.Name = $"{faction.Name.ToLowerInvariant().Replace(' ', '-')}-general";
                });
                System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Channel {faction.DiscordChannelId} updated.");
            }
            catch (Exception ex)
            {
                if (IsNotFoundError(ex))
                {
                    faction.DiscordChannelId = null;
                    await SaveFactionAsync(faction);
                    System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Cleared stale channel ID: {ex.Message}");
                }
                else
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
            if (config == null || config.GuildId == null) return;

            var rest = _botService.Client!.Rest;
            var guildId = config.GuildId.Value;

            // Verify stored category still exists in Discord
            if (config.CoLocationCategoryId.HasValue)
            {
                var snapshot = await GetOrFetchSnapshotAsync();
                if (snapshot != null && CategoryExists(snapshot, config.CoLocationCategoryId.Value))
                    return;

                // Stored ID is stale or no longer a category
                config.CoLocationCategoryId = null;
            }

            var category = await rest.CreateGuildChannelAsync(guildId,
                new GuildChannelProperties("Co-Location", ChannelType.CategoryChannel));

            config.CoLocationCategoryId = category.Id;
            await db.SaveChangesAsync();

            System.Diagnostics.Debug.WriteLine(
                $"[DiscordChannelManager] Co-Location category ensured: {category.Id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[DiscordChannelManager] EnsureCoLocationCategory failed: {ex.Message}");
        }
    }


    public async Task OnCoLocationChannelCreatedAsync(CoLocationChannel channel)
    {
        if (!_botService.IsConnected) return;

        try
        {
            // Idempotency: check DB in case in-memory entity is stale
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
                var existing = await db.CoLocationChannels.FindAsync(channel.Id);
                if (existing?.DiscordChannelId.HasValue == true)
                {
                    // Verify the channel still exists in Discord
                    var snapshot = await GetOrFetchSnapshotAsync();
                    if (snapshot != null && ChannelExists(snapshot, existing.DiscordChannelId!.Value))
                    {
                        channel.DiscordChannelId = existing.DiscordChannelId;
                        return;
                    }
                    // Stale — clear and recreate
                    existing.DiscordChannelId = null;
                    await SaveCoLocationChannelAsync(existing);
                }
            }

            var rest = _botService.Client!.Rest;
            var guildId = await GetGuildIdAsync();
            if (guildId == null) return;

            await EnsureCoLocationCategoryAsync();

            var categoryId = await GetCoLocationCategoryIdAsync();
            if (categoryId == null) return;

            var channelOverwrites = new List<PermissionOverwriteProperties>
        {
            new(guildId.Value, PermissionOverwriteType.Role)
            {
                Denied = Permissions.ViewChannel,
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

            var choristerOverwrite = await BuildChoristerOverwriteAsync();
            if (choristerOverwrite != null)
                channelOverwrites.Add(choristerOverwrite);

            var discordChannel = await rest.CreateGuildChannelAsync(guildId.Value,
                new GuildChannelProperties($"coloc-{channel.Name.ToLowerInvariant().Replace(' ', '-')}", ChannelType.TextGuildChannel)
                {
                    ParentId = categoryId,
                    PermissionOverwrites = channelOverwrites,
                });

            channel.DiscordChannelId = discordChannel.Id;
            await SaveCoLocationChannelAsync(channel);

            System.Diagnostics.Debug.WriteLine(
                $"[DiscordChannelManager] Co-location channel '{channel.Name}' created: {discordChannel.Id}");
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
                    Denied = Permissions.CreatePublicThreads
                           | Permissions.CreatePrivateThreads | Permissions.SendMessagesInThreads
                });
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Commander '{commander.Name}' added to co-location '{channel.Name}'.");
        }
        catch (Exception ex)
        {
            if (IsNotFoundError(ex))
            {
                channel.DiscordChannelId = null;
                await SaveCoLocationChannelAsync(channel);
                System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Cleared stale co-location channel ID: {ex.Message}");
            }
            else
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
            var calendarService = scope.ServiceProvider.GetRequiredService<ICalendarService>();

            var commander = await commanderService.GetCommanderWithArmiesAsync(commanderId);
            if (commander == null || !commander.DiscordChannelId.HasValue) return;
            if (!_botService.IsConnected) return;

            var gameState = await gameStateService.GetGameStateAsync();
            string formattedTime = calendarService.FormatDateTime(gameState.CurrentWorldHour);

            var embeds = commander.CommandedArmies
                .Select(army => ArmyReportEmbedBuilder.BuildArmyReport(army, commander, formattedTime))
                .ToList();

            if (embeds.Count == 0) return;

            // Discord allows up to 10 embeds per message — batch all army reports into one call
            // instead of one API call per army, reducing message API usage as armies scale.
            const int MaxEmbedsPerMessage = 10;
            var rest = _botService.Client!.Rest;
            for (int i = 0; i < embeds.Count; i += MaxEmbedsPerMessage)
            {
                var batch = embeds.GetRange(i, Math.Min(MaxEmbedsPerMessage, embeds.Count - i));
                await rest.SendMessageAsync(commander.DiscordChannelId.Value,
                    new NetCord.Rest.MessageProperties { Embeds = batch });
            }

            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Army reports sent to '{commander.Name}': {embeds.Count} report(s).");
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

    public async Task SendNavyReportsToCommanderAsync(int commanderId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var commanderService = scope.ServiceProvider.GetRequiredService<Services.ICommanderService>();
            var navyService = scope.ServiceProvider.GetRequiredService<Services.INavyService>();
            var gameStateService = scope.ServiceProvider.GetRequiredService<Services.IGameStateService>();
            var calendarService = scope.ServiceProvider.GetRequiredService<ICalendarService>();

            var commander = await commanderService.GetByIdAsync(commanderId);
            if (commander == null || !commander.DiscordChannelId.HasValue) return;
            if (!_botService.IsConnected) return;

            var navies = await navyService.GetNaviesWithDetailsByCommanderAsync(commanderId);
            if (navies.Count == 0) return;

            var gameState = await gameStateService.GetGameStateAsync();
            string formattedTime = calendarService.FormatDateTime(gameState.CurrentWorldHour);

            var embeds = navies
                .Select(navy => NavyReportEmbedBuilder.BuildNavyReport(navy, commander, formattedTime))
                .ToList();

            // Discord allows up to 10 embeds per message — batch all navy reports into one call
            const int MaxEmbedsPerMessage = 10;
            var rest = _botService.Client!.Rest;
            for (int i = 0; i < embeds.Count; i += MaxEmbedsPerMessage)
            {
                var batch = embeds.GetRange(i, Math.Min(MaxEmbedsPerMessage, embeds.Count - i));
                await rest.SendMessageAsync(commander.DiscordChannelId.Value,
                    new NetCord.Rest.MessageProperties { Embeds = batch });
            }

            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Navy reports sent to '{commander.Name}': {embeds.Count} report(s).");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] SendNavyReportsToCommander failed: {ex.Message}");
        }
    }

    public async Task SendAllNavyReportsAsync()
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
                await SendNavyReportsToCommanderAsync(id);
            }

            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Daily navy reports sent to {commanderIds.Count} commanders.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] SendAllNavyReports failed: {ex.Message}");
        }
    }

    public async Task SendScoutingReportAsync(Commander target, Stream imageStream, string armyName, string? weatherName)
    {
        if (!_botService.IsConnected) return;
        if (!target.DiscordChannelId.HasValue) return;

        try
        {
            var rest = _botService.Client!.Rest;
            imageStream.Position = 0;
            var attachment = new AttachmentProperties($"scouting-report-{armyName.ToLowerInvariant().Replace(' ', '-')}.png", imageStream);
            var weatherLine = weatherName != null ? $"\nWeather: {weatherName}" : "";
            await rest.SendMessageAsync(target.DiscordChannelId.Value, new MessageProperties
            {
                Content = $"Scouting report for **{armyName}**{weatherLine}",
                Attachments = [attachment]
            });
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Scouting report sent to '{target.Name}' for army '{armyName}'.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] SendScoutingReport failed: {ex.Message}");
        }
    }

    public async Task SendAllScoutingReportsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
            var mapService = scope.ServiceProvider.GetRequiredService<Services.IMapService>();
            var armyService = scope.ServiceProvider.GetRequiredService<Services.IArmyService>();
            var navyService = scope.ServiceProvider.GetRequiredService<Services.INavyService>();

            if (!_botService.IsConnected) return;

            // Load shared map data once for all reports
            var allHexes = await mapService.GetAllHexesAsync();
            var terrainTypes = await mapService.GetTerrainTypesAsync();
            var locationTypes = await mapService.GetLocationTypesAsync();

            // Load all armies/navies for the renderer (includes Faction for marker colors)
            var allArmies = await armyService.GetAllAsync();
            var allNavies = await navyService.GetAllAsync();

            // Load commanders with Discord channels and their armies + brigades
            var commanders = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(db.Commanders
                    .Where(c => c.DiscordChannelId != null)
                    .Include(c => c.CommandedArmies)
                        .ThenInclude(a => a.Brigades));

            int reportsSent = 0;
            foreach (var commander in commanders)
            {
                foreach (var army in commander.CommandedArmies)
                {
                    if (!army.CoordinateQ.HasValue || !army.CoordinateR.HasValue) continue;
                    if (army.Brigades == null || army.Brigades.Count == 0) continue;

                    try
                    {
                        int scoutingRange = army.Brigades.Max(b => b.ScoutingRange);
                        var centerHex = new Hex(army.CoordinateQ.Value, army.CoordinateR.Value,
                            -army.CoordinateQ.Value - army.CoordinateR.Value);

                        var hexesInRange = allHexes
                            .Where(h => centerHex.Distance(h.ToHex()) <= scoutingRange)
                            .ToList();

                        var armiesInRange = allArmies
                            .Where(a => a.CoordinateQ.HasValue && a.CoordinateR.HasValue &&
                                centerHex.Distance(new Hex(a.CoordinateQ.Value, a.CoordinateR.Value,
                                    -a.CoordinateQ.Value - a.CoordinateR.Value)) <= scoutingRange)
                            .ToList();

                        var naviesInRange = allNavies
                            .Where(n => n.CoordinateQ.HasValue && n.CoordinateR.HasValue &&
                                centerHex.Distance(new Hex(n.CoordinateQ.Value, n.CoordinateR.Value,
                                    -n.CoordinateQ.Value - n.CoordinateR.Value)) <= scoutingRange)
                            .ToList();

                        using var bitmap = ScoutingReportRenderer.RenderScoutingReport(
                            hexesInRange, terrainTypes, locationTypes,
                            armiesInRange, naviesInRange, centerHex, scoutingRange);

                        using var image = SKImage.FromBitmap(bitmap);
                        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                        using var stream = new System.IO.MemoryStream();
                        data.SaveTo(stream);
                        stream.Position = 0;

                        var centerMapHex = hexesInRange
                            .FirstOrDefault(h => h.Q == centerHex.q && h.R == centerHex.r);
                        string? weatherName = centerMapHex?.Weather?.Name;

                        await SendScoutingReportAsync(commander, stream, army.Name, weatherName);
                        reportsSent++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[DiscordChannelManager] Scouting report for '{army.Name}' failed: {ex.Message}");
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine(
                $"[DiscordChannelManager] Daily scouting reports sent: {reportsSent}.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[DiscordChannelManager] SendAllScoutingReports failed: {ex.Message}");
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
        await db.CoLocationChannels
            .Where(c => c.Id == channel.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.DiscordChannelId, channel.DiscordChannelId));
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
                    | Permissions.ReadMessageHistory | Permissions.ManageChannels
                    | Permissions.CreatePublicThreads | Permissions.CreatePrivateThreads
                    | Permissions.SendMessagesInThreads,
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

    private async Task<ulong?> GetChoristerRoleIdAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
        var config = await db.DiscordConfigs.FindAsync(1);
        return config?.ChoristerRoleId;
    }

    private async Task<PermissionOverwriteProperties?> BuildChoristerOverwriteAsync()
    {
        var choristerRoleId = await GetChoristerRoleIdAsync();
        if (choristerRoleId == null) return null;

        return new PermissionOverwriteProperties(choristerRoleId.Value, PermissionOverwriteType.Role)
        {
            Allowed = Permissions.ViewChannel | Permissions.ReadMessageHistory,
            Denied = Permissions.SendMessages | Permissions.AddReactions
                   | Permissions.CreatePublicThreads | Permissions.CreatePrivateThreads
                   | Permissions.SendMessagesInThreads,
        };
    }

    private async Task SaveFactionAsync(Faction faction)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
        await db.Factions
            .Where(f => f.Id == faction.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.DiscordRoleId, faction.DiscordRoleId)
                .SetProperty(f => f.DiscordCategoryId, faction.DiscordCategoryId)
                .SetProperty(f => f.DiscordChannelId, faction.DiscordChannelId));
    }

    private async Task SaveCommanderAsync(Commander commander)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
        await db.Commanders
            .Where(c => c.Id == commander.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.DiscordChannelId, commander.DiscordChannelId));
    }

    public async Task EnsureChoristerRoleAsync()
    {
        if (!_botService.IsConnected) return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
            var config = await db.DiscordConfigs.FindAsync(1);
            if (config == null || config.GuildId == null) return;

            // Verify stored role still exists in Discord
            if (config.ChoristerRoleId.HasValue)
            {
                var snapshot = await GetOrFetchSnapshotAsync();
                if (snapshot != null && RoleExists(snapshot, config.ChoristerRoleId.Value))
                    return;
                config.ChoristerRoleId = null;
                await db.SaveChangesAsync();
            }

            var rest = _botService.Client!.Rest;
            var role = await rest.CreateGuildRoleAsync(config.GuildId.Value, new RoleProperties
            {
                Name = "Chorister",
                Color = new Color(0x9B, 0x59, 0xB6), // Purple
            });
            config.ChoristerRoleId = role.Id;
            await db.SaveChangesAsync();

            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] Chorister role created: {role.Id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] EnsureChoristerRole failed: {ex.Message}");
        }
    }

    public async Task EnsureChorusChannelAsync()
    {
        if (!_botService.IsConnected) return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
            var config = await db.DiscordConfigs.FindAsync(1);
            if (config == null || config.GuildId == null) return;

            var rest = _botService.Client!.Rest;
            var guildId = config.GuildId.Value;

            // Verify stored IDs still exist in Discord
            var snapshot = await GetOrFetchSnapshotAsync();
            if (snapshot != null)
            {
                if (config.ChorusCategoryId.HasValue && !CategoryExists(snapshot, config.ChorusCategoryId.Value))
                    config.ChorusCategoryId = null;
                if (config.ChorusChannelId.HasValue && !ChannelExists(snapshot, config.ChorusChannelId.Value))
                    config.ChorusChannelId = null;
            }

            // 1. Create category if needed
            if (!config.ChorusCategoryId.HasValue)
            {
                var category = await rest.CreateGuildChannelAsync(guildId,
                    new GuildChannelProperties("Greek Chorus", ChannelType.CategoryChannel));
                config.ChorusCategoryId = category.Id;
            }

            // 2. Create channel if needed
            if (!config.ChorusChannelId.HasValue)
            {
                var overwrites = new List<PermissionOverwriteProperties>
                {
                    // Deny @everyone
                    new(guildId, PermissionOverwriteType.Role)
                    {
                        Denied = Permissions.ViewChannel,
                    },
                };

                AddBotOverwrite(overwrites);

                // Chorister role: full read/write access in this channel
                if (config.ChoristerRoleId.HasValue)
                {
                    overwrites.Add(new(config.ChoristerRoleId.Value, PermissionOverwriteType.Role)
                    {
                        Allowed = Permissions.ViewChannel | Permissions.SendMessages
                                | Permissions.ReadMessageHistory | Permissions.AddReactions
                                | Permissions.CreatePublicThreads | Permissions.SendMessagesInThreads,
                    });
                }

                // Admin role access
                if (config.AdminRoleId.HasValue)
                {
                    overwrites.Add(new(config.AdminRoleId.Value, PermissionOverwriteType.Role)
                    {
                        Allowed = Permissions.ViewChannel | Permissions.SendMessages
                                | Permissions.ReadMessageHistory | Permissions.ManageChannels,
                    });
                }

                var channel = await rest.CreateGuildChannelAsync(guildId,
                    new GuildChannelProperties("greek-chorus", ChannelType.TextGuildChannel)
                    {
                        ParentId = config.ChorusCategoryId,
                        PermissionOverwrites = overwrites,
                    });
                config.ChorusChannelId = channel.Id;
            }

            await db.SaveChangesAsync();
            System.Diagnostics.Debug.WriteLine("[DiscordChannelManager] Greek Chorus resources ensured.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] EnsureChorusChannel failed: {ex.Message}");
        }
    }

    public async Task SyncChoristerPermissionsAsync()
    {
        if (!_botService.IsConnected) return;
        var choristerOverwrite = await BuildChoristerOverwriteAsync();
        if (choristerOverwrite == null) return;

        try
        {
            var rest = _botService.Client!.Rest;
            List<ulong> channelIds;

            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();

                // Include faction categories (so child channels inherit Chorister deny)
                var factionCategoryIds = await db.Factions
                    .Where(f => f.DiscordCategoryId != null)
                    .Select(f => f.DiscordCategoryId!.Value)
                    .ToListAsync();

                var factionChannelIds = await db.Factions
                    .Where(f => f.DiscordChannelId != null)
                    .Select(f => f.DiscordChannelId!.Value)
                    .ToListAsync();

                var commanderChannelIds = await db.Commanders
                    .Where(c => c.DiscordChannelId != null)
                    .Select(c => c.DiscordChannelId!.Value)
                    .ToListAsync();

                var coLocChannelIds = await db.CoLocationChannels
                    .Where(c => c.DiscordChannelId != null)
                    .Select(c => c.DiscordChannelId!.Value)
                    .ToListAsync();

                // Include the co-location category itself
                var config = await db.DiscordConfigs.FindAsync(1);
                var coLocCategoryId = config?.CoLocationCategoryId;

                channelIds = factionCategoryIds
                    .Concat(factionChannelIds)
                    .Concat(commanderChannelIds)
                    .Concat(coLocChannelIds)
                    .ToList();

                if (coLocCategoryId.HasValue)
                    channelIds.Add(coLocCategoryId.Value);
            }

            foreach (var channelId in channelIds)
            {
                try
                {
                    await rest.ModifyGuildChannelPermissionsAsync(channelId, choristerOverwrite);
                }
                catch (Exception ex)
                {
                    if (IsNotFoundError(ex))
                        System.Diagnostics.Debug.WriteLine(
                            $"[DiscordChannelManager] Skipping stale channel {channelId} during Chorister sync: {ex.Message}");
                    else
                        System.Diagnostics.Debug.WriteLine(
                            $"[DiscordChannelManager] Chorister overwrite failed for channel {channelId}: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine(
                $"[DiscordChannelManager] Chorister permissions synced to {channelIds.Count} channels.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordChannelManager] SyncChoristerPermissions failed: {ex.Message}");
        }
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
