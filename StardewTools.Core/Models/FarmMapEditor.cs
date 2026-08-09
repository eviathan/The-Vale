using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// Typed access over the Farm location's placed content: trees, grass, resource clumps
/// (stumps/boulders/logs), and world objects. This is a different layer from the base
/// terrain art (grass/dirt/path tile graphics) - that comes from the game's own map files,
/// not the save, and isn't covered here. What's covered is everything the save actually
/// tracks about what's *placed* on the farm, which is what's actually editable.
/// </summary>
public sealed class FarmMapEditor
{
    private static readonly XName XsiType = XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance");
    private static readonly XName XsiNil = XName.Get("nil", "http://www.w3.org/2001/XMLSchema-instance");

    private readonly XElement _farmLocation;

    public FarmMapEditor(XElement farmLocationElement)
    {
        _farmLocation = farmLocationElement;
    }

    /// <summary>Whether the Greenhouse is unlocked (confirmed real field on the Farm location
    /// itself, &lt;greenhouseUnlocked&gt; - a plain NetBool, gates whether crops planted inside
    /// actually grow; normally flips true once the Pantry community-center bundle/Joja pantry
    /// equivalent completes). The Greenhouse's own top-level &lt;GameLocation&gt; and its
    /// building's exterior sprite exist and render the same regardless of this flag - it's a
    /// gameplay-functionality gate, not a different visual/map state, so there's nothing else
    /// for this tool to change alongside it.</summary>
    public bool GreenhouseUnlocked
    {
        get => _farmLocation.TryGetChildBool("greenhouseUnlocked") ?? false;
        set => _farmLocation.SetChildBoolCreateIfMissing("greenhouseUnlocked", value);
    }

    public IReadOnlyList<TreeEditor> Trees
        => DictionaryEntries("terrainFeatures")
            .Where(e => (string?)e.Value.Attribute(XsiType) == "Tree")
            .Select(e => new TreeEditor(e.Position, e.Value))
            .ToList();

    public IReadOnlyList<GrassEditor> Grass
        => DictionaryEntries("terrainFeatures")
            .Where(e => (string?)e.Value.Attribute(XsiType) == "Grass")
            .Select(e => new GrassEditor(e.Position, e.Value))
            .ToList();

    public IReadOnlyList<FruitTreeEditor> FruitTrees
        => DictionaryEntries("terrainFeatures")
            .Where(e => (string?)e.Value.Attribute(XsiType) == "FruitTree")
            .Select(e => new FruitTreeEditor(e.Position, e.Value))
            .ToList();

    /// <summary>Tilled soil, optionally with a planted crop - confirmed against a real save.</summary>
    public IReadOnlyList<HoeDirtEditor> HoeDirtTiles
        => DictionaryEntries("terrainFeatures")
            .Where(e => (string?)e.Value.Attribute(XsiType) == "HoeDirt")
            .Select(e => new HoeDirtEditor(e.Position, e.Value))
            .ToList();

    /// <summary>Floor/path tiles - confirmed against the decompiled Flooring class shape (see
    /// FlooringEditor remarks; no real placed example exists in this project's sample saves to
    /// verify field order/shape against directly, unlike Tree/HoeDirt/Bush).</summary>
    public IReadOnlyList<FlooringEditor> Flooring
        => DictionaryEntries("terrainFeatures")
            .Where(e => (string?)e.Value.Attribute(XsiType) == "Flooring")
            .Select(e => new FlooringEditor(e.Position, e.Value))
            .ToList();

    /// <summary>
    /// terrainFeatures types present that we don't model yet. Surfaced here rather than
    /// silently dropped, so the map view can at least say "there's also N tiles of X here"
    /// instead of pretending they don't exist.
    /// </summary>
    public IReadOnlyList<(TilePosition Position, string Type)> UnmodeledTerrainFeatures
        => DictionaryEntries("terrainFeatures")
            .Select(e => (e.Position, Type: (string?)e.Value.Attribute(XsiType) ?? "Unknown"))
            .Where(e => e.Type is not ("Tree" or "Grass" or "HoeDirt" or "Flooring"))
            .ToList();

    public IReadOnlyList<ResourceClumpEditor> ResourceClumps
        => (_farmLocation.Element("resourceClumps")?.Elements("ResourceClump") ?? Enumerable.Empty<XElement>())
            .Select(e => new ResourceClumpEditor(e))
            .ToList();

    /// <summary>Bushes - a flat list (like resourceClumps/buildings, not a tile dictionary),
    /// confirmed against real save data. See BushEditor remarks for the full field-shape story.</summary>
    public IReadOnlyList<BushEditor> Bushes
        => (_farmLocation.Element("largeTerrainFeatures")?.Elements("LargeTerrainFeature")
                .Where(e => (string?)e.Attribute(XsiType) == "Bush") ?? Enumerable.Empty<XElement>())
            .Select(e => new BushEditor(e))
            .ToList();

    public IReadOnlyList<PlacedObjectEditor> Objects
        => DictionaryEntries("objects")
            .Select(e => new PlacedObjectEditor(e.Position, e.Value))
            .ToList();

    /// <summary>Placed furniture - a flat list (like Bush/ResourceClump/Building), not the
    /// &lt;objects&gt; tile dictionary. See FurnitureEditor remarks.</summary>
    public IReadOnlyList<FurnitureEditor> Furniture
        => (_farmLocation.Element("furniture")?.Elements("Furniture") ?? Enumerable.Empty<XElement>())
            .Select(e => new FurnitureEditor(e))
            .ToList();

    /// <summary>Player-constructed buildings - see BuildingEditor remarks on why this is unverified.</summary>
    public IReadOnlyList<BuildingEditor> Buildings
        => (_farmLocation.Element("buildings")?.Elements("Building") ?? Enumerable.Empty<XElement>())
            .Select(e => new BuildingEditor(e))
            .ToList();

    /// <summary>Real GameLocation.name field - "Farm" for the top-level farm, or an interior's
    /// own real map name (e.g. "Shed"/"Barn"/"Coop2") when this wraps a building's &lt;indoors&gt;
    /// (see BuildingIndoorsEditor). Null if genuinely absent (shouldn't happen for anything this
    /// tool itself constructs, but real top-level locations loaded via SaveGameEditor.
    /// GetLocationMap could in principle predate the field).</summary>
    public string? Name => _farmLocation.Element("name")?.Value;

