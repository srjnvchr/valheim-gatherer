# Valheim Gatherer

Automatically pins resources on your map as you explore Valheim, the same way you would by hand.

## Objective

While exploring, players stop to mark ore veins, berry patches, tar pits and lore stones on the map so they can come back later. Valheim Gatherer does this for you. When you come within range of a resource, it adds a normal saved map pin with a clear label such as `Copper`, `Raspberries` or `Tar Pit`.

Goals:
- **Only pin what you've been near.** Nothing is revealed through fog of war. A pin appears only after you come within the discovery radius (50 m by default).
- **Keep the map clean.** A cluster of the same resource gets one pin, and pins you placed yourself with the same name are respected.
- **Keep pins accurate.** Pins for ore and one-off pickups are removed when you mine out or collect the last of them. Plant pins stay, since plants grow back.
- **Resources only.** Creatures are never pinned, and neither are crops or mushrooms inside your own base.
- **Client-side only.** The server and other players don't need the mod.

### Tracked by default

| Category | Labels |
| --- | --- |
| Ores | Copper, Tin, Silver, Obsidian, Flametal (meteorite), Scrap Iron (mud piles), Chitin (leviathan), Giant Remains, Soft Tissue |
| Pickables | Tin, Dragon Egg, Crystal, Black Core |
| Plants | Raspberries, Blueberries, Cloudberries, Mushrooms (all kinds), Smoke Puffs, Thistle, Dandelion, Carrot/Turnip/Onion Seeds, Barley, Flax, Fiddlehead, Royal Jelly, Beehive |
| InfoStones | Runestone, Vegvisir |
| Locations | Tar Pit |

You can add your own rules or disable any of these in the config (see [Configuration](#configuration)).

## Installation

### Requirements
- Valheim (PC)
- [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/), installed manually or through a mod manager (Thunderstore Mod Manager or r2modman)

### Option A: Mod manager (Thunderstore / r2modman)
1. Build the mod (see [Building from source](#building-from-source)) or get a built `ValheimGatherer.dll`.
2. In the mod manager, open your Valheim profile and choose **Settings → Browse profile folder**.
3. Copy `ValheimGatherer.dll` into `BepInEx/plugins/ValheimGatherer/` inside that folder.
4. Launch the game with **Start modded**.

### Option B: Manual BepInEx install
1. Install BepInExPack for Valheim into your Valheim folder by following its instructions. Launch the game once so BepInEx creates its folders.
2. Copy `ValheimGatherer.dll` into `<Valheim>/BepInEx/plugins/ValheimGatherer/`.
3. Launch Valheim.

To check that it loaded, look for `Valheim Gatherer 0.1.0 loaded` in `BepInEx/LogOutput.log`.

### Building from source
Requirements: .NET SDK 6 or newer. Valheim and BepInEx must also be installed on the same machine, because the build references their DLLs.

1. Copy `Local.props.example` to `Local.props` and set:
   - `ValheimDir`: your Valheim install folder (the one containing `valheim_Data`).
   - `BepInExDir`: the `BepInEx` folder of your manual install or mod manager profile.
   - `DeployToPlugins`: `true` to copy the DLL into `BepInEx/plugins/ValheimGatherer/` after every build.
2. Build:
   ```
   dotnet build ValheimGatherer -c Release
   ```
   The output is `ValheimGatherer/bin/Release/ValheimGatherer.dll`.

### Uninstalling
Delete `BepInEx/plugins/ValheimGatherer/`. Pins that were already created stay on your map as ordinary pins.

## Configuration

The config file is `BepInEx/config/valheimgatherer.cfg`. It is created on first launch. There are three ways to change settings:
- **In-game chat commands** (see [In-game commands](#in-game-commands)). Changes apply immediately and are saved to the file.
- **Editing the file by hand.** The game doesn't notice the edits on its own. Run `/gatherer reload` in chat to apply them, or restart the game.
- **A config manager mod** such as BepInEx Configuration Manager (opened with F1). Changes apply immediately.

| Section | Setting | Default | Description |
| --- | --- | --- | --- |
| General | `Enabled` | `true` | Turns automatic pinning on or off. |
| General | `DiscoveryRadius` | `50` | Distance in meters at which a resource gets pinned. |
| General | `ScanInterval` | `1` | Seconds between proximity checks. |
| General | `IgnoreInsidePlayerBase` | `true` | Skips resources within workbench range. |
| Rules | `CustomRules` | *(empty)* | Extra rules written as `PrefabPrefix=Label`, separated by commas. Example: `Crypt=Crypt, Pickable_VoltureEgg=Volture Egg`. |
| Rules | `DisabledPrefabs` | *(empty)* | Prefab name prefixes that should never be pinned, separated by commas. |
| Category.* | `Enabled` | `true` | Turns a category on or off. |
| Category.* | `Icon` | varies | Map pin icon (`Icon0` to `Icon4`). |
| Category.* | `MergeRadius` | varies | No new pin is added if a pin with the same label is already within this distance. |
| Category.* | `RemoveWhenDepleted` | Ores/Pickables `true` | Removes the pin once the last object it covers is mined or collected. |

## In-game commands

Type these in chat with a leading `/`, or in the F5 console without it. They run only on your own game and are never sent to other players. Press Tab after `/gatherer ` to autocomplete the first option.

| Command | What it does |
| --- | --- |
| `/gatherer` | Shows whether pinning is on, the radius, and which categories are enabled. |
| `/gatherer on` / `off` / `toggle` | Turns automatic pinning on or off. |
| `/gatherer radius 80` | Sets `DiscoveryRadius` (allowed range 5 to 300). |
| `/gatherer plants off` | Turns a category on or off: `ores`, `pickables`, `plants`, `infostones`, `locations`, `custom`. |
| `/gatherer settings` | Lists every setting with its current value. |
| `/gatherer set <Section.Key> <value>` | Changes any setting by its full name, for example `/gatherer set Category.Ores.MergeRadius 20` or `/gatherer set Rules.CustomRules Crypt=Crypt, Pickable_VoltureEgg=Volture Egg`. |
| `/gatherer reload` | Re-reads the `.cfg` file after you edit it by hand. |
| `/gatherer help` | Lists the commands. |

Every change is saved to the `.cfg` file right away, so it is kept after a restart. Invalid values are rejected and the old value stays. Numbers outside the allowed range are clamped to it.

## Known limitations
- Only some prefab names were checked against the game files. If a resource is never pinned, its prefab name is probably different, so add it through `CustomRules`.
- Ashlands resources are not included by default.
- A pin is removed only when **you** mine out or collect the resource. If another player does it, the pin stays.

See [ARCHITECTURE.md](ARCHITECTURE.md) for how the plugin works and why it was built this way.
