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
- **Map** - a top-down, click-to-select view of the farm, with the game's *real* tile
  art (terrain, paths, border trees) as the backdrop when an "extracted assets folder"
  is set (see below). Placed entities render as **real sprites**, not markers, where
  we've verified the sprite sheet layout:
  - **Trees** (`TreeSprites`) - the actual adult tree sprite from `TerrainFeatures/
    tree{1,2,3}_{season}.png`, confirmed by visually inspecting the real extracted
    file (two ~48x80px adult variants side by side), anchored trunk-down on the tile.
    Always draws the *adult* sprite regardless of actual growth stage - sapling/bush
    stage frame coordinates aren't verified yet, so a grown tree in the right spot
    beats a stage-accurate square. Only treeType 1/2/3 are mapped; exotic types
    (palm, mystic, mushroom) still fall back to a marker.
  - **Objects** (`ObjectSprites`) - real icons from `Maps/springobjects.png` (confirmed
    384x624px = 24x39 grid of 16px cells) via each object's real `ParentSheetIndex`.
  - **Grass** - not drawn at all once real art is loaded; it's already baked into the
    terrain art, so a marker per tile was just noise.
  - **Resource clumps** (stumps/boulders/logs) and anything without a mapped sprite
    still render as an outlined colored square - real position/data, placeholder look.
    Same reasoning as everywhere else in this repo: an unverified sprite crop can
    render as visible garbage, which is worse than an honest placeholder.

  **Depth**: terrain and entities are drawn row-by-row, interleaved (all of row 0's
  terrain+entities, then row 1's, ...) rather than "all terrain, then all entities" -
  this approximates the game's Y-sorted draw order, so a tall tree sprite correctly
  occludes/is occluded by whatever's drawn in neighboring rows. Not a full scene graph.
  When something's selected, any tree that could plausibly be covering it (same/later
  row within canopy-reach, nearby horizontally) draws at reduced opacity, and the
  selection itself is redrawn on top as a final pass so it's never fully hidden.
  Confirmed the mechanism targets the right entity and runs without error (found a
  real tree-behind-tree pair in the live save and selected it programmatically to
  check), but couldn't visually confirm the opacity difference itself - the map
  renders too small in a screenshot to distinguish 0.35 vs 1.0 opacity on one tree
  among 143 by eye. Worth a manual look once you're driving the UI yourself.

  Confirmed against a 1,636-entity real farm; remove + round-trip tested. Without an
  assets folder set, the whole thing falls back to the flat-color abstract view.
  `FarmMapEditor.UnmodeledTerrainFeatures` surfaces any terrain feature type we don't
  render yet (e.g. planted crops/tilled soil - no save we've tested against had any)
  so the view can say "there's also N tiles of X not shown" instead of silently
  dropping them.

  **Tile inspection**: clicking empty space (no entity there) reports every layer's
  tile at that position and its real TMX properties (Diggable/Buildable/Type/...) in
  the side panel - confirmed against the real Farm map. These are base-map properties
  (fixed game design, not save-tracked data), so there's nothing to write back here;
  what *is* save-tracked at a tile (a planted crop, tilled soil) would go through the
  entity system instead, once that schema is verified the way everything else has been.

  **Buildings** (barns, coops, sheds): modeled and rendered (`BuildingEditor`,
  `MapEntitySummary.FromBuilding`) as a translucent footprint-sized rectangle sized to
  the building's real width/height. Unverified against real data, same as resource
  clumps - the reference save has never had one built, so there was no real
  `<buildings>` content to confirm field names (`buildingType`/`tileX`/`tileY`/
  `tilesWide`/`tilesHigh`) against. Parsing logic itself *is* verified, via a synthetic
  building injected into a copy of the real save (same technique used earlier for
  chest detection) - confirmed it's found, positioned, and sized correctly; only the
  field names are still a best-effort guess pending a real example.

  **Every location**: a "Location" picker (populated from `SaveGameEditor.LocationNames`
  - every location this save actually has, confirmed real) lets you view the real tile
  art for any of them, not just the Farm - `TmxMap`/`MapAssetLoader` work for any
  `.tmx` file StardewXnbHack extracts. Placed-entity data (trees/objects/clumps/
  buildings) is still Farm-only; other locations show terrain with no overlay.

  **Getting real tile art**: click "Auto-Extract" in the Map tab. `TileArtExtractor`
  finds your local game install, downloads
  [StardewXnbHack](https://github.com/Pathoschild/StardewXnbHack) (MIT licensed - it's
  code, not game assets) if it isn't already there, runs it, and points the app at the
  result automatically - no manual tool placement or terminal use. (A manual "Browse..."
  is still there as a fallback.) Building this surfaced a real bug worth noting: the
  first version redirected the tool's stdout/stderr without ever reading them, which
  fills the OS pipe buffer and makes the child process block on its own output -
  looked like a hang. Fixed by draining both streams concurrently with the wait, not
  after it. This produces plain `.tmx`/`.png` files, which is what `TmxMap` and
  `MapAssetLoader` (in `StardewTools.SaveEditor/MapAssets/`) read directly - no XNB
  decompression happens in this app at all. Those extracted files are the game's own
  copyrighted art; they're never bundled with this app or committed to the repo - only
  the (MIT-licensed, asset-free) extractor tool itself gets downloaded automatically.
  You only need to extract once per game update, though - the path is saved to a local
  settings file (`~/Library/Application Support/StardewTools/settings.json` on macOS,
  via `AppSettings`) and reloaded automatically on every launch; if that file is ever
  missing, `GameInstallLocator` searches common Steam/GOG paths before falling back to
  asking. Season is handled by swapping the `spring_` prefix on
  tileset image names for `summer_`/`fall_`/`winter_` (confirmed: all four variants
  exist on disk).

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

That hand-rolled approach hit a real wall on larger assets: it correctly decompressed
small files (achievements, item data) but broke partway through a ~2MB tilesheet
texture, because XNA's block-compression framing has undocumented edge cases our
reverse-engineering hadn't hit yet. Rather than keep guessing at a proprietary binary
format, the tile art uses [StardewXnbHack](https://github.com/Pathoschild/StardewXnbHack)
instead (see the Map section above) - it sidesteps the whole problem by decompressing
through a real (if temporary) game instance rather than reimplementing the format.

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
install and references its assemblies + SMAPI's at build time. **Confirmed working
end-to-end**: built, deployed, loaded by SMAPI with zero errors, and the app's
Trainer tab connects to a real running game and polls live state correctly.

Setup, if starting fresh:

1. Update Stardew Valley to 1.6+ and install [SMAPI](https://smapi.io/) (its installer
   patches the game launch to load mods - a change to your actual game install, so run
   it yourself rather than scripting it).
2. `dotnet build StardewTools.Trainer` - on success this copies the mod into
   your `Mods/` folder automatically; launch the game via SMAPI to test.

**If you have more than one game install** (e.g. an old GOG copy alongside a newer
Steam one), the mod build package's auto-detection just picks whichever it finds
first, valid or not - it found the old copy here and refused to fall back. Fixed by
setting `<GamePath>` explicitly in `StardewTools.Trainer.csproj` rather than touching
either install:

```xml
<GamePath>/path/to/Steam/steamapps/common/Stardew Valley/Contents/MacOS</GamePath>
```

Core, Ipc, and the app itself don't depend on the game being installed at all.

## Building

```
dotnet build StardewTools.sln   # builds everything except Trainer until SMAPI/1.6+ is set up
dotnet run --project StardewTools.SaveEditor
```