    /// <summary>Real animalLimit field (AnimalHouse-specific, absent/0 on every other GameLocation
    /// this class wraps - e.g. the top-level Farm - which is exactly the "am I inside a Barn/Coop
    /// interior" gate the app layer uses, no separate tracking needed). See
    /// BuildingIndoorsEditor.CreateDefaultAnimalHouse remarks.</summary>
    public int AnimalLimit => _farmLocation.TryGetChildInt("animalLimit") ?? 0;

    /// <summary>Animals living in this location - only meaningful when AnimalLimit &gt; 0 (i.e.
    /// this FarmMapEditor wraps a Barn/Coop's &lt;indoors&gt;, not the top-level Farm). Real
    /// NetLongDictionary&lt;FarmAnimal,...&gt; shape - `&lt;item&gt;&lt;key&gt;&lt;long&gt;N
    /// &lt;/long&gt;&lt;/key&gt;&lt;value&gt;&lt;FarmAnimal&gt;...&lt;/FarmAnimal&gt;&lt;/value&gt;
    /// &lt;/item&gt;` - confirmed against decompiled SerializableDictionary&lt;TKey,TValue&gt;.
    /// WriteXml/ReadXml (the actual save-XML (de)serialization SerializableDictionary a NetLongDictionary
    /// is backed by, not NetLongDictionary's own binary network ReadKey/WriteKey - a bare `long`'s
    /// XmlSerializer output is `&lt;long&gt;value&lt;/long&gt;`, and FarmAnimal isn't polymorphic
    /// (no xsi:type needed, unlike Object/TerrainFeature's base-class value type).</summary>
    public IReadOnlyList<FarmAnimalEditor> Animals
        => (_farmLocation.Element("animals")?.Elements("item") ?? Enumerable.Empty<XElement>())
            .Select(item => item.Element("value")?.Element("FarmAnimal"))
            .Where(fa => fa is not null)
            .Select(fa => new FarmAnimalEditor(fa!))
            .ToList();

    /// <summary>Adopts a new animal into this location - only meaningful inside a Barn/Coop's
    /// &lt;indoors&gt; (see Animals remarks). Generates a fresh myID unique against every animal
    /// already in this dictionary (real save data could in principle span a huge id range, but a
    /// simple looped-random-until-unique is the same pattern this codebase already uses wherever
    /// else a fresh unique id is needed - e.g. FarmMapEditor.CloneEntityAt's building ids, just
    /// long instead of Guid since myID's real type is long, not string). Appends the same id to
    /// animalsThatLiveHere (AnimalHouse's own parallel id list, confirmed real - see
    /// BuildingIndoorsEditor remarks) so both stay in sync, matching AnimalHouse.adoptAnimal's
    /// real behavior.</summary>
    public FarmAnimalEditor AddAnimal(string type, string buildingTypeILiveIn, string name, long ownerId)
    {
        var container = _farmLocation.Element("animals");
        if (container is null)
        {
            container = new XElement("animals");
            _farmLocation.Add(container);
        }

        var existingIds = Animals.Select(a => a.MyId).ToHashSet();
        long myId;
        do
        {
            myId = Random.Shared.NextInt64();
        } while (existingIds.Contains(myId));

        var animalElement = FarmAnimalEditor.CreateDefault(type, name, myId, ownerId, buildingTypeILiveIn);
        container.Add(new XElement("item",
            new XElement("key", new XElement("long", myId)),
            new XElement("value", animalElement)));

        var livesHere = _farmLocation.Element("animalsThatLiveHere");
        if (livesHere is null)
        {
            livesHere = new XElement("animalsThatLiveHere");
            _farmLocation.Add(livesHere);
        }
        livesHere.Add(new XElement("long", myId));

        return new FarmAnimalEditor(animalElement);
    }

    /// <summary>
    /// Places a new plain Object - or, when <paramref name="bigCraftable"/> is true, a
    /// machine/decoration like a Furnace or Grandfather Clock - on the farm at the given tile.
    /// Both are the exact same underlying save shape (confirmed in the decompiled Object.cs:
    /// bigCraftable is just a NetBool field on Object, no special xsi:type or element shape),
    /// only the index space differs (Data/BigCraftables.json instead of Data/Objects.json -
    /// see PlaceableItems). The element shape (every field, in this order) is copied from a
    /// real placed Object in an actual save, not invented - the only per-item fields are
    /// name/parentSheetIndex/price/edibility/type/category, which should come from real
    /// Data/Objects.json or Data/BigCraftables.json data, not the save's own &lt;type&gt;/
    /// &lt;category&gt; values - those turned out to already be corrupted with placeholder
    /// junk ("asdf" as a type, wrong category) in a real save this was verified against,
    /// apparently from some earlier, unrelated tool.
    /// </summary>
    public PlacedObjectEditor AddObject(TilePosition position, int parentSheetIndex, string name, int price, int edibility, int category, string type, bool bigCraftable = false, string? itemId = null)
    {
        var container = _farmLocation.Element("objects");
        if (container is null)
        {
            container = new XElement("objects");
            _farmLocation.Add(container);
        }

        var value = new XElement("Object", ObjectXmlBuilder.Fields(name, parentSheetIndex, price, edibility, category, type, bigCraftable, 1, position.X, position.Y, itemId: itemId));

        var item = new XElement("item",
            new XElement("key", new XElement("Vector2", new XElement("X", position.X), new XElement("Y", position.Y))),
            new XElement("value", value));

        container.Add(item);
        return new PlacedObjectEditor(position, value);
    }

