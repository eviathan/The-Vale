using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Real sprites for placed farm buildings (barn, coop, silo, etc.), reading Data/Buildings.json
/// for the real per-building Texture/SourceRect/SeasonOffset/Skins rather than assuming "the
/// whole PNG is the sprite" - confirmed wrong for at least Pet Bowl (SourceRect (32,0,32,32) out
/// of a 96x128 sheet packing all 4 seasons x 2 frames) by inspecting the actual file. Verified
/// against the decompiled Building.getSourceRect()/ApplySourceRectOffsets(): SourceRect
/// (0,0,0,0) is the game's own "no crop, use the whole texture" sentinel (most buildings use
/// this - Gold Clock, Silo, Well, Fish Pond, Junimo Hut, ... - which is why the old whole-image
/// assumption happened to work for them); anything else is a real crop, shifted per-season by
/// SeasonOffset x Game1.seasonIndex (Spring=0/Summer=1/Fall=2/Winter=3). A skin only swaps
/// Texture - SourceRect/SeasonOffset still come from the base building data (confirmed: skin
/// entries carry no SourceRect of their own in Data/Buildings.json).
/// </summary>
public static class BuildingSprites
{
    private sealed record SpriteData(string Texture, Rect SourceRect, (int X, int Y) SeasonOffset, (double X, double Y) DrawOffset, IReadOnlyDictionary<string, string> SkinTextures);

    private static readonly Dictionary<string, Bitmap?> BitmapCache = new();
    private static IReadOnlyDictionary<string, SpriteData>? _data;
    private static IReadOnlySet<string>? _paintableKeys;

    private static IReadOnlyDictionary<string, SpriteData> Data => _data ??= Load();

    /// <summary>Real, confirmed keys from Data/PaintData.json - only these building "paint data
    /// keys" have an actual _PaintMask texture the game can recolor against (matches the
    /// decompiled Building.GetPaintDataKey()/CanBePainted(): most building types, including the
    /// base Barn/Coop/Shed/Silo/Well/Greenhouse/Slime Hutch tiers, simply have no paint data at
    /// all - painting them is a real, intentional no-op in the actual game too, not something
    /// this tool is missing).</summary>
    private static IReadOnlySet<string> PaintableKeys => _paintableKeys ??= LoadPaintableKeys();

    private static HashSet<string> LoadPaintableKeys()
    {
        var path = Path.Combine(BundledContent.FolderPath, "Data", "PaintData.json");
        if (!File.Exists(path))
            return new HashSet<string>();

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();
    }

    /// <summary>Mirrors the decompiled Building.GetPaintDataKey(): a skin's own id is checked
    /// first (a skin can be paintable even if the base building type generally isn't listed
    /// under its own name), then the building type itself, with "Farmhouse" translated to
    /// Data/PaintData.json's "House" entry (confirmed: PaintData has no literal "Farmhouse" key)
    /// and "Cabin" (a generic/base cabin type, not one of this app's specific cabin skins) to
    /// "Stone Cabin".</summary>
    public static bool CanBePainted(string buildingType, string? skinId)
    {
        if (skinId is not null && PaintableKeys.Contains(skinId))
            return true;

        var lookupName = buildingType switch
        {
            "Farmhouse" => "House",
            "Cabin" => "Stone Cabin",
            _ => buildingType,
        };
        return PaintableKeys.Contains(lookupName);
    }

    private static Dictionary<string, SpriteData> Load()
    {
        var result = new Dictionary<string, SpriteData>();
        var path = Path.Combine(BundledContent.FolderPath, "Data", "Buildings.json");
        if (!File.Exists(path))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var el = prop.Value;

            var texture = TextureFileName(el.TryGetProperty("Texture", out var t) ? t.GetString() : null) ?? prop.Name;

            var sourceRect = default(Rect);
            if (el.TryGetProperty("SourceRect", out var sr))
            {
                var w = sr.TryGetProperty("Width", out var wEl) ? wEl.GetInt32() : 0;
                var h = sr.TryGetProperty("Height", out var hEl) ? hEl.GetInt32() : 0;
                if (w > 0 && h > 0)
                {
                    var x = sr.TryGetProperty("X", out var xEl) ? xEl.GetInt32() : 0;
                    var y = sr.TryGetProperty("Y", out var yEl) ? yEl.GetInt32() : 0;
                    sourceRect = new Rect(x, y, w, h);
                }
            }

            var seasonOffset = (0, 0);
            if (el.TryGetProperty("SeasonOffset", out var so))
            {
                seasonOffset = (
                    so.TryGetProperty("X", out var sx) ? sx.GetInt32() : 0,
                    so.TryGetProperty("Y", out var sy) ? sy.GetInt32() : 0);
            }

            // A compact "X, Y" string (confirmed real format via direct inspection - unlike
            // SourceRect/SeasonOffset above, which are {X,Y} objects), not a JSON object - the
            // same format Data/Buildings.json also uses for e.g. UpgradeSignTile.
            var drawOffset = (0.0, 0.0);
            if (el.TryGetProperty("DrawOffset", out var doEl) && doEl.ValueKind == JsonValueKind.String)
            {
                var parts = doEl.GetString()?.Split(',', StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
                if (parts.Length == 2 && double.TryParse(parts[0], out var dx) && double.TryParse(parts[1], out var dy))
                    drawOffset = (dx, dy);
            }

            var skins = new Dictionary<string, string>();
            if (el.TryGetProperty("Skins", out var skinArray) && skinArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var skin in skinArray.EnumerateArray())
                {
                    var id = skin.TryGetProperty("Id", out var idEl) ? idEl.GetString() : null;
                    var skinTexture = TextureFileName(skin.TryGetProperty("Texture", out var stEl) ? stEl.GetString() : null);
                    if (id is not null && skinTexture is not null)
                        skins[id] = skinTexture;
                }
            }

            result[prop.Name] = new SpriteData(texture, sourceRect, seasonOffset, drawOffset, skins);
        }

