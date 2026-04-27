# MechanicalCataphract

MechanicalCataphract is a .NET 9.0 Windows desktop application for running **Cataphracts**, Sam Sorensen's asynchronous, real-time operational wargame. It is built with Avalonia UI, Entity Framework Core, SQLite, SkiaSharp, and an in-process Discord bot.

Cataphracts is focused on operational command: movement takes time, messengers carry information across the map, commanders have limited knowledge, and the referee manages the authoritative campaign state. For design context, see [Cataphracts Design Diary #1](https://samsorensen.blot.im/cataphracts-design-diary-1).

Players interact through Discord while the referee manages the campaign through the desktop GUI.

## Features

- Hex grid map with terrain, roads, rivers, weather, locations, population density, and faction control
- Army, brigade, commander, faction, navy, and ship management
- Supply, forage, morale, combat strength, scouting range, and carried cargo calculations
- Real-time world clock with direct time setting and hourly time advance
- Message movement across the map, including delayed in-game delivery
- Order tracking and Discord order capture from `:scroll:` messages
- Discord message capture from `:envelope:` messages
- Discord-managed faction roles, commander channels, co-location channels, and player-facing reports
- News/rumour propagation tools for referee-managed event delivery
- Map editing, overlays, forage selection, muster helper, and scouting report rendering

## Architecture

```text
Avalonia desktop UI
  MainWindow, HexMapView, MapEditorWindow, detail views

ViewModels
  HexMapViewModel plus focused coordinators for editing, path selection,
  Discord connection, news drops, muster, and entity detail view models

Services
  Map, army, commander, faction, faction rules, game state, orders,
  messages, time advance, weather, news, navies, pathfinding, calendar,
  game rules, and co-location channels

Discord integration
  DiscordBotService, DiscordChannelManager, DiscordMessageHandler,
  ArmyReportEmbedBuilder, NavyReportEmbedBuilder, ScoutingReportRenderer

Data
  Entity Framework Core with SQLite using wargame.db
```

Core persisted entities include `MapHex`, `TerrainType`, `LocationType`, `Weather`, `Faction`, `FactionRule`, `Army`, `Brigade`, `Commander`, `Message`, `Order`, `GameState`, `DiscordConfig`, `CoLocationChannel`, `NewsItem`, `Navy`, and `Ship`.

## Build & Run

Requires the .NET 9.0 SDK.

```bash
dotnet build Hexes/MechanicalCataphract.csproj
dotnet run --project Hexes/MechanicalCataphract.csproj
```

The SQLite database (`wargame.db`) is created automatically on first run. Delete it to reset local campaign state.

## Published Beta

The beta release is distributed as `MechanicalCataphract.zip`.

For non-technical users:

1. Download `MechanicalCataphract.zip`.
2. Create a new empty folder somewhere convenient, such as Desktop or Documents.
3. Right-click `MechanicalCataphract.zip` and choose **Extract All**.
4. Extract into the new empty folder.
5. Open the extracted folder.
6. Double-click `MechanicalCataphract.exe`.
7. Keep the extracted files together. Do not run the app directly from inside the zip file.

## Discord Bot Setup

The app includes an in-process Discord bot that lets players interact with the game from Discord and lets the referee mirror campaign structure into Discord roles and channels.

### 1. Create a Discord Bot

1. Go to the [Discord Developer Portal](https://discord.com/developers/applications).
2. Click **New Application** and give it a name.
3. Go to **Bot** in the sidebar.
4. Click **Reset Token** and copy the token.
5. Under **Privileged Gateway Intents**, enable **Message Content Intent**.

### 2. Invite the Bot to Your Server

1. In the Developer Portal, go to **OAuth2 > URL Generator**.
2. Select scopes: `bot`, `applications.commands`.
3. Select bot permissions: `Manage Roles`, `Manage Channels`, `Send Messages`, `Attach Files`, `Read Message History`.
4. Copy the generated URL and open it in your browser to invite the bot to your Discord server.

### 3. Get Your Guild ID

1. In Discord, go to **Settings > Advanced** and enable **Developer Mode**.
2. Right-click your server name and click **Copy Server ID**.

### 4. Connect in the App

1. Launch MechanicalCataphract.
2. In the right panel, click the **Discord** tab.
3. Paste your **Bot Token** and **Guild ID**.
4. Click **Connect**.

The status should change to `Connected`. If a saved token and guild ID already exist, the bot attempts to auto-start on app launch.

### What the Bot Does

Once connected, the bot manages Discord channels and roles to mirror the game state:

- **Faction created** in the GUI: creates or syncs a Discord role, category, and general channel.
- **Commander linked to Discord**: creates or syncs a private commander channel.
- **Commander faction changed**: moves or updates the commander's Discord resources.
- **Co-location channel created**: creates or syncs a shared channel for commanders who can communicate in the same place.
- **Message delivered**: sends in-game message content to the target commander's private channel.
- **Reports**: supports army, navy, and scouting report delivery from the Discord channel manager.
- **Incoming player messages**: parses `:envelope:` messages into game messages and `:scroll:` messages into orders.

The current bot service has slash-command infrastructure wired, but player message/order capture is handled through normal Discord messages using the formats above.

## Configurable Rules

Rules are split into global JSON files and per-faction overrides managed through the GUI.

### Global Game Rules (`Assets/game_rules.json`)

`Assets/game_rules.json` lives next to the executable in published builds. Edit it with a text editor and restart the app. No recompilation is needed.

Current top-level rule groups:

- `movement`: road/off-road costs, marching day, long-column limits, forced march, and river fording cost.
- `movementRates`: base rates for armies, messengers, and commanders.
- `supply`: wagon supply usage, forage multiplier, and daily usage hour.
- `armies`: daily report hour.
- `unitStats`: per-unit supply, carry, combat, scouting, column, and fording values.
- `news`: news propagation speed by travel type.
- `weather`: daily update hour and weather transition probabilities.
- `ships`: transport capacity, crew supply usage, river/sea movement, rowing bonus, and ship type capacity multipliers.

If `game_rules.json` is missing or malformed, the app falls back to built-in defaults and logs a warning to debug output.

### Global Calendar Rules (`Assets/calendar_rules.json`)

`Assets/calendar_rules.json` defines the campaign calendar:

- calendar name
- hours per day
- weekday names
- month names and lengths
- epoch year, month, day, and weekday

If the primary file is missing, the app tries `calendar_rules_default.json`, then a hardcoded Gregorian-style default. If the calendar JSON exists but is malformed or invalid, startup fails with a fatal validation error.

The app also validates that `marchDayStartHour` and `marchDayEndHour` from `game_rules.json` are within the configured `hoursPerDay`.

### Per-Faction Rules

Some rules vary by faction. These are managed through the **Faction Detail** panel in the GUI.

To add a faction rule:

1. Select a faction in the left panel.
2. Scroll to the **Game Rules** section.
3. Click **+ Add Rule**.
4. Choose a rule key and set the value.
5. The rule is saved immediately.

Available rule keys:

| Key | Value type | Effect |
|---|---|---|
| `OwnTerritoryMessengerMultiplier` | Multiplier, e.g. `1.5` | Messengers travelling through hexes controlled by this faction move faster. |
| `WagonCarryCapacity` | Integer, default `1000` | Overrides carry capacity per wagon for the faction. |

Rules are stored in the database and take effect on the next relevant calculation or time advance.

### Adding a New Rule Key

1. Add a `public const string` to `Services/FactionRuleKeys.cs`.
2. Add it to `AllKeys` and `Descriptions`.
3. Read it from the relevant service using `GetRuleValueAsync`, or preload and read through the cached faction rule APIs for hot loops.
4. The key appears automatically in the faction detail dropdown.

## Current Beta Caveats

- This project is intended for running Cataphracts, not as a generic wargame engine.
- Daily army supply consumption is currently not applied by time advance.
- Daily army, navy, and scouting report sends are present in the channel manager but not currently called from time advance.
- Discord slash-command plumbing exists, but player-facing message and order intake currently uses `:envelope:` and `:scroll:` messages.
- Test Discord setup, reconnect behavior, channel synchronization, message delivery, map editing, and time advancement before relying on a build for live play.