    /// <summary>
    /// Places a new, empty, fully-functional Chest (or Stone/Junimo Chest - any BigCraftables.json
    /// entry whose Name contains "Chest") - previously the generic AddObject path was the only
    /// way to place one, which wrote a plain Object with no xsi:type="Chest" and none of Chest's
    /// own fields (items container, lid frame, etc.) at all, so the game loaded it as an inert,
    /// uninteractable, visually-glitched placeholder instead of a real chest (confirmed via
    /// direct user report). Field shape - both the shared Object base and the Chest-specific
    /// fields - copied from a real placed "Stone Chest" in an actual save; see
    /// ObjectXmlBuilder/ChestXmlBuilder remarks.
    /// </summary>
    public PlacedObjectEditor AddChest(TilePosition position, int parentSheetIndex, string name, int price, string? itemId = null)
    {
        var container = _farmLocation.Element("objects");
        if (container is null)
        {
            container = new XElement("objects");
            _farmLocation.Add(container);
        }

        var (lidFrameCount, fridge, specialChestType) = ChestVariant(parentSheetIndex);
        var fields = ObjectXmlBuilder.Fields(name, parentSheetIndex, price, edibility: -300, category: -9, type: "Crafting", bigCraftable: true, stack: 1, position.X, position.Y, itemId: itemId)
            .Concat(ChestXmlBuilder.Fields(parentSheetIndex, lidFrameCount, fridge, specialChestType));

        var value = new XElement("Object", new XAttribute(XsiType, "Chest"), fields);

        var item = new XElement("item",
            new XElement("key", new XElement("Vector2", new XElement("X", position.X), new XElement("Y", position.Y))),
            new XElement("value", value));

        container.Add(item);
        return new PlacedObjectEditor(position, value);
    }

    /// <summary>Mini-Fridge/Mini-Shipping Bin/Hopper/Big Chest/Big Stone Chest are real Chest
    /// instances under the hood (confirmed via decompiled Object.cs placement code: each calls
    /// `new Chest(...)`, not a distinct class) that each override exactly one field from a plain
    /// Chest's defaults - see ChestXmlBuilder remarks. SpecialChestType values verified verbatim
    /// against the decompiled Chest.SetSpecialChestType()'s own QualifiedItemId switch
    /// (StardewValley.Objects/Chest.cs:546-563) - this previously mapped id 256 (Junimo Chest) to
    /// "None" instead of the real "JunimoChest", a real bug found while cross-referencing this
    /// switch against the decompiled source for the Big Chest fix below (2026-08-08).</summary>
    private static (int LidFrameCount, bool Fridge, string SpecialChestType) ChestVariant(int parentSheetIndex) => parentSheetIndex switch
    {
        216 => (2, true, "None"),               // Mini-Fridge
        248 => (5, false, "MiniShippingBin"),    // Mini-Shipping Bin
        256 => (5, false, "JunimoChest"),        // Junimo Chest
        275 => (2, false, "AutoLoader"),         // Hopper
        304 or 328 => (5, false, "BigChest"),    // Big Chest / Big Stone Chest
        _ => (5, false, "None"),                 // Chest / Stone Chest
    };

    /// <summary>Real Data/BigCraftables.json ids that are secretly a Chest under the hood -
    /// Chest, Stone Chest, Junimo Chest, Mini-Fridge, Mini-Shipping Bin, Hopper, and (via their
    /// real SpriteIndex - see PlaceableItems.LoadBigCraftables) Big Chest (304)/Big Stone Chest
    /// (328), whose actual save id is the non-numeric "BigChest"/"BigStoneChest" (threaded
    /// through separately as PlaceableItem.ItemId, not this numeric key).
    ///
    /// Requires isBigCraftable=true - confirmed by direct cross-reference of both JSON files
    /// that EVERY one of these ids also names a completely different, unrelated plain
    /// (non-bigCraftable) Objects.json food/produce item at the same number (130=Tuna,
    /// 232=Rice Pudding, 256=Tomato, 216=Bread, 248=Garlic, 275=Artifact Trove, 304=Hops,
    /// 328=Wood Floor). Before this check took isBigCraftable into account, placing any of those
    /// eight completely ordinary items would have silently routed through AddChest instead of
    /// AddObject - a real, confirmed latent bug (never triggered until this pass added 304/328,
    /// which is what surfaced the general pattern) fixed 2026-08-08 alongside the Big Chest
    /// addition - see MAP_AUDIT.md.</summary>
    public static bool IsChestId(int parentSheetIndex, bool isBigCraftable) => isBigCraftable && parentSheetIndex is 130 or 232 or 256 or 216 or 248 or 275 or 304 or 328;

    /// <summary>Places one of the Object subclasses that needs its own xsi:type and a handful of
    /// extra fields, but isn't a Chest and isn't Auto-Grabber - Cask, Item Pedestal, Torch/Spirit
    /// Torch, Signs, Garden Pot, Workbench, Mini-Jukebox, Wood Chipper, Telephone, Crab Pot,
    /// Fences. See ExoticObjectCatalog for the per-item xsi:type/extra-fields/canBeGrabbed
    /// lookup this pulls from (keyed by id AND isBigCraftable together - see its remarks for why
    /// id alone isn't enough here either).</summary>
    public PlacedObjectEditor AddExoticObject(TilePosition position, int parentSheetIndex, string name, int price, int edibility, int category, string type, bool bigCraftable)
    {
        var container = _farmLocation.Element("objects");
        if (container is null)
        {
            container = new XElement("objects");
            _farmLocation.Add(container);
        }

        var spec = ExoticObjectCatalog.Lookup(parentSheetIndex, bigCraftable);
        var fields = ObjectXmlBuilder.Fields(name, parentSheetIndex, price, edibility, category, type, bigCraftable, stack: 1, position.X, position.Y, includeQuestId: spec.IncludeQuestId, canBeGrabbed: spec.CanBeGrabbed)
            .Concat(spec.ExtraFields(parentSheetIndex));

        var value = new XElement("Object", new XAttribute(XsiType, spec.XsiTypeName), fields);

        var item = new XElement("item",
            new XElement("key", new XElement("Vector2", new XElement("X", position.X), new XElement("Y", position.Y))),
            new XElement("value", value));

        container.Add(item);
        return new PlacedObjectEditor(position, value);
    }

    public static bool IsExoticObjectId(int parentSheetIndex, bool isBigCraftable) => ExoticObjectCatalog.IsKnown(parentSheetIndex, isBigCraftable);

    /// <summary>Requires isBigCraftable=true - Objects.json's own id 165 is the unrelated
    /// "Scorpion Carp" fish (same collision pattern as IsChestId above).</summary>
    public static bool IsAutoGrabberId(int parentSheetIndex, bool isBigCraftable) => isBigCraftable && parentSheetIndex == 165;

