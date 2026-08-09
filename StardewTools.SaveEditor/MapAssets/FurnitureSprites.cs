using System.IO;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// TileSheets/furniture.png loader - unlike ObjectSprites/HoeDirtSprites, no cell-math is needed
/// here: a placed FurnitureEditor already carries its own real sourceRect/boundingBox (written
/// once at placement time by FarmMapEditor.AddFurniture/PlaceableFurnitureCatalog, mirroring how
/// the real game itself computes and persists these rather than re-deriving them every draw), so
/// rendering just needs the bitmap itself.
/// </summary>
public static class FurnitureSprites
{
    private static Bitmap? _cached;
    private static string? _cachedFolder;

    public static bool TryGetBitmap(string contentFolder, out Bitmap bitmap)
    {
        bitmap = null!;

        if (_cachedFolder != contentFolder)
        {
            var path = Path.Combine(contentFolder, "TileSheets", "furniture.png");
            if (!File.Exists(path))
                return false;

            _cached = new Bitmap(path);
            _cachedFolder = contentFolder;
        }

        if (_cached is null)
            return false;

        bitmap = _cached;
        return true;
    }
}
