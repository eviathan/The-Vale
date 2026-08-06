using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Machines/decorations (Furnace, Grandfather Clock, ...) - a different sheet and cell size
/// than plain objects (ObjectSprites): TileSheets/Craftables.png, confirmed 128x1472px (8
/// columns), each cell 16x32 (1 tile wide, 2 tiles tall - these are always taller than their
/// footprint, like buildings/trees). Source rect formula ported from the decompiled
/// Object.getSourceRectForBigCraftable() and pixel-verified by cropping index 68 (Grandfather
/// Clock) and confirming it's actually a clock, not a guess.
/// </summary>
public static class BigCraftableSprites
{
    private const int Columns = 8;
    private const int CellWidth = 16;
    private const int CellHeight = 32;

    private static Bitmap? _cached;
    private static string? _cachedFolder;

    public static bool TryGetSprite(string contentFolder, int index, out Bitmap bitmap, out Rect source)
    {
        if (_cachedFolder != contentFolder)
        {
            var path = Path.Combine(contentFolder, "TileSheets", "Craftables.png");
            _cached = File.Exists(path) ? new Bitmap(path) : null;
            _cachedFolder = contentFolder;
        }

        if (_cached is null)
        {
            bitmap = null!;
            source = default;
            return false;
        }

        var col = index % Columns;
        var row = index / Columns;
        bitmap = _cached;
        source = new Rect(col * CellWidth, row * CellHeight, CellWidth, CellHeight);
        return true;
    }
}