    /// <summary>Real Data/Objects.json ids for the placeable floor/path items (Wood/Stone/
    /// Weathered/Crystal/Straw/Brick/Rustic Plank Floor, Gravel/Wood/Crystal/Cobblestone/
    /// Stepping Stone Path) - confirmed via decompiled Object.placementAction's IsFloorPathItem()
    /// branch that these never become a plain placed Object at all; see AddFlooring. None of
    /// these 13 ids collide with a BigCraftables.json entry (confirmed by direct cross-reference),
    /// but isBigCraftable is still checked for symmetry/defensiveness with the Chest/Exotic checks
    /// above, which do have real collisions - costs nothing and stays correct if that ever changes.</summary>
    public static bool IsFloorPathItemId(int parentSheetIndex, bool isBigCraftable) => !isBigCraftable && FloorPathCatalog.IsKnown(parentSheetIndex);

    /// <summary>
    /// Places a floor/path tile - confirmed via the decompiled Object.placementAction's
    /// IsFloorPathItem() branch: `location.terrainFeatures.Add(placementTile, new Flooring(key))`
    /// where key is Data/FloorsAndPaths.json's own key for that item (FloorPathCatalog.Lookup),
    /// not the item's own id. Lives in the same terrainFeatures tile dictionary as Tree/Grass/
    /// HoeDirt, wrapped &lt;TerrainFeature xsi:type="Flooring"&gt; - matching that dictionary's
    /// established shape here, not a per-type element name. whichView is only meaningful for
    /// ConnectType.Random floor types (see FlooringEditor.WhichView) - always written as 0 here;
    /// every other ConnectType derives its sprite from the live neighbor bitmask at render time
    /// regardless of this value, so a fixed 0 costs nothing for those and only cosmetically
    /// affects which of the 16 non-connecting variants a Random-type tile shows.
    /// </summary>
    public FlooringEditor AddFlooring(TilePosition position, int parentSheetIndex)
    {
        var container = TerrainFeaturesContainer();

        var value = new XElement("TerrainFeature",
            new XAttribute(XsiType, "Flooring"),
            new XElement("whichFloor", FloorPathCatalog.Lookup(parentSheetIndex)),
            new XElement("whichView", 0));

        var item = new XElement("item",
            new XElement("key", new XElement("Vector2", new XElement("X", position.X), new XElement("Y", position.Y))),
            new XElement("value", value));

        container.Add(item);
        return new FlooringEditor(position, value);
    }

    /// <summary>Auto-Grabber (165) stays a plain Object (no xsi:type override) but is
    /// non-functional without a `heldObject` Chest attached - confirmed via decompiled Object.cs
    /// placement code (`autoGrabber.heldObject.Value = new Chest()`, the parameterless ctor,
    /// which leaves the nested chest at Chest's own field-initializer defaults: startingLidFrame
    /// 501, lidFrameCount 5, not playerChest/bigCraftable). Nested tileLocation/boundingBox are
    /// zeroed, matching how the real game represents a non-placed nested Object value (confirmed
    /// via the real Item Pedestal "requiredItem" nested-Object example).</summary>
    public PlacedObjectEditor AddAutoGrabber(TilePosition position, int parentSheetIndex, string name, int price, int edibility, int category, string type)
    {
        var container = _farmLocation.Element("objects");
        if (container is null)
        {
            container = new XElement("objects");
            _farmLocation.Add(container);
        }

        var nestedChestFields = ObjectXmlBuilder.Fields("Chest", 130, price: 0, edibility: -300, category: -9, type: "interactive", bigCraftable: false, stack: 1, tileX: 0, tileY: 0, canBeSetDown: false)
            .Concat(ChestXmlBuilder.Fields(500, playerChest: false));
        var heldObject = new XElement("heldObject", new XAttribute(XsiType, "Chest"), nestedChestFields);

        var fields = ObjectXmlBuilder.Fields(name, parentSheetIndex, price, edibility, category, type, bigCraftable: true, stack: 1, position.X, position.Y, heldObject: heldObject);
        var value = new XElement("Object", fields);

        var item = new XElement("item",
            new XElement("key", new XElement("Vector2", new XElement("X", position.X), new XElement("Y", position.Y))),
            new XElement("value", value));

        container.Add(item);
        return new PlacedObjectEditor(position, value);
    }

    /// <summary>
    /// Plants a tree at the given tile - field shape and order copied verbatim from a real
    /// adult tree in an actual save (growthStage 5, health 10, a leading empty &lt;texture /&gt;
    /// element before growthStage), wrapped as &lt;TerrainFeature xsi:type="Tree"&gt; matching
    /// the terrainFeatures dictionary's real shape (confirmed: the wrapping element is always
    /// named "TerrainFeature", with xsi:type distinguishing Tree/Grass/HoeDirt - not a
    /// per-type element name like &lt;Object&gt;/&lt;Building&gt; use).
    /// </summary>
    public TreeEditor AddTree(TilePosition position, int treeType, int growthStage, bool fertilized = false)
    {
        var container = TerrainFeaturesContainer();

        var value = new XElement("TerrainFeature",
            new XAttribute(XsiType, "Tree"),
            new XElement("texture"),
            new XElement("growthStage", growthStage),
            new XElement("treeType", treeType),
            new XElement("health", 10),
            new XElement("flipped", false),
            new XElement("stump", false),
            new XElement("tapped", false),
            new XElement("hasSeed", false),
            new XElement("fertilized", fertilized),
            new XElement("shakeLeft", false));

        var item = new XElement("item",
            new XElement("key", new XElement("Vector2", new XElement("X", position.X), new XElement("Y", position.Y))),
            new XElement("value", value));

        container.Add(item);
        return new TreeEditor(position, value);
    }

