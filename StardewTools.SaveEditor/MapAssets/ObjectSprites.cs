using System;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Resolves a placed object's real ParentSheetIndex to its icon in the game's object sprite
/// sheet (Maps/springobjects.png - confirmed 384x624px = 24 columns x 39 rows of 16px cells;
/// unlike outdoor terrain tiles, this sheet isn't season-swapped, so there's only one file).
/// </summary>
public static class ObjectSprites
{
    private const int Columns = 24;
    private const int CellSize = 16;

    private static Bitmap? _cached;
    private static string? _cachedFolder;

    public static bool TryGetSprite(string contentFolder, int parentSheetIndex, out Bitmap bitmap, out Rect source)
    {
        bitmap = null!;
        source = default;

        if (_cachedFolder != contentFolder)
        {
            var path = Path.Combine(contentFolder, "Maps", "springobjects.png");
            if (!File.Exists(path))
                return false;

            _cached = new Bitmap(path);
            _cachedFolder = contentFolder;
        }

        if (_cached is null)
            return false;

        bitmap = _cached;
        var col = parentSheetIndex % Columns;
        var row = parentSheetIndex / Columns;
        source = new Rect(col * CellSize, row * CellSize, CellSize, CellSize);
        return true;
    }
}
