using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Real tool icons from TileSheets/tools.png (336x384, confirmed) - a 16x16-cell sheet, same
/// math as Game1.getSquareSourceRectForNonStandardTileSheet (X = (index*16) % textureWidth,
/// Y = (index*16/textureWidth)*16, confirmed against the decompiled ToolDataDefinition.GetSourceRect).
/// </summary>
public static class ToolSprites
{
    private static Bitmap? _cached;
    private static string? _cachedFolder;

    public static bool TryGetBitmap(string contentFolder, out Bitmap bitmap)
    {
        if (_cachedFolder != contentFolder)
        {
            var path = Path.Combine(contentFolder, "TileSheets", "tools.png");
            _cached = File.Exists(path) ? new Bitmap(path) : null;
            _cachedFolder = contentFolder;
        }

        bitmap = _cached!;
        return _cached is not null;
    }

    public static bool TryGetSprite(string contentFolder, int spriteIndex, out Bitmap bitmap, out Rect source)
    {
        if (!TryGetBitmap(contentFolder, out bitmap))
        {
            source = default;
            return false;
        }

        var textureWidth = (int)bitmap.PixelSize.Width;
        var x = spriteIndex * 16 % textureWidth;
        var y = spriteIndex * 16 / textureWidth * 16;
        source = new Rect(x, y, 16, 16);
        return true;
    }
}