    /// <summary>
    /// Plants a fruit tree at the given tile - wrapped as &lt;TerrainFeature xsi:type="FruitTree"&gt;
    /// in the same terrainFeatures dictionary Tree/Grass/HoeDirt already use. Field shape derived
    /// from decompiled FruitTree.cs (no real placed-fruit-tree save evidence available locally -
    /// see FruitTreeEditor remarks). <paramref name="treeId"/> is the real Data/FruitTrees.json
    /// key (e.g. "628" for Cherry). growthStage 4 (fully grown) needs daysUntilMature 0 -
    /// confirmed via FruitTree.GrowthStageToDaysUntilMature(), the real game's own growthStage-
    /// to-daysUntilMature table - callers should pass the matching pair, not derive one from the
    /// other independently.
    /// </summary>
    public FruitTreeEditor AddFruitTree(TilePosition position, string treeId, int growthStage, int daysUntilMature)
    {
        var container = TerrainFeaturesContainer();

        var value = new XElement("TerrainFeature",
            new XAttribute(XsiType, "FruitTree"),
            new XElement("growthStage", growthStage),
            new XElement("treeId", treeId),
            new XElement("daysUntilMature", daysUntilMature),
            new XElement("fruit"),
            new XElement("struckByLightningCountdown", 0),
            new XElement("health", 10),
            new XElement("flipped", false),
            new XElement("stump", false),
            new XElement("greenHouseTileTree", false),
            new XElement("growthRate", 1));

        var item = new XElement("item",
            new XElement("key", new XElement("Vector2", new XElement("X", position.X), new XElement("Y", position.Y))),
            new XElement("value", value));

        container.Add(item);
        return new FruitTreeEditor(position, value);
    }

    /// <summary>
    /// Tills a tile - field shape copied verbatim from a real HoeDirt in an actual save (state/
    /// fertilizer/isGreenhouseDirt, no &lt;crop&gt; child - see HoeDirtEditor.PlantCrop for
    /// planting into it afterward). state 0 = dry (see HoeDirtEditor.State remarks).
    /// </summary>
    public HoeDirtEditor AddHoeDirt(TilePosition position, int state = 0)
    {
        var container = TerrainFeaturesContainer();

        var value = new XElement("TerrainFeature",
            new XAttribute(XsiType, "HoeDirt"),
            new XElement("state", state),
            new XElement("fertilizer", 0),
            new XElement("isGreenhouseDirt", false));

        var item = new XElement("item",
            new XElement("key", new XElement("Vector2", new XElement("X", position.X), new XElement("Y", position.Y))),
            new XElement("value", value));

        container.Add(item);
        return new HoeDirtEditor(position, value);
    }

    /// <summary>
    /// Plants a bush at the given tile - field order/shape copied verbatim from 5 real Bush
    /// examples in an actual save (tilePosition, size, datePlanted, tileSheetOffset, health,
    /// flipped, townBush, greenhouseBush, drawShadow - see BushEditor remarks). Wrapped as
    /// &lt;LargeTerrainFeature xsi:type="Bush"&gt; inside its own &lt;largeTerrainFeatures&gt;
    /// flat list, not the terrainFeatures tile dictionary. tileSheetOffset defaults to 1 (bloom/
    /// harvest-ready) so a freshly-planted bush looks "mature" like AddTree's default adult
    /// growthStage, per the "plant already matured" ask.
    /// </summary>
    public BushEditor AddBush(TilePosition position, int size, int datePlanted = 0, int tileSheetOffset = 1)
    {
        var container = _farmLocation.Element("largeTerrainFeatures");
        if (container is null)
        {
            container = new XElement("largeTerrainFeatures");
            _farmLocation.Add(container);
        }

        var value = new XElement("LargeTerrainFeature",
            new XAttribute(XsiType, "Bush"),
            new XElement("tilePosition", new XElement("X", position.X), new XElement("Y", position.Y)),
            new XElement("size", size),
            new XElement("datePlanted", datePlanted),
            new XElement("tileSheetOffset", tileSheetOffset),
            new XElement("health", 0),
            new XElement("flipped", false),
            new XElement("townBush", false),
            new XElement("greenhouseBush", false),
            new XElement("drawShadow", true));

        container.Add(value);
        return new BushEditor(value);
    }

    /// <summary>
    /// Places a piece of furniture - field order/shape copied verbatim from a real save's own
    /// starting Bed (isLostItem through drawHeldObjectLow, all base-class Furniture fields per
    /// decompiled Furniture.cs's [XmlElement]-attributed members; bedType is BedFurniture-only
    /// and deliberately omitted - see FurnitureEditor/PlaceableFurniture remarks for why this only
    /// targets the generic, non-Bed/non-FishTank subset). xsi:type="Furniture" (not "BedFurniture"
    /// or "FishTankFurniture") - the plain base class real generic furniture actually uses.
    /// sourceRect/boundingBox/defaultBoundingBox are computed by the caller (PlaceableFurniture,
    /// which knows the real TileSheets/furniture.png sheet width) rather than here, mirroring
    /// decompiled InitializeAtTile/RecalculateBoundingBox computing and persisting these once at
    /// placement time rather than re-deriving them on every draw.
    /// </summary>
    public FurnitureEditor AddFurniture(
        TilePosition position, string furnitureId, string name, int parentSheetIndex,
        int furnitureType, int rotations, int price,
        int sourceX, int sourceY, int sourceWidth, int sourceHeight,
        int boundingWidth, int boundingHeight, bool drawHeldObjectLow)
    {
        var container = _farmLocation.Element("furniture");
        if (container is null)
        {
            container = new XElement("furniture");
            _farmLocation.Add(container);
        }

        var boundingBoxX = position.X * 64;
        var boundingBoxY = position.Y * 64;

        XElement Rect(string elementName, int x, int y, int width, int height) => new(elementName,
            new XElement("X", x), new XElement("Y", y), new XElement("Width", width), new XElement("Height", height),
            new XElement("Location", new XElement("X", x), new XElement("Y", y)),
            new XElement("Size", new XElement("X", width), new XElement("Y", height)));

        var value = new XElement("Furniture",
            new XAttribute(XsiType, "Furniture"),
            new XElement("isLostItem", false),
            new XElement("category", -24),
            new XElement("hasBeenInInventory", false),
            new XElement("name", name),
            new XElement("parentSheetIndex", parentSheetIndex),
            new XElement("itemId", furnitureId),
            new XElement("specialItem", false),
            new XElement("isRecipe", false),
            new XElement("quality", 0),
            new XElement("stack", 1),
            new XElement("SpecialVariable", 0),
            new XElement("tileLocation", new XElement("X", position.X), new XElement("Y", position.Y)),
            new XElement("owner", 0),
            new XElement("canBeSetDown", true),
            new XElement("canBeGrabbed", true),
            new XElement("isSpawnedObject", false),
            new XElement("questItem", false),
            new XElement("isOn", false),
            new XElement("fragility", 0),
            new XElement("price", price),
            new XElement("edibility", -300),
            new XElement("bigCraftable", false),
            new XElement("setOutdoors", true),
            new XElement("setIndoors", true),
            new XElement("readyForHarvest", false),
            new XElement("showNextIndex", false),
            new XElement("flipped", false),
            new XElement("isLamp", false),
            new XElement("minutesUntilReady", 0),
            Rect("boundingBox", boundingBoxX, boundingBoxY, boundingWidth, boundingHeight),
            new XElement("scale", new XElement("X", 0), new XElement("Y", 0)),
            new XElement("uses", 0),
            new XElement("destroyOvernight", false),
            new XElement("furniture_type", furnitureType),
            new XElement("rotations", rotations),
            new XElement("currentRotation", 0),
            Rect("sourceRect", sourceX, sourceY, sourceWidth, sourceHeight),
            Rect("defaultSourceRect", sourceX, sourceY, sourceWidth, sourceHeight),
            Rect("defaultBoundingBox", boundingBoxX, boundingBoxY, boundingWidth, boundingHeight),
            new XElement("drawHeldObjectLow", drawHeldObjectLow));

        container.Add(value);
        return new FurnitureEditor(value);
    }

