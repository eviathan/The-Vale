using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Resolves a farm animal's real type (e.g. "White Cow") to its own dedicated texture file and
/// standing-frame source rect - unlike springobjects.png/BigCraftables' shared sheets, every real
/// animal type has its own separate file (Data/FarmAnimals.json's own Texture field, e.g.
/// "Animals\White Cow" - confirmed exhaustively, every entry has one), same "many small per-type
/// files, not one shared sheet" shape as TreeSprites. Deliberately always the adult/non-swimming
/// sprite - this tool doesn't model age/water state, same scope call as everywhere else here.
///
/// Real animals don't persist a save position at all (FarmAnimalEditor's own remarks - position is
/// [XmlIgnore], re-randomized fresh by AnimalHouse.resetPositionsOfAllAnimals every time the
/// location loads/is entered), so there is no "correct" position to render at. This tool picks a
/// deterministic pseudo-random position per animal (seeded by its own real, stable MyId) so it
/// looks like a real scattered herd and stays visually stable across renders/sessions, rather than
/// jumping around on every repaint the way a fresh System.Random each frame would.
/// </summary>
public static class AnimalSprites
{
    private static readonly Dictionary<string, Bitmap> BitmapCache = new();
    private static Dictionary<string, (string Texture, int Width, int Height)>? _animalData;
    private static string? _animalDataFolder;

    /// <summary>Frame (0,0) of the animal's own texture - decompiled FarmAnimal.draw()'s own
    /// Sprite.draw call passes currentFrame 0 for the base call (movement/animation frames are
    /// added on top at runtime) - this is the same real "standing still" pose the game itself
    /// starts every animation from.</summary>
    public static bool TryGetSprite(string contentFolder, string animalType, out Bitmap bitmap, out Rect source)
    {
        bitmap = null!;
        source = default;

        if (_animalDataFolder != contentFolder)
        {
            _animalData = LoadAnimalData(contentFolder);
            _animalDataFolder = contentFolder;
        }

        if (_animalData is null || !_animalData.TryGetValue(animalType, out var data))
            return false;

        if (!BitmapCache.TryGetValue(data.Texture, out var cached))
        {
            var path = Path.Combine(contentFolder, data.Texture.Replace('\\', Path.DirectorySeparatorChar) + ".png");
            if (!File.Exists(path))
                return false;

            cached = new Bitmap(path);
            BitmapCache[data.Texture] = cached;
        }

        bitmap = cached;
        source = new Rect(0, 0, data.Width, data.Height);
        return true;
    }

    /// <summary>Deterministic pseudo-random offset (in tiles, from the map's own top-left) for
    /// this animal within a WxH area - see class remarks for why this is seeded rather than truly
    /// random. Kept away from the outer edge (a 2-tile margin) so animals don't render on top of
    /// walls in a typical Barn/Coop interior.</summary>
    public static (int X, int Y) PseudoRandomPosition(long myId, int mapWidth, int mapHeight)
    {
        var usableWidth = System.Math.Max(1, mapWidth - 4);
        var usableHeight = System.Math.Max(1, mapHeight - 4);
        var seed = unchecked((int)(myId ^ (myId >> 32)));
        var rng = new System.Random(seed);
        return (2 + rng.Next(usableWidth), 2 + rng.Next(usableHeight));
    }

    private static Dictionary<string, (string, int, int)>? LoadAnimalData(string contentFolder)
    {
        var path = Path.Combine(contentFolder, "Data", "FarmAnimals.json");
        if (!File.Exists(path))
            return null;

        var result = new Dictionary<string, (string, int, int)>();
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var el = prop.Value;
            var texture = el.TryGetProperty("Texture", out var t) ? t.GetString() : null;
            var width = el.TryGetProperty("SpriteWidth", out var w) ? w.GetInt32() : 16;
            var height = el.TryGetProperty("SpriteHeight", out var h) ? h.GetInt32() : 16;
            if (!string.IsNullOrEmpty(texture))
                result[prop.Name] = (texture, width, height);
        }
        return result;
    }
}