        return result;
    }

    /// <summary>"Buildings\Stone Pet Bowl" (game's backslash-separated content path) -> "Stone Pet Bowl" (our flat Buildings/ folder's file name).</summary>
    private static string? TextureFileName(string? contentPath)
        => contentPath?.Split('\\', '/').LastOrDefault();

    private static int SeasonIndex(string season) => season.ToLowerInvariant() switch
    {
        "summer" => 1,
        "fall" => 2,
        "winter" => 3,
        _ => 0,
    };

    /// <summary>Every skin id this building type has (e.g. Pet Bowl: "Stone Pet Bowl"/"Hay Pet
    /// Bowl") - empty for the vast majority of buildings, which have none.</summary>
    public static IReadOnlyList<string> SkinsFor(string buildingType)
        => Data.TryGetValue(buildingType, out var data) ? data.SkinTextures.Keys.ToList() : Array.Empty<string>();

    /// <summary>Real per-building sprite draw offset in native (16px/tile) units - e.g.
    /// Farmhouse's real (-16, 2), confirmed via decompiled Building.draw(): "drawPosition +
    /// drawOffset" where drawOffset = data.DrawOffset * 4f (the game's own fixed 4x building-art
    /// scale). Most buildings are (0,0) (Stable included) - a building whose sprite is centered
    /// differently than its logical tile footprint (Farmhouse's porch/door asymmetry) is the
    /// exception, not the rule, but skipping this for every building meant anything placed flush
    /// against one of these had a visible gap this tool never had (real user report: "in game you
    /// can position the stable right next to the house and it lines up... there is a space
    /// between them" in this tool).</summary>
    public static (double X, double Y) DrawOffsetFor(string buildingType)
        => Data.TryGetValue(buildingType, out var data) ? data.DrawOffset : (0.0, 0.0);

    public static bool TryGetSprite(string contentFolder, string buildingType, string? skinId, string season, out Bitmap bitmap, out Rect source)
    {
        if (!Data.TryGetValue(buildingType, out var data))
        {
            // Unknown to Data/Buildings.json (shouldn't happen for anything PlaceableBuildings
            // offers, but a save could reference a removed/modded type) - fall back to the old
            // whole-image-by-filename behavior rather than failing outright.
            return TryGetWholeImage(contentFolder, buildingType, out bitmap, out source);
        }

        var textureFile = skinId is not null && data.SkinTextures.TryGetValue(skinId, out var skinTexture) ? skinTexture : data.Texture;
        if (!TryGetWholeImage(contentFolder, textureFile, out bitmap, out var fullBounds))
        {
            source = default;
            return false;
        }

        if (data.SourceRect == default)
        {
            source = fullBounds;
            return true;
        }

        var seasonIndex = SeasonIndex(season);
        source = new Rect(
            data.SourceRect.X + data.SeasonOffset.X * seasonIndex,
            data.SourceRect.Y + data.SeasonOffset.Y * seasonIndex,
            data.SourceRect.Width,
            data.SourceRect.Height);
        return true;
    }

    /// <summary>The real texture file name a placed building draws from (accounting for its skin,
    /// if any) - matches the game's own textureName() used to build a paint mask's path
    /// ({textureName()}_PaintMask.png), which is the actual texture file, not the building's
    /// catalog type name (only differs for skinned buildings, e.g. Pet Bowl).</summary>
    public static string TextureFileNameFor(string buildingType, string? skinId)
    {
        if (!Data.TryGetValue(buildingType, out var data))
            return buildingType;
        return skinId is not null && data.SkinTextures.TryGetValue(skinId, out var skinTexture) ? skinTexture : data.Texture;
    }

    /// <summary>The raw, uncropped sheet for a building's default (non-skinned) texture file.
    /// Fish Pond composites multiple layers from this sheet itself (see
    /// FarmMapControl.TryDrawFishPondSprite) and needs the bitmap without an assumed source rect.</summary>
    public static bool TryGetBitmap(string contentFolder, string buildingType, out Bitmap bitmap)
    {
        var textureFile = Data.TryGetValue(buildingType, out var data) ? data.Texture : buildingType;
        return TryGetWholeImage(contentFolder, textureFile, out bitmap, out _);
    }

    private static bool TryGetWholeImage(string contentFolder, string textureFileName, out Bitmap bitmap, out Rect bounds)
    {
        var key = contentFolder + "|" + textureFileName;
        if (!BitmapCache.TryGetValue(key, out var cached))
        {
            var path = Path.Combine(contentFolder, "Buildings", textureFileName + ".png");
            cached = File.Exists(path) ? new Bitmap(path) : null;
            BitmapCache[key] = cached;
        }

        if (cached is null)
        {
            bitmap = null!;
            bounds = default;
            return false;
        }

        bitmap = cached;
        bounds = new Rect(0, 0, cached.PixelSize.Width, cached.PixelSize.Height);
        return true;
    }
}
