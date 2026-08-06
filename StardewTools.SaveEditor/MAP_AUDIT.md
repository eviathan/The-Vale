# Map tab audit — cross-referenced against the decompiled game

Date: 2026-08-06. Updated 2026-08-06 — every finding in sections 1-2 below has now been addressed
(fixed, or in 2.2's case, investigated and found to be unnecessary) except the two items
explicitly called out as still-open in their own entries (1.5, 2.1's lower-priority fields). See
the new **§5 Update log** and **§6 Out of scope** sections at the bottom for what changed and what
this pass deliberately didn't attempt.

Scope: `StardewTools.SaveEditor/Controls/FarmMapControl.cs`, `StardewTools.SaveEditor/MapAssets/*`,
`StardewTools.SaveEditor/ViewModels/MapTabViewModel.cs`, `StardewTools.Core/Models/*` (the Map
tab's rendering, interaction, and save-XML-construction code).

Method: for each subsystem, the actual game logic was located in the decompiled 1.6 source
(`.reference/StardewValleyDecompiled/`) and compared line-by-line against our implementation.
Findings are ranked by how visible/impactful they are, not by how they were discovered. Each
entry cites the decompiled file/method it's checked against so it can be re-verified.

**Meta-caveat that applies to everything below:** every "round-trips correctly" claim in this
codebase's existing comments (and in this audit) has only ever been verified by loading the
written XML back through `StardewTools.Core`'s *own* parser (`SaveGameEditor.Load`), never by
loading it in the actual game. XML deserialization tolerance for missing/reordered optional
fields is assumed, not confirmed. Before relying on synthesized elements (a placed Building, a
newly-planted Crop, a materialized Farmhouse) in a save you care about, load it in-game once and
check nothing errors or looks wrong.

---

## 1. Confirmed bugs (rendering doesn't match the game, verified against source)

**Status: 1.1-1.4 fixed below (each entry now says so inline). 1.5 is still open** — a real
scene-graph Y-sort was out of scope for this pass; see its entry for why it stays low-priority.

### 1.1 Adult trees always use the wrong sprite variant, and `Flipped` has no effect — HIGH

`FarmMapControl.TryDrawTreeSprite` (`Controls/FarmMapControl.cs:817-850`) picks between two
sprite-sheet columns via:

```csharp
var variant = (position.X * 3 + position.Y * 7) % 2;
found = TreeSprites.TryGetAdultSprite(ContentFolder!, tree.TreeType, Season, variant, ...);
```

`TreeSprites.TryGetAdultSprite` (`MapAssets/TreeSprites.cs:30-41`) treats `variant` as choosing
between the sheet's X=0 and X=96 columns.

This doesn't match the real game at all. Verified against `Tree.draw()`
(`StardewValley.TerrainFeatures/Tree.cs:1672-1685`):

```csharp
Rectangle source_rect = treeTopSourceRect;   // (0, 0, 48, 96)
if ((data.UseAlternateSpriteWhenSeedReady && hasSeed) || (data.UseAlternateSpriteWhenNotShaken && !wasShakenToday))
    source_rect.X = 48;
else
    source_rect.X = 0;
if (hasMoss)
    source_rect.X = 96;
...
spriteBatch.Draw(texture, ..., source_rect, ..., flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None, ...);
```

So in the real game:
- **X=0 vs X=96 is never a cosmetic "variant" choice.** X=96 specifically means "this tree has
  moss" (a real, tracked 1.6 feature). X=48 means "alternate sprite" (seed-ready glow / not
  shaken today, both data- and state-driven). Our tile-position formula picks X=0 or X=96 for
  essentially random trees regardless of whether they actually have moss — **roughly half of
  all rendered adult trees show the moss-covered sprite even when `hasMoss` is false**, and none
  of them ever show the real X=48 alternate sprite.
- **The actual per-tree visual variation the game uses is a horizontal mirror** (`flipped ?
  SpriteEffects.FlipHorizontally : SpriteEffects.None`), driven by `Tree.flipped` — a real,
  persisted `NetBool` (`Tree.cs:103-104`), randomized once at creation
  (`flipped.Value = Game1.random.NextBool();`, `Tree.cs:186`).
- **We already read and expose this exact field** — `TreeEditor.Flipped` (`Core/Models/TreeEditor.cs:65-69`)
  is fully wired into `TreeDetailsViewModel` and editable in the UI — **but rendering never
  reads it.** Toggling "Flipped" in the details panel does nothing visually.

**Fix shape:** drop the fake `variant` parameter entirely; always use `Rect(0, 0, 48, 96)` unless
`hasMoss` (then `X=96`) — both `UseAlternateSpriteWhenSeedReady`/`WhenNotShaken` would need
`Data/trees.json` lookups we don't currently do, so X=48 can reasonably stay unimplemented for
now. Then mirror the destination `Rect` horizontally when `tree.Flipped` is true (Avalonia has no
`SpriteEffects` equivalent on the simple `DrawImage` overload — needs a `PushTransform` with a
horizontal-flip matrix, or drawing into a negative-width rect via `context.PushTransform`).

**FIXED.** `TreeSprites.TryGetAdultSprite` now takes `hasMoss` directly (no more fake `variant`)
and `FarmMapControl.TryDrawTreeSprite` mirrors the destination rect via a new
`FlipHorizontalAround(Rect)` helper (negate-X-then-translate-back `Matrix` pushed with
`context.PushTransform`) whenever `tree.Flipped` is true — visually verified via the render
harness's `--tree-flip-test` (isolated renders of a normal/flipped/mossy tree, confirmed by eye).
The X=48 alternate-sprite column is still deliberately unimplemented (would need a `Data/trees.json`
lookup this tool doesn't do) — same scope cut as originally suggested, not a regression.

### 1.2 Trees never render below stage 5 — always the adult sprite — HIGH (partially known)

Already flagged in `TryDrawTreeSprite`'s own doc comment ("Always uses the adult sprite
regardless of the tree's actual growth stage") — but the comment undersells it: stages 0-4
aren't "a smaller version of the same tree," they're **entirely different sprites** (a seed on
the ground, a tiny sprout, etc.), and the previously-unverified frame coordinates are now known.
Verified against `Tree.draw()` (`Tree.cs:1649-1658`):

```csharp
if (growthStage < 5)
{
    Rectangle sourceRect = growthStage switch
    {
        0 => new Rectangle(32, 128, 16, 16),  // seed
        1 => new Rectangle(0, 128, 16, 16),   // sprout
        2 => new Rectangle(16, 128, 16, 16),  // sapling
        _ => new Rectangle(0, 96, 16, 32),    // "bush" stage (3 and 4 both use this)
    };
    // anchored differently than the adult sprite too - origin (8, growthStage>=3 ? 32 : 16)
}
```

A tree placed via the new "Plant Tree" tool at a low growth stage, or any tree edited down from
adult, currently renders as a full adult tree in the editor regardless. This is now fixable with
exact verified rects rather than a guess.

Related, smaller gap: `growthStage >= 3` also visually anchors differently (origin Y 32 vs 16)
and the sub-5 draw applies a `Color.HotPink` tint when `fertilized` is true — our code never
tints fertilized trees at any stage.

**FIXED** (the growth-stage sprite rects; the fertilized tint is a cosmetic polish item still not
done). `TreeSprites.TryGetGrowthStageSprite` implements the exact switch above and
`TryDrawTreeSprite` now branches `stump ? stump-sprite : growthStage < 5 ? growth-stage-sprite :
adult-sprite`, matching `Tree.draw()`'s own branching. Visually verified via `--tree-flip-test`
(stages 0-4 rendered and inspected).

### 1.3 `Tree.hasMoss` / `Tree.wasShakenToday` aren't tracked at all — MEDIUM

Both are real, separately-serialized `NetBool` fields (`Tree.cs:115-122`, confirmed serialized
via `.AddField(wasShakenToday, ...).AddField(hasMoss, ...)` at `Tree.cs:219-220`).
`TreeEditor` (`Core/Models/TreeEditor.cs`) doesn't expose either one. Consequence beyond the
rendering bug in 1.1: any edit-and-resave of a tree that has moss doesn't touch these fields
directly (XML passthrough should preserve them fine since we only mutate specific child
elements), but there's no way to see or change moss state from the tool, and no way to correctly
decide the sprite's X offset (1.1's fix) without reading it.

**FIXED.** `TreeEditor.HasMoss`/`WasShakenToday` added (create-if-missing read/write, since a real
save examined during development predates these fields and lacks the elements entirely — unlike
every other Tree field here, which throws if missing). Both are now editable checkboxes in
`TreeDetailsViewModel`/the details panel, and `HasMoss` feeds 1.1's sprite-column fix.

### 1.4 Grass renders one centered tuft; the game scatters up to four — MEDIUM (cosmetic)

`GrassSprites.TryGetSprite` + `FarmMapControl`'s grass branch (`Controls/FarmMapControl.cs:766-781`)
draw a single sprite roughly centered on the tile. The real `Grass.draw()`
(`StardewValley.TerrainFeatures/Grass.cs:530-538`) draws **`numberOfWeeds` (1-4) independent
15x20 tufts**, each with its own scattered position offset within the tile, its own blade variant
(`whichWeed[i]`, 0-2), and its own random horizontal flip (`flip[i]`) — a loose cluster, not one
graphic:

```csharp
for (int i = 0; i < numberOfWeeds; i++)
{
    Vector2 pos = /* scattered grid + jitter offset within the tile */;
    spriteBatch.Draw(texture, pos, new Rectangle(whichWeed[i] * 15, grassSourceOffset, 15, 20),
        ..., flip[i] ? FlipHorizontally : None, ...);
}
```

We already track `GrassEditor.NumberOfWeeds` (editable in the details panel) but never use it
for rendering — a patch with `NumberOfWeeds = 1` and one with `NumberOfWeeds = 4` render
identically. The source-rect math itself (`X = variant * 15, Y = SourceOffsetY(...)`) is correct
and matches the game exactly.

**FIXED.** `GrassSprites.GetTufts` now returns 1-4 independently-scattered tufts (2x2 quadrant
grid + jitter, each with its own variant/flip) driven by `NumberOfWeeds`, and
`FarmMapControl`'s grass branch loops over them instead of drawing one centered sprite. The
per-tuft variant/flip/jitter is a deterministic hash of `(tile position, tuft index)`, not real
game RNG (which isn't persisted, so it can't be replicated) — good enough for "looks like a real
patch," not a pixel-exact replay of any specific save's actual tuft layout. Visually verified via
`--grass-scatter-test` (1 vs. 4 weeds, isolated renders inspected by eye).

### 1.5 Y-sort is a coarse per-row approximation, not the game's actual sort — LOW (already partially documented)

Already flagged in `FarmMapControl`'s class doc comment as "approximating... without a full scene
graph." Precisely: the real game sorts every drawable by `getBoundingBox().Bottom` (a pixel Y
value; e.g. `Tree.cs:1642`, `baseSortPosition = getBoundingBox().Bottom`, used directly as the
draw layer depth) — a continuous, sub-tile-precise order. We group entities by integer tile row
only (`Position.Y + Height - 1`) and draw whatever's in the same row in whatever order the
`ILookup` enumeration happens to produce. Two entities in the *same* row draw in an arbitrary
(but stable) order rather than the game's precise pixel-bottom order. Low practical impact for a
farm (rarely two tall sprites in the exact same row overlapping the exact same columns), but
worth knowing precisely rather than "approximately."

---

## 2. Confirmed gaps: synthesized save XML is missing real fields

These affect `FarmMapEditor.AddBuilding`/`AddFarmhouse` (`Core/Models/FarmMapEditor.cs`) and
`ObjectXmlBuilder.Fields` (`Core/Serialization/ObjectXmlBuilder.cs`), used by every "place a new
X" tool. Checked against the actual `NetFields.AddField(...)` chains in the decompiled classes,
which is the authoritative list of what the game itself serializes.

### 2.1 `Building` — missing fields, two of which affect buildings this tool actively places

Real list (`StardewValley.Buildings/Building.cs:384-409`): `id, indoors, nonInstancedIndoorsName,
tileX, tileY, tilesWide, tilesHigh, maxOccupants, currentOccupants, daysOfConstructionLeft,
daysUntilUpgrade, buildingType, humanDoor, animalDoor, magical, fadeWhenPlayerIsBehind,
animalDoorOpen, owner, newConstructionTimer, netBuildingPaintColor, buildingChests,
animalDoorOpenAmount, hayCapacity, parentLocationName, upgradeName, skinId, modData`.

Our template covers everything except: `netBuildingPaintColor`, `buildingChests`, `hayCapacity`,
`parentLocationName`, `upgradeName`, `modData`.

Two of these are not cosmetic for buildings we already let people place:
- **`hayCapacity`** — Silo is on `PlaceableBuildings`' safe list and exists specifically to store
  hay. Without this field, a placed Silo's hay storage tracking is unverified/possibly wrong.
- **`buildingChests`** — Junimo Hut (also on the safe list) has an associated chest per
  `Data/Buildings.json`'s `Chests` entry; without this field the hut may be missing the chest
  structure the game expects to interact with.

`netBuildingPaintColor`/`parentLocationName`/`upgradeName`/`modData` are lower-risk (paint-bucket
recoloring, nested-location bookkeeping, upgrade-path tracking, mod metadata) but still real gaps
between our synthesized element and what the game itself would have written.

**`hayCapacity`/`buildingChests` FIXED** — both added to `FarmMapEditor.AddBuilding`/
`AddFarmhouse`'s template (`buildingChests` written empty; confirmed via `Building.load()` ->
`LoadFromBuildingData(data)`, called on every location load, that it self-heals: it unconditionally
overwrites `hayCapacity.Value` from `Data/Buildings.json` and auto-creates any chest entries the
building's `Chests` list calls for — so an empty/zero starting value is safe, not a lasting gap).
`BuildingEditor.HayCapacity` is also now a real read/write property. **The remaining fields
(`netBuildingPaintColor`/`parentLocationName`/`upgradeName`/`modData`) are still not written** —
left as-is; still lower-risk than the two that were fixed, per the original writeup above.

### 2.2 `Object` — missing fields, one of which affects a common decorative case

Real list (`StardewValley/Object.cs:759-787`, Object's own fields on top of Item's — verified
against a real placed Object/inventory Item earlier this session for the fields we do have):
adds `heldObject, lastInputItem, lastOutputRuleId, preserve, netLightSource, orderData,
_destroyOvernight, signText` beyond what `ObjectXmlBuilder.Fields` currently builds.

- **`netLightSource`** is the concrete risk: `isLamp` is tracked and set correctly, but without
  the actual light-source object reference, a placed lamp/torch-type decoration likely won't
  emit light in-game until picked up and re-placed by the player.
- `heldObject`/`lastInputItem`/`lastOutputRuleId` matter for machines (Furnace, Keg, ...) placed
  mid-processing — not relevant for a freshly-placed machine (nothing to hold yet), so low risk
  in practice.
- `preserve`, `orderData`, `signText`, `_destroyOvernight` are all niche (jelly/pickle type
  tracking, special-order objects, sign text, respawn-overnight flag) and unlikely to matter for
  what this tool is actually used to place.

**INVESTIGATED, NOT FIXED — confirmed unnecessary, not a real gap.** Before adding any of these
fields, a real placed "Weeds" `Object` element in an actual save was checked directly: it has
**none** of `heldObject/lastInputItem/lastOutputRuleId/preserve/netLightSource/orderData/
_destroyOvernight/signText`, despite every one of them being in `Object.cs`'s `.AddField()` chain.
This is the same self-healing pattern already relied on elsewhere in this codebase for
`nonInstancedIndoorsName` — confirmed specifically for `netLightSource` via `Object.cs`'s
`initializeLightSource(tileLocation.Value)`, called during the object's own lifecycle to
(re)derive its light source from the object's lamp-ness (`isLamp`, which we do track correctly),
not from saved XML. **The decompiled `.AddField()` registration list is necessary but not
sufficient evidence that a field is actually present in real save XML** — it lists everything a
field *could* serialize as, not what a freshly-placed instance actually has. Real save data wins;
no code change was made here.

### 2.3 Building placement doesn't validate `AdditionalPlacementTiles` — MEDIUM

`Data/Buildings.json` entries can declare extra tiles beyond the building's own
`TilesWide x TilesHigh` rectangle that also need to be valid ground — e.g. Farmhouse's mailbox
tile at a relative offset outside its main footprint (`"AdditionalPlacementTiles"` in the JSON).
Verified this is real, enforced validation, not just a rendering/interaction hint — the actual
in-game placement gate, `GameLocation.buildStructure`
(`StardewValley/GameLocation.cs:16403-16459`), calls `isBuildable(...)` for every
`AdditionalPlacementTile` in addition to the core footprint, including an `OnlyNeedsToBePassable`
variant for tiles that don't need to be strictly `Buildable`/`Diggable`.

`MapTabViewModel.CanPlaceFootprint` / `PlaceBuildingAt` only check the building's core
`TilesWide x TilesHigh` rectangle (`ViewModels/MapTabViewModel.cs`) — `AdditionalPlacementTiles`
isn't read from `Data/Buildings.json` or validated at all. In practice this means the tool could
allow placing a building somewhere the real game's own construction menu would reject, because
one of its "extra" required tiles (like a mailbox spot) lands on invalid ground.

**FIXED.** `PlaceableBuildings.Load` now also parses `HayCapacity` and `AdditionalPlacementTiles`
(each area's `TileArea` rect + `OnlyNeedsToBePassable`) from `Data/Buildings.json`. New
`MapTabViewModel.CanPlaceBuildingFootprint` checks the core rectangle via the existing
`CanPlaceFootprint`, then every additional tile individually (using the stricter buildable check
unless `OnlyNeedsToBePassable`), and is now what both `PlaceBuildingAt` and the Building draw
tool's click handler call instead of the bare `CanPlaceFootprint`.

---

## 3. Design differences worth knowing about (not bugs — the editor intentionally behaves differently than a live game action would)

- **Till tool collapses two real game actions into one.** In the actual game, swinging a Hoe at
  a tile that already has a terrain feature (grass, a tree, existing HoeDirt) *doesn't till it* —
  it just interacts with/tries to destroy that feature and stops (`Hoe.cs`, the
  `tilesAffected` loop: `if (terrainFeature.performToolAction(...)) { ...Remove...; } continue;`).
  Actually tilling requires a *second* swing once the tile is clear. Our Till/Plant Crop tools
  instead offer one confirmation that removes whatever's blocking *and* tills/plants in the same
  action. This is a reasonable editor-UX simplification, not a data-correctness issue, but it
  means a single click here does more than a single real tool swing would.
- **Building footprints are treated as fully solid rectangles for overlap purposes.** Real
  buildings have a per-tile `CollisionMap` (`Data/Buildings.json`, e.g. Farmhouse's has interior
  `O` "open" cells) — not every tile in the footprint is actually blocking. Treating the whole
  rectangle as solid is conservative (never permits an overlap the real game would actually
  reject) but occasionally more restrictive than necessary (blocks a placement the real game
  might allow through an open collision cell). Safe direction to be wrong in; not a correctness
  bug.
- **`Occludes()`'s "might be covering the selection" heuristic only considers trees**
  (`Controls/FarmMapControl.cs:647-657`). A tall building or a 2-tile BigCraftable drawn in a
  later row could just as easily visually cover a selection in an earlier row, but won't get the
  translucency treatment. Doesn't affect correctness — the selected entity is unconditionally
  redrawn fully opaque on top of everything at the end of `RenderRealMap`
  (`Controls/FarmMapControl.cs:637-638`) regardless of what `Occludes` returns — it only means
  *other* nearby entities miss out on the "fade so you can see what's behind" polish in those
  cases.

---

## 4. Checked and confirmed correct

- **Object sprite index math.** `ObjectSprites.TryGetSprite`'s `col = index % 24, row = index /
  24` against a 384px-wide, 16px-cell sheet is algebraically identical to the game's own
  `Game1.getSourceRectForStandardTileSheet` (`Game1.cs:15400-15411`:
  `X = (index*16) % textureWidth, Y = (index*16/textureWidth) * 16`) for a 384px-wide sheet.
- **Tile placement rules** (`TmxMap.IsTilePlaceable`/`IsTileBuildable`/`IsTileDiggable`,
  `MapAssets/TmxMap.cs`) — verified in an earlier pass against `GameLocation.isTilePlaceable`,
  `isBuildable`, and `Hoe.cs`'s own tillability check; still holds up under this pass.
- **Fish Pond water compositing** (frame + tinted water fill) — verified against
  `FishPond.draw()`; the only intentionally-skipped pieces (animated ripple, netting style) are
  clearly documented as such.
- **Farmhouse exterior source rect** (`FarmhouseSprite.cs`) — `row = min(upgradeLevel, 2)`
  matches `Building.getSourceRect()`'s `FarmHouse`-specific branch exactly
  (`Building.cs:1725-1736`).

---

## Suggested priority if addressing these

1. **1.1 (tree flip/variant)** — highest visibility (affects roughly half of all rendered adult
   trees) and cheapest to fix (the data, `TreeEditor.Flipped`, already exists and is already
   wired into the UI; only the render call needs to change).
2. **2.3 (AdditionalPlacementTiles)** — the only item here that can let the tool produce a
   placement the real game's own rules would reject; worth closing before it bites someone using
   Farmhouse-relocation or Pet Bowl placement near map edges/water.
3. **2.1's `hayCapacity`/`buildingChests`** — narrow but concrete: affects two buildings already
   on the placeable list.
4. **1.2 (growth-stage sprites)** — now unblocked (exact rects known) but lower urgency than 1.1
   since most placed/edited trees are adults by default.
5. Everything else in section 1-2 is lower-visibility polish or edge-case field coverage.

*(All five items above are now done — see §5.)*

---

## 5. Update log — 2026-08-06 pass

Everything in §1 (except 1.5) and §2 above is now fixed or investigated-and-closed, per each
entry's own inline note. Beyond that, this pass also added a new placed-entity kind end-to-end,
following the exact same "verify against real save data, then decompiled source, before writing
any code" discipline as the rest of this audit:

### 5.1 Bush support added (place/edit/render) — new capability, not a bug fix

Bushes (small/berry/large/tea/walnut, including decorative "town bush" variants) were entirely
unmodeled before this pass — not editable, not placeable, not rendered (they'd have fallen into
`UnmodeledTerrainFeatures`... except they don't even live in `terrainFeatures`, so they were
silently invisible with no signal at all that they existed on the map).

- **Real save shape confirmed against 5 actual `Bush` examples**: unlike Tree/Grass/HoeDirt
  (`terrainFeatures` tile dictionary) or Building (`buildings` flat list with its own element
  name), Bush lives in its own `largeTerrainFeatures` flat list, wrapped as
  `<LargeTerrainFeature xsi:type="Bush">` with its own `<tilePosition>` child. Field order:
  `tilePosition, size, datePlanted, tileSheetOffset, health, flipped, townBush, greenhouseBush,
  drawShadow`. `sourceRect`/`inPot`/`uniqueSpawnMutex` (all present in the decompiled `Bush.cs`'s
  `.AddField()` chain) are absent from every real example — same self-healing pattern as §2.2,
  confirmed via `setUpSourceRect()` being recomputed from `size`/`tileSheetOffset`/season on load,
  not read from saved XML.
- **`health`/`greenhouseBush` are plain public fields on `Bush`** (not `NetField`s), so they don't
  appear in `.AddField()` at all — yet both are present in real save XML. This confirms (again)
  that `.AddField()` is necessary but not sufficient evidence of what's actually serialized; the
  decompiled reference here is evidently a slightly different game version than the one that wrote
  the save, and real save data wins either way.
- **Sprite math transcribed directly from `Bush.setUpSourceRect()`/`draw()`** (`Bush.cs`), not
  guessed: `BushSprites.TryGetSprite` implements the full per-size/season/townBush switch.
  Working through `draw()`'s own anchor math algebraically (`position - origin*scale` for every
  size/townBush/size==4 combination) shows the sprite's left edge always lands on the placement
  tile's left edge and its bottom edge always lands on the tile's bottom edge, regardless of
  size — the game's own per-size "raise by one tile" logic exists purely to compensate for the
  taller sizes' taller source rects and cancels out to the same bottom-anchor every time. So
  `FarmMapControl.TryDrawBushSprite` needs none of that branching: bottom-anchored, left-aligned
  to the tile (not centered — medium/large bushes span 2-3 tiles growing rightward from their own
  placement tile), same `FlipHorizontalAround` mirror as trees/grass for `Flipped`.
- **Footprint width per size** (1/2/3 tiles for small-or-tea / medium-or-walnut / large) confirmed
  against `Bush.getBoundingBox()`, used both for rendering and for placement-overlap checks
  (`MapEntitySummary.FootprintWidth`).
- New "Plant Bush" tool follows the Building tool's pattern (own real footprint per click, not a
  brush stroke — several overlapping wide bushes from one brush stroke wouldn't make sense the way
  a wider tilled patch does), defaults to bloom/harvest-ready (`tileSheetOffset=1`) per the
  session's standing "plant already matured" preference.
- Visually verified via the render harness's `--bush-test`: all 5 sizes plus a flipped variant
  rendered and inspected by eye (small bush, berry bush with visible red berries, large bush, tea
  bush, walnut bush with a visible golden walnut) — all correct, distinct sprites, not a fallback
  marker. Round-tripped through `FarmMapEditor.Bushes` to confirm the real save's 6 pre-existing
  bushes parse correctly alongside the newly-placed ones.

### 5.2 Fence (Object subtype) — investigated, confirmed as a real gap, not fixed

Checked whether the existing generic Object placement tool already covers fences (`Data/
Objects.json`'s `Hardwood Fence`/`Wood Fence`/`Stone Fence`/`Iron Fence`, all `Type: "Crafting"`).
**It doesn't, and placing one via the generic tool would silently produce a non-functional prop,
not a real fence.** `Fence` is a real `Object` subclass (`StardewValley/Fence.cs`) with its own
`xsi:type="Fence"` wrapper and extra fields beyond plain Object (`health, maxHealth, whichType,
gatePosition, isGate`) that drive real gameplay behavior (decay over time, gate open/close,
neighbor-aware connected-fence rendering). The generic `AddObject`/`ObjectXmlBuilder` path writes
a plain `<Object>` with none of that — same class of gap as Bush was before this pass, just not
addressed this time (Fence's connectivity-aware rendering in particular is a meaningfully bigger
lift than Bush's single-sprite-per-tile draw). No real placed Fence was available in the sample
save to verify field values against, unlike Bush's 5 real examples.

---

## 6. Out of scope — "anything you can do in game" gaps not attempted this pass

The Map tab now covers Tree, Grass, HoeDirt/Crop, ResourceClump, Object/BigCraftable, Building
(no-interior subset), and Bush — placement, editing, removal, and (mostly) real rendering for all
of them. The following are consciously **not** attempted this pass, each for a specific reason,
not an oversight:

- **Fence** — see 5.2 above. A real, scoped gap; smaller than the ones below but still a genuine
  subtype (own xsi:type, own fields, connectivity-aware rendering), not just a missing item on a
  list.
- **FarmAnimal / Furniture** — no real save on this machine has any owned animals or placed
  furniture, so their field shapes are entirely unverified (this session's whole discipline has
  been "real save data or decompiled source before writing code," not modding-wiki guesses).
  Per the standing plan (`melodic-crafting-treasure.md`, Phase 2), these are blocked on either
  decompiling the classes directly or getting a save that actually has one, whichever comes first
  — not attempted here.
- **Buildings with a real interior** (Barn, Coop, Big Shed, Greenhouse, ...) — `PlaceableBuildings`
  deliberately excludes anything with a non-null `IndoorMap`/`NonInstancedIndoorLocation` in
  `Data/Buildings.json`, because placing one correctly means also creating and linking its interior
  `GameLocation`, which isn't implemented or verified. Placing one without that link would produce
  a building whose door leads nowhere.
- **A true Y-sorted scene graph** (1.5) — the current per-tile-row grouping is a coarse but
  low-impact approximation; replacing it with the game's actual continuous pixel-bottom sort is a
  real rendering-architecture change, not a field-mapping fix like everything else here.

None of these are silently dropped — they're recorded here specifically so the next pass at
"anything you can do in game" doesn't have to rediscover the same scope boundary from scratch.
