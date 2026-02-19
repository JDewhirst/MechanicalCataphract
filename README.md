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

## Configurable Game Rules

Game rules are split into two tiers: global constants in a JSON file, and per-faction overrides managed through the GUI.

### Tier 1 — Global Rules (`game_rules.json`)

All numeric game constants live in `Assets/game_rules.json` next to the executable. Open it in any text editor and change values, then restart the app. No recompilation needed.

```json
{
  "movement": {
    "roadCost": 6,
    "offRoadCost": 12,
    "armyMovementMultiplier": 1.5,
    "marchDayStartHour": 8,
    "marchDayEndHour": 20,
    "longColumnThreshold": 6,
    "longColumnSpeedCap": 0.5,
    "forcedMarchMultiplier": 2.0,
    "riverFordingCostPerColumnUnit": 6
  },
  "movementRates": {
    "armyBaseRate": 1.0,
    "messengerBaseRate": 2.0,
    "commanderBaseRate": 2.0
  },
  "supply": {
    "wagonSupplyMultiplier": 10,
    "forageMultiplierPerDensity": 500
  },
  "unitStats": {
    "infantry":    { "supplyConsumptionPerMan": 1,  "carryCapacityPerMan": 15, "combatPowerPerMan": 1, "scoutingRange": 1, "marchingColumnCapacity": 5000, "countsForFordingLength": true  },
    "skirmishers": { "supplyConsumptionPerMan": 1,  "carryCapacityPerMan": 15, "combatPowerPerMan": 1, "scoutingRange": 1, "marchingColumnCapacity": 5000, "countsForFordingLength": true  },
    "cavalry":     { "supplyConsumptionPerMan": 10, "carryCapacityPerMan": 75, "combatPowerPerMan": 2, "scoutingRange": 2, "marchingColumnCapacity": 2000, "countsForFordingLength": false }
  }
}
```

**Movement fields**

| Field | Default | Meaning |
|---|---|---|
| `roadCost` | 6 | Abstract cost to cross one hex on a road |
| `offRoadCost` | 12 | Abstract cost to cross one hex off-road |
| `armyMovementMultiplier` | 1.5 | Army path cost multiplier (armies pay 50% more per hex than messengers) |
| `marchDayStartHour` | 8 | Hour armies begin marching (24-hour clock) |
| `marchDayEndHour` | 20 | Hour armies halt for the night |
| `longColumnThreshold` | 6 | Column-length units above which the speed cap applies |
| `longColumnSpeedCap` | 0.5 | Effective movement rate for armies exceeding the column threshold |
| `forcedMarchMultiplier` | 2.0 | Rate multiplier when an army is forced marching |
| `riverFordingCostPerColumnUnit` | 6 | Extra movement cost per column-length unit when crossing a river without a bridge |

**Movement rate fields** — base speed (in abstract units per hour) for each entity type.

**Supply fields**

| Field | Default | Meaning |
|---|---|---|
| `wagonSupplyMultiplier` | 10 | Supply consumed per wagon per day |
| `forageMultiplierPerDensity` | 500 | Supply gained per point of population density when foraging a hex |

**Unit stat fields** — applied per man for each unit type.

| Field | Meaning |
|---|---|
| `supplyConsumptionPerMan` | Daily supply cost per soldier |
| `carryCapacityPerMan` | Max supply each soldier can carry |
| `combatPowerPerMan` | Contribution to army combat strength |
| `scoutingRange` | Hex radius this unit type can scout |
| `marchingColumnCapacity` | Men per abstract column unit (affects long-column penalty) |
| `countsForFordingLength` | Whether this unit type adds to the river fording cost |

If the file is missing or malformed the app falls back to the defaults shown above and logs a warning to the debug output.

---

### Tier 2 — Per-Faction Rules (GUI)

Some rules vary by faction. These are managed through the **Faction Detail** panel in the GUI — no file editing required.

**How to add a faction rule:**

1. Select a faction in the left panel to open its detail view
2. Scroll to the **Game Rules** section at the bottom
3. Click **+ Add Rule**
4. Choose a rule key from the dropdown and set the value
5. The rule is saved immediately

**How to remove a faction rule:**

Click the **✕** button on any rule row. The rule is deleted immediately.

**Available rule keys**

| Key | Value type | Effect |
|---|---|---|
| `OwnTerritoryMessengerMultiplier` | Multiplier (e.g. `1.5`) | Messengers travelling through hexes controlled by this faction move faster. A value of `1.5` means 50% faster, `2.0` means twice as fast. Only applies when the messenger's sender belongs to this faction. |

Rules are stored in the database and take effect on the next time advance — no restart needed.

---

### Adding a New Rule Key (for developers)

1. Add a `public const string` to `Services/FactionRuleKeys.cs` and add it to `AllKeys` and `Descriptions`
2. Read it in the relevant service using `await _factionRuleService.GetRuleValueAsync(factionId, FactionRuleKeys.YourKey, defaultValue)`, or for hot loops: preload with `PreloadForFactionAsync` then read synchronously with `GetCachedRuleValue`
3. The new key will appear automatically in the faction detail dropdown
