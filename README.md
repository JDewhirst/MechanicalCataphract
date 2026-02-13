# MechanicalCataphract

A .NET 9.0 desktop wargame application with hex-based tactical maps, army management, and turn-based gameplay. Built with Avalonia UI and SQLite.

Players command armies through a Discord bot interface while a referee manages the game through the desktop GUI.

## Features

- Hex grid map with terrain, roads, rivers, weather, and named locations
- Army and brigade management with supply, morale, and combat strength tracking
- Commander system with faction assignments and Discord user linking
- Turn-based time advance with movement, supply consumption, and message delivery
- In-game messaging system between commanders (messages travel across the map)
- Order tracking and processing
- Discord bot integration for player interaction

## Build & Run

Requires .NET 9.0 SDK.

```bash
dotnet build Hexes/MechanicalCataphract.csproj
dotnet run --project Hexes/MechanicalCataphract.csproj
```

The SQLite database (`wargame.db`) is created automatically on first run. Delete it to reset.

## Discord Bot Setup

The app includes an in-process Discord bot that lets players interact with the game from Discord.

### 1. Create a Discord Bot

1. Go to the [Discord Developer Portal](https://discord.com/developers/applications)
2. Click **New Application**, give it a name
3. Go to **Bot** in the sidebar
4. Click **Reset Token** and copy the token (you'll need it shortly)
5. Under **Privileged Gateway Intents**, enable **Message Content Intent**

### 2. Invite the Bot to Your Server

1. In the Developer Portal, go to **OAuth2 > URL Generator**
2. Select scopes: `bot`, `applications.commands`
3. Select bot permissions: `Manage Roles`, `Manage Channels`, `Send Messages`, `Attach Files`, `Read Message History`
4. Copy the generated URL and open it in your browser to invite the bot to your Discord server

### 3. Get Your Guild ID

1. In Discord, go to **Settings > Advanced** and enable **Developer Mode**
2. Right-click your server name and click **Copy Server ID**

### 4. Connect in the App

1. Launch MechanicalCataphract
2. In the right panel, click the **Discord** tab
3. Paste your **Bot Token** and **Guild ID**
4. Click **Connect**

The status should change to "Connected". The bot will now appear online in your Discord server.

### What the Bot Does

Once connected, the bot manages Discord channels and roles to mirror the game state:

- **Faction created** in the GUI: a Discord role and channel category are created
- **Commander added**: a private channel is created for that player in their faction's category
- **Time advance**: scouting reports (map renders) and weather updates are sent to commander channels
- **Message delivered**: in-game message content appears in the target commander's Discord channel
- **Slash commands**: players can query army stats and send in-game messages from Discord