    public void Remove(FurnitureEditor furniture) => furniture.Element.Remove();

    private XElement TerrainFeaturesContainer()
    {
        var container = _farmLocation.Element("terrainFeatures");
        if (container is null)
        {
            container = new XElement("terrainFeatures");
            _farmLocation.Add(container);
        }

        return container;
    }

    /// <summary>
    /// Places a new building at the given tile - deliberately limited to buildings that have
    /// no interior (Gold Clock, the 4 Obelisks, Well, Silo, Mill, Fish Pond, Pet Bowl, Stable,
    /// Shipping Bin - confirmed via Data/Buildings.json: these all have IndoorMap/
    /// NonInstancedIndoorLocation both null and HumanDoor (-1,-1), i.e. no door, no interior
    /// to wire up) plus Shed and the Barn/Coop family, whose real nested &lt;indoors&gt; interior
    /// IS now written (see BuildingIndoorsEditor). Cabins (FarmHouse's much larger field surface)
    /// and Big Shed (needs upgradeLevel wiring) still need that interior linked correctly, which
    /// isn't done yet - see PlaceableBuildings in the app layer for the actual safe list. Field
    /// order/shape confirmed against the decompiled Building.cs's [XmlElement] attributes (no
    /// real placed-building save data was available to verify against directly).
    ///
    /// <paramref name="indoorMap"/>/<paramref name="animalLimit"/> are only used for the
    /// Barn/Coop family (Data/Buildings.json's own IndoorMap/MaxOccupants for this building type -
    /// null indoorMap means "no AnimalHouse interior to write", which is every other supported
    /// building type including Shed, whose own interior is triggered by buildingType instead).
    /// Building.load() unconditionally recomputes the EXTERIOR building's own maxOccupants/
    /// humanDoor/animalDoor from Data/Buildings.json on every load regardless of what's saved
    /// here (confirmed: Building.hasLoaded is [XmlIgnore], so it's always false right after
    /// deserializing, and LoadFromBuildingData() - which sets exactly those fields - only ever
    /// runs when !hasLoaded) - so leaving maxOccupants/humanDoor/animalDoor at their existing
    /// placeholder values below is safe for every building type, not just the ones already
    /// placeable before this change.
    /// </summary>
    public BuildingEditor AddBuilding(TilePosition position, string buildingType, int tilesWide, int tilesHigh, bool magical, int hayCapacity = 0,
        string? indoorMap = null, int animalLimit = 0)
    {
        var container = _farmLocation.Element("buildings");
        if (container is null)
        {
            container = new XElement("buildings");
            _farmLocation.Add(container);
        }

        var element = new XElement("Building",
            new XElement("id", Guid.NewGuid().ToString()),
            new XElement("tileX", position.X),
            new XElement("tileY", position.Y),
            new XElement("tilesWide", tilesWide),
            new XElement("tilesHigh", tilesHigh),
            new XElement("maxOccupants", -1),
            new XElement("currentOccupants", 0),
            new XElement("daysOfConstructionLeft", 0),
            new XElement("daysUntilUpgrade", 0),
            new XElement("buildingType", buildingType),
            BuildingPaintColorEditor.CreateDefault(),
            new XElement("humanDoor", new XElement("X", -1), new XElement("Y", -1)),
            new XElement("animalDoor", new XElement("X", -1), new XElement("Y", -1)),
            new XElement("animalDoorOpen", false),
            new XElement("animalDoorOpenAmount", 0),
            new XElement("magical", magical),
            new XElement("fadeWhenPlayerIsBehind", true),
            new XElement("owner", 0),
            new XElement("newConstructionTimer", 0),
            new XElement("skinId", new XElement("string", new XAttribute(XsiNil, "true"))),
            // hayCapacity/buildingChests are real Building NetFields previously missing
            // entirely (see MAP_AUDIT.md 2.1) - hayCapacity only matters for Silo (0 is inert
            // for everything else); an empty buildingChests self-heals on next game load
            // (confirmed: Building.load() auto-creates any chest Data/Buildings.json's own
            // Chests list calls for that isn't already present - e.g. Junimo Hut's "Output"
            // chest - so there's no need to hand-construct one here).
            new XElement("hayCapacity", hayCapacity),
            new XElement("buildingChests"));

        if (buildingType == "Shed")
            element.Add(BuildingIndoorsEditor.CreateDefaultShed(position));
        else if (indoorMap is not null)
            element.Add(BuildingIndoorsEditor.CreateDefaultAnimalHouse(indoorMap, animalLimit, position));

        container.Add(element);
        return new BuildingEditor(element);
    }

