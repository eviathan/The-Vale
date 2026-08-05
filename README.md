# StardewTools

A single desktop application for editing Stardew Valley save files and live-cheating
a running game.

## Structure

```
StardewTools.sln
StardewTools.Core/          Save file model + XML editing logic
StardewTools.Ipc/           Shared trainer command/state contracts + named-pipe client/server
StardewTools.SaveEditor/    The app: Avalonia GUI with "Save File" and "Trainer" tabs
StardewTools.Trainer/       SMAPI mod - the in-game half of the Trainer tab
```

`StardewTools.SaveEditor` is the one thing you run. It works standalone for offline
save editing; the Trainer tab additionally needs the game running via SMAPI with
`StardewTools.Trainer` installed as a mod (see below) - that's an unavoidable
requirement for touching a live game process, but it's a one-time setup, not a
second app you interact with day to day.

## Core: how save editing works

Stardew's save file schema is huge (thousands of fields across items, locations,
NPCs, buildings, terrain features...) and shifts between game versions. Rather than
modeling the whole schema as POCOs and round-tripping through `XmlSerializer` -
which would silently drop any element our model doesn't know about - `SaveFile`
keeps the save as a live `XDocument` and typed editors (`PlayerEditor`,
`SaveGameEditor`) mutate only the specific elements they expose. Everything else
in the file passes through untouched. This lets us grow the schema coverage
incrementally without ever risking corrupting/truncating a real save.

Confirmed against a real local save file (`~/.config/StardewValley/Saves/...`):
`player/name`, `player/money`, `player/health`, `player/maxHealth`,
`player/stamina`, and root-level `currentSeason`, `dayOfMonth`, `year`. A
round-trip against the actual 2.8MB save file (load, edit money, save, reload)
changed exactly 3 bytes - the digits of the edited value - confirming nothing
else in the file is touched or lost.

Open a save via the "Open Save..." button (save files have no extension - they're
usually at `~/.config/StardewValley/Saves/<farm>/<farm>` on Mac/Linux). Every field
across all seven sub-tabs writes straight through to the in-memory save as you edit
it - "Save" just flushes that to disk. The first save in a session copies the
original aside to `<file>.bak` before writing, so you always have a way back.

Sub-tabs, each backed by its own `*Editor` class in Core:

- **Player** - Name/Money/Health/MaxHealth/Stamina/Season/Day/Year.
- **Inventory** - edit Stack/Quality or remove any carried item, and duplicate an
  existing item (with an adjustable stack size) to effectively "add" it. Duplicating
  is deliberately the only way to add - fabricating a brand-new item type from
  scratch (e.g. an Iridium Ore you've never had) needs a correct `<Item
  xsi:type="...">` blob whose required fields vary by item class, and we don't have
  a verified real example of every type to build one from safely. Duplicating an
  item already in the file sidesteps that: it's always exactly as valid as the item
  it was copied from. Duplicates reuse an empty slot (`<Item xsi:nil="true" />`)
  rather than growing the list past the backpack's real capacity - confirmed via a
  round-trip against the real save (slot count unchanged, new item survives reload).
- **Stats** - a curated ~14 of the ~50+ fields under `<stats>` (days played, steps,
  monsters killed, items shipped, money earned, ...). `StatsEditor.GetRaw`/`SetRaw`
  reach any of the rest by field name if you need one that's not in the UI.
- **Achievements** - pick from a dropdown of every real achievement name (not IDs).
  `GameEnums.AchievementNames` is extracted directly from the installed game's own
  `Content/Data/Achievements.xnb` (LZX-decompressed and parsed by hand - see below),
  not guessed. IDs 8, 10, 14, 23, 33 genuinely aren't in that file and fall back to
  "Achievement #N".
- **Relationships** - Points and "talked to today" for any NPC you already have a
  friendship entry for. Deliberately can't create a relationship from nothing: the
  reference save had no populated `<Friendship>` entries to verify the full required
  field set against, so fabricating one risks writing something the game's loader
  rejects.
- **Storage** - every chest found anywhere in the save (house, sheds, farm, ...),
  found generically by scanning for any element with `xsi:type="Chest"`, regardless
  of the wrapping element's own name. That last part matters: carried items serialize
  as `<Item xsi:type="...">`, but the same classes placed in the world serialize as
  `<Object xsi:type="...">` instead - confirmed against a real save (plain `<Object>`
  for a base object like Weeds, `<Object xsi:type="Cask">` for a subclass). An earlier
  version of this matched only `<Item>` and silently found zero chests as a result.
  Contents-container name is still a best guess (no real chest existed to verify
  against) and degrades gracefully rather than crashing if it's wrong.
- **Farm** - farm type, tomorrow's weather, daily luck (all confirmed real fields),
  shown as named dropdowns rather than raw numbers, plus a read-only building list.
  Farm type and item quality names are confirmed; weather names are best-effort
  (the reference save only ever showed value `1` = Rain).
- **Map** - a top-down, click-to-select view of everything the save tracks as placed
  on the farm: trees, grass, resource clumps (stumps/boulders/logs), and world
  objects (confirmed real against a 1,636-entity real farm - trees, grass and
  resource clumps all verified, remove + round-trip tested). This is *not* the
  game's real terrain art (grass/dirt/path graphics) - that comes from Stardew's own
  xTile map files, a different and much more involved format to parse and render
  (plus DXT-decoding the actual tilesheet textures) than anything else in this repo.
  What's shown is an abstract grid: a colored dot per entity at its real tile
  position, tinted by the save's current season. `FarmMapEditor.UnmodeledTerrainFeatures`
  surfaces any terrain feature type we don't render (e.g. planted crops/tilled soil -
  the reference save had none, so we don't have verified real data for that schema)
  so the map view can say "there's also N tiles of X not shown" instead of silently
  dropping them. Only the Farm location is covered; the save has dozens of others
  (Town, Beach, Mine, ...) that aren't touched.

### Extracting real data from the game's own files

Where guessing felt too risky (achievement names - 30 IDs, easy to mismatch and
mislead), we pulled the real thing instead of relying on memory: the installed
game ships `Content/Data/Achievements.xnb`, a compiled XNB file, LZX-compressed.
Using the game's own `MonoGame.Framework.dll` (found in the app bundle) via
reflection to invoke its internal `LzxDecoder`, plus hand-parsing the decompressed
`Dictionary<int,string>` payload (including the block-framing the XNB compressor
uses, and the extra type-id marker XNA writes before reference-type dictionary
values), we got the exact IDs and names the game itself uses. That was a one-off
extraction, not something the app does at runtime - the result is just baked into
`GameEnums.AchievementNames`.

## Ipc: how the live Trainer works

The app and the mod talk over a local named pipe (`System.IO.Pipes` - a Unix domain
socket under the hood on macOS, nothing network-exposed). `StardewTools.Ipc` defines
the wire format both sides share:

- `TrainerCommand` - one flat message type (`Type` + optional int/bool/float payload)
  covering one-shot sets (`SetMoney`, `SetHealth`, ...) and continuous toggles
  (`ToggleInfiniteStamina`, `SetSpeedBonus`) that get re-applied every game tick.
- `TrainerState` - a snapshot of live player values, requested via a `GetState`
  command; the app polls this once a second while connected to keep the UI live.
- `TrainerPipeClient` (app side) / `PipeServer` (mod side, in `StardewTools.Trainer`).

On the mod side, the pipe's background thread only ever *enqueues* incoming commands
- they're applied to `Game1.player` on the main thread from `ModEntry`'s
`UpdateTicked` handler, since game state should never be mutated off-thread.

The app's Trainer tab shows "waiting for the game" until it can connect, and
degrades back to that state if the pipe drops - the game closing is a normal
event, not an error.

## Trainer mod: prerequisites

The Trainer project is a [SMAPI](https://smapi.io/) mod, built via the
`Pathoschild.Stardew.ModBuildConfig` NuGet package, which auto-locates your game
install and references its assemblies + SMAPI's at build time.

**Known blocker on this machine:** the build package found `/Applications/Stardew
Valley.app` but it's the older Mono-based build (pre-1.6), not the current
.NET 6 build SMAPI 4.x's tooling expects. To build/run the Trainer:

1. Update Stardew Valley to 1.6+ (via Steam/GOG Galaxy).
2. Install [SMAPI](https://smapi.io/) (its installer patches the game launch to load mods - a
   change to your actual game install, so run it yourself rather than scripting it).
3. `dotnet build StardewTools.Trainer` - on success this copies the mod into
   your `Mods/` folder automatically; launch the game via SMAPI to test.

Core, Ipc, and the app itself don't depend on the game being installed at all.

## Building

```
dotnet build StardewTools.sln   # builds everything except Trainer until SMAPI/1.6+ is set up
dotnet run --project StardewTools.SaveEditor
```
