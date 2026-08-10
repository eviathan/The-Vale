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

    /// <summary>
    /// Resource clumps (stumps, hollow logs, boulders) are also indexed into springobjects.png
    /// - confirmed by cropping the sheet around index 600 and visually finding a 2-tile-wide
    /// stump there - but unlike a normal 1x1 object, the source rect spans the clump's full
    /// width/height in cells starting from that index's top-left cell (ported from the
    /// decompiled ResourceClump.draw(): width/height * 16, not a fixed 16x16).
    /// </summary>
    public static bool TryGetClumpSprite(string contentFolder, int parentSheetIndex, int widthInTiles, int heightInTiles, out Bitmap bitmap, out Rect source)
    {
        if (!TryGetSprite(contentFolder, parentSheetIndex, out bitmap, out var cell))
        {
            source = default;
            return false;
        }

        source = new Rect(cell.X, cell.Y, CellSize * widthInTiles, CellSize * heightInTiles);
        return true;
    }

    /// <summary>The static (non-animated) flame cell of the real torch-on-object overlay
    /// (decompiled Torch.drawBasicTorch: source rect (336,48,16,16) with Y+=8/Height/=2 applied,
    /// i.e. the bottom half of that cell - the animated glow/flicker layers drawBasicTorch also
    /// draws aren't reproduced here, same "no per-frame animation" scope every other sprite in
    /// this tool already has). Same springobjects.png sheet as every other Object icon.</summary>
    public static bool TryGetTorchOverlaySprite(string contentFolder, out Bitmap bitmap, out Rect source)
    {
        if (!TryGetSprite(contentFolder, 0, out bitmap, out _))
        {
            source = default;
            return false;
        }

        source = new Rect(336, 56, 16, 8);
        return true;
    }
}