    /// <summary>
    /// Materializes the player's house as a real, editable Building. Most saves don't have one:
    /// the base game only creates this element lazily via Farm.AddDefaultBuilding/AddDefaultBuildings
    /// ("if missing"), called before the player ever sees their farm - saves from before that
    /// existed, or that simply haven't been re-saved since, compute the farmhouse's position
    /// from the map's FarmHouseEntry property instead (see Farm.GetMainFarmHouseEntry,
    /// GetStarterFarmhouseLocation in the decompiled source) and have no save-tracked position
    /// to move at all. This exists so the map tab has something real to move once - the actual
    /// game accepts it as-is on next load (AddDefaultBuilding only ever constructs one "if
    /// missing"), and re-derives nonInstancedIndoorsName from Data/Buildings.json's
    /// NonInstancedIndoorLocation on its own regardless of what's set here (confirmed:
    /// Building.load() overwrites it unconditionally for buildings with that data field set) -
    /// it's set here anyway for defensiveness before that first reload. Every other field here
    /// mirrors AddBuilding's already-verified template; only tilesWide/tilesHigh (9x5) and
    /// humanDoor ((5,2), not (-1,-1)) differ, both taken directly from Data/Buildings.json's
    /// "Farmhouse" entry. Callers must only invoke this when no "Farmhouse" building already
    /// exists (see FarmMapEditor.Buildings) - Farm.GetMainFarmHouse() finds the house by
    /// searching for buildingType "Farmhouse", so a second one would break that lookup.
    /// </summary>
    public BuildingEditor AddFarmhouse(TilePosition position)
    {
        var container = _farmLocation.Element("buildings");
        if (container is null)
        {
            container = new XElement("buildings");
            _farmLocation.Add(container);
        }

        var element = new XElement("Building",
            new XElement("id", Guid.NewGuid().ToString()),
            new XElement("tileX", position.X),
            new XElement("tileY", position.Y),
            new XElement("tilesWide", 9),
            new XElement("tilesHigh", 5),
            new XElement("maxOccupants", -1),
            new XElement("currentOccupants", 0),
            new XElement("daysOfConstructionLeft", 0),
            new XElement("daysUntilUpgrade", 0),
            new XElement("buildingType", "Farmhouse"),
            BuildingPaintColorEditor.CreateDefault(),
            new XElement("humanDoor", new XElement("X", 5), new XElement("Y", 2)),
            new XElement("animalDoor", new XElement("X", -1), new XElement("Y", -1)),
            new XElement("animalDoorOpen", false),
            new XElement("animalDoorOpenAmount", 0),
            new XElement("magical", false),
            new XElement("fadeWhenPlayerIsBehind", true),
            new XElement("owner", 0),
            new XElement("newConstructionTimer", 0),
            new XElement("nonInstancedIndoorsName", "FarmHouse"),
            new XElement("skinId", new XElement("string", new XAttribute(XsiNil, "true"))),
            new XElement("hayCapacity", 0),
            new XElement("buildingChests"));

        container.Add(element);
        return new BuildingEditor(element);
    }

    public void Remove(TreeEditor tree) => tree.Item.Remove();
    public void Remove(FruitTreeEditor fruitTree) => fruitTree.Item.Remove();
    public void Remove(GrassEditor grass) => grass.Item.Remove();
    public void Remove(HoeDirtEditor dirt) => dirt.Item.Remove();
    public void Remove(ResourceClumpEditor clump) => clump.Element.Remove();
    public void Remove(PlacedObjectEditor placedObject) => placedObject.WrappingItem.Remove();
    public void Remove(BuildingEditor building) => building.Element.Remove();
    public void Remove(BushEditor bush) => bush.Element.Remove();
    public void Remove(FlooringEditor flooring) => flooring.Item.Remove();

    /// <summary>Wipes every real, placed entity on this Farm - the "blank slate" half of the
    /// Farm tab's Regenerate Farm feature (see MapTabViewModel.RegenerateFarm). Clears
    /// terrainFeatures (Tree/Grass/HoeDirt/Flooring, and any unmodeled type too - nothing left
    /// half-stale on the new map's terrain), objects, resourceClumps, largeTerrainFeatures
    /// (Bush), and buildings entirely. Deliberately does NOT touch the FarmHouse or Greenhouse -
    /// neither is tracked in these containers at all (see BuildingEditor.NonInstancedIndoorsName
    /// remarks: both are permanent, independent top-level &lt;GameLocation&gt; entries, not
    /// something a &lt;Building&gt; element's removal here affects). A Barn/Coop/Shed's own
    /// nested &lt;indoors&gt; sub-location (a child element of its own &lt;Building&gt;, not a
    /// separate top-level &lt;locations&gt; entry) is removed automatically along with its
    /// parent Building element here - no separate interior cleanup needed.</summary>
    public void ClearAllContent()
    {
        _farmLocation.Element("terrainFeatures")?.RemoveNodes();
        _farmLocation.Element("objects")?.RemoveNodes();
        _farmLocation.Element("resourceClumps")?.RemoveNodes();
        _farmLocation.Element("largeTerrainFeatures")?.RemoveNodes();
        _farmLocation.Element("buildings")?.RemoveNodes();
    }

    public void Move(TreeEditor tree, TilePosition newPosition) => tree.Move(newPosition);
    public void Move(GrassEditor grass, TilePosition newPosition) => grass.Move(newPosition);
    public void Move(HoeDirtEditor dirt, TilePosition newPosition) => dirt.Move(newPosition);
    public void Move(ResourceClumpEditor clump, TilePosition newPosition) => clump.Move(newPosition);
    public void Move(PlacedObjectEditor placedObject, TilePosition newPosition) => placedObject.Move(newPosition);
    public void Move(BuildingEditor building, TilePosition newPosition) => building.Move(newPosition);
    public void Move(BushEditor bush, TilePosition newPosition) => bush.Move(newPosition);

