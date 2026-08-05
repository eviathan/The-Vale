using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Loads a farm map + its tileset images from a folder of assets unpacked by StardewXnbHack
/// (github.com/Pathoschild/StardewXnbHack) - plain .tmx/.png files, not read from .xnb
/// directly. Those extracted files are never bundled with this app or committed to the
/// repo (they're the game's own copyrighted art) - point this at your own local extraction.
/// </summary>
public sealed class MapAssetLoader
{
    private readonly string _mapsFolder;
    private readonly Dictionary<string, Bitmap> _bitmapCache = new();

    public MapAssetLoader(string contentUnpackedFolder)
    {
        _mapsFolder = Path.Combine(contentUnpackedFolder, "Maps");
    }

    public TmxMap LoadFarmMap() => LoadMap("Farm");

    /// <summary>
    /// Loads any location by its save xsi:type name (e.g. "Town", "Beach", "Mine") - these
    /// are the same .tmx files StardewXnbHack extracts for every map in the game, so this
    /// works for anywhere as long as the name matches a real file (some locations, like Mine
    /// levels, aren't a single simple map and won't resolve here).
    /// </summary>
    public TmxMap LoadMap(string locationName) => TmxMap.Load(Path.Combine(_mapsFolder, locationName + ".tmx"));

    public bool HasMap(string locationName) => File.Exists(Path.Combine(_mapsFolder, locationName + ".tmx"));

    /// <summary>
    /// Resolves a tileset's declared image source to a season-appropriate bitmap. Stardew's
    /// outdoor tilesheets follow a "spring_..."/"summer_..."/"fall_..."/"winter_..." naming
    /// convention (confirmed: all four exist on disk for spring_outdoorsTileSheet) - other
    /// tilesets (paths, extra tiles) aren't season-specific and are used as-is.
    /// </summary>
    public Bitmap GetTilesetBitmap(string imageSource, string season)
    {
        var fileName = imageSource.StartsWith("spring_", StringComparison.OrdinalIgnoreCase)
            ? season.ToLowerInvariant() + imageSource["spring".Length..]
            : imageSource;

        if (_bitmapCache.TryGetValue(fileName, out var cached))
            return cached;

        var path = Path.Combine(_mapsFolder, fileName + ".png");
        var bitmap = new Bitmap(path);
        _bitmapCache[fileName] = bitmap;
        return bitmap;
    }
}