    /// <summary>
    /// Copy/paste and duplicate (see MapTabViewModel) - deep-clones the source's own wrapping
    /// XML node (the &lt;item&gt; for a tile-dictionary entry, the flat-list element for
    /// Building/Bush/ResourceClump), re-parents the clone into the same real container the
    /// original lives in, wraps it in a fresh Editor instance, then repositions via that
    /// instance's own already-real Move() - the same one used for drag-to-move, so this needs no
    /// new per-kind field-copying logic. A cloned Building gets a fresh &lt;id&gt; GUID (real,
    /// confirmed field - uniqueness matters, unlike every other kind here which has no such id).
    /// </summary>
    public object CloneEntityAt(object source, TilePosition newPosition)
    {
        // Every tile-dictionary entry (Tree/Grass/HoeDirt/Flooring/Object) is
        // <item><key>...</key><value>{single real element}</value></item> - the constructors take
        // that single real element directly (TreeEditor._feature.Parent!.Parent! climbs value then
        // item to find Item, so passing <value> itself here instead of its child would corrupt
        // that climb by one level), matching DictionaryEntries' own "value's single child" shape.
        switch (source)
        {
            case TreeEditor tree:
            {
                var clonedItem = new XElement(tree.Item);
                TerrainFeaturesContainer().Add(clonedItem);
                var result = new TreeEditor(tree.Position, clonedItem.Element("value")!.Elements().Single());
                result.Move(newPosition);
                return result;
            }
            case GrassEditor grass:
            {
                var clonedItem = new XElement(grass.Item);
                TerrainFeaturesContainer().Add(clonedItem);
                var result = new GrassEditor(grass.Position, clonedItem.Element("value")!.Elements().Single());
                result.Move(newPosition);
                return result;
            }
            case HoeDirtEditor dirt:
            {
                var clonedItem = new XElement(dirt.Item);
                TerrainFeaturesContainer().Add(clonedItem);
                var result = new HoeDirtEditor(dirt.Position, clonedItem.Element("value")!.Elements().Single());
                result.Move(newPosition);
                return result;
            }
            case FlooringEditor flooring:
            {
                var clonedItem = new XElement(flooring.Item);
                TerrainFeaturesContainer().Add(clonedItem);
                var result = new FlooringEditor(flooring.Position, clonedItem.Element("value")!.Elements().Single());
                result.Move(newPosition);
                return result;
            }
            case PlacedObjectEditor placedObject:
            {
                var clonedItem = new XElement(placedObject.WrappingItem);
                _farmLocation.Element("objects")!.Add(clonedItem);
                var result = new PlacedObjectEditor(placedObject.Position, clonedItem.Element("value")!.Elements().Single());
                result.Move(newPosition);
                return result;
            }
            case ResourceClumpEditor clump:
            {
                var clonedElement = new XElement(clump.Element);
                _farmLocation.Element("resourceClumps")!.Add(clonedElement);
                var result = new ResourceClumpEditor(clonedElement);
                result.Move(newPosition);
                return result;
            }
            case BuildingEditor building:
            {
                var clonedElement = new XElement(building.Element);
                clonedElement.Element("id")!.Value = Guid.NewGuid().ToString();
                _farmLocation.Element("buildings")!.Add(clonedElement);
                var result = new BuildingEditor(clonedElement);
                result.Move(newPosition);
                return result;
            }
            case BushEditor bush:
            {
                var clonedElement = new XElement(bush.Element);
                _farmLocation.Element("largeTerrainFeatures")!.Add(clonedElement);
                var result = new BushEditor(clonedElement);
                result.Move(newPosition);
                return result;
            }
            default:
                throw new InvalidOperationException($"Unknown entity source type: {source.GetType()}.");
        }
    }

    /// <summary>
    /// Shifts every placed tree/grass/hoe-dirt/fruit-tree/object/bush/furniture in this location
    /// by (dx, dy) tiles - the same operation decompiled GameLocation.shiftContents() performs,
    /// confirmed as exactly what FarmHouse.moveObjectsForHouseUpgrade() calls when the player's
    /// house upgrade level changes (see MapTabViewModel.OnHouseUpgradeLevelChanged, the actual
    /// caller - upgrading/downgrading the house resizes its interior, so without this, existing
    /// content stays at its old coordinates, which usually land outside or in the wrong part of
    /// the new layout: the real, reported "furniture floating near the top-left" bug). Buildings
    /// and resourceClumps are deliberately NOT shifted - the real shiftContents() doesn't touch
    /// them either (a FarmHouse interior has neither).
    ///
    /// Shifting an EXISTING placed Furniture (FurnitureEditor.Move) just needs its own
    /// tileLocation/boundingBox/defaultBoundingBox fields nudged by the same delta - confirmed
    /// via decompiled Object.TileLocation (whose setter calls RecalculateBoundingBox(), which for
    /// Furniture just repositions boundingBox to the new tile at its EXISTING width/height).
    /// Furniture.drawPosition is deliberately NOT shifted - see FurnitureEditor.Move remarks.
    /// </summary>
    public void ShiftContents(int dx, int dy)
    {
        foreach (var tree in Trees) tree.Move(new TilePosition(tree.Position.X + dx, tree.Position.Y + dy));
        foreach (var grass in Grass) grass.Move(new TilePosition(grass.Position.X + dx, grass.Position.Y + dy));
        foreach (var dirt in HoeDirtTiles) dirt.Move(new TilePosition(dirt.Position.X + dx, dirt.Position.Y + dy));
        foreach (var fruitTree in FruitTrees) fruitTree.Move(new TilePosition(fruitTree.Position.X + dx, fruitTree.Position.Y + dy));
        foreach (var obj in Objects) obj.Move(new TilePosition(obj.Position.X + dx, obj.Position.Y + dy));
        foreach (var bush in Bushes) bush.Move(new TilePosition(bush.Position.X + dx, bush.Position.Y + dy));
        foreach (var furniture in Furniture) furniture.Move(new TilePosition(furniture.Position.X + dx, furniture.Position.Y + dy));
    }

    /// <summary>
    /// Walks a `&lt;name&gt;&lt;item&gt;&lt;key&gt;&lt;Vector2&gt;X/Y&lt;/Vector2&gt;&lt;/key&gt;
    /// &lt;value&gt;{single child}&lt;/value&gt;&lt;/item&gt;...&lt;/name&gt;` tile dictionary,
    /// yielding each entry's position and its value's single child element.
    /// </summary>
    private IEnumerable<(TilePosition Position, XElement Value)> DictionaryEntries(string containerName)
    {
        var container = _farmLocation.Element(containerName);
        if (container is null)
            yield break;

        foreach (var item in container.Elements("item"))
        {
            var vector = item.Element("key")?.Element("Vector2");
            var value = item.Element("value")?.Elements().FirstOrDefault();
            if (vector is null || value is null)
                continue;

            yield return (new TilePosition(vector.GetChildInt("X"), vector.GetChildInt("Y")), value);
        }
    }
}
