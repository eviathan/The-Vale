using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>Which real in-game icon a StardewIcon renders - see MenuChrome.IconSourceRects for
/// each one's real, decompiled-source-verified location in Cursors.png.</summary>
public enum IconKind
{
    Trash,
    Close,
    ArrowUp,
    ArrowDown,
    ArrowLeft,
    ArrowRight,
    Ok,
    Plus,
    Minus,
}

/// <summary>
/// Real Stardew Valley UI chrome - the classic tan/wood dialog border (Maps/MenuTiles.png,
/// Game1.menuTexture) and a handful of action icons (LooseSprites/Cursors.png, Game1.mouseCursors).
/// Every source rect here was pulled directly from the decompiled game's own draw code
/// (IClickableMenu.drawTextureBox for the border; InventoryPage/MenuWithInventory's trashCan,
/// IClickableMenu's upperRightCloseButton, and the various menus' scroll/page arrows and
/// OptionsPlusMinus's +/- for the icons), not guessed - same discipline as ObjectSprites/
/// BuildingSprites/TreeSprites. Loaded from BundledContent (a fixed app asset, not per-save
/// content like the Map tab's extracted sprites), so no content-folder parameter is needed.
/// </summary>
public static class MenuChrome
{
    /// <summary>IClickableMenu.drawTextureBox's default box source rect (Game1.menuTexture,
    /// Maps/MenuTiles.png) - a 3x3 grid of 20x20px pieces (cornerSize = sourceRect.Width/3).
    /// StardewPanel slices this same rect the same way the game itself does.</summary>
    public static readonly Rect BorderSourceRect = new(0, 256, 60, 60);
    public const int BorderCornerSize = 20;

    public static readonly IReadOnlyDictionary<IconKind, Rect> IconSourceRects = new Dictionary<IconKind, Rect>
    {
        [IconKind.Trash] = new Rect(564, 102, 18, 26),
        [IconKind.Close] = new Rect(337, 494, 12, 12),
        [IconKind.ArrowUp] = new Rect(421, 459, 11, 12),
        [IconKind.ArrowDown] = new Rect(421, 472, 11, 12),
        [IconKind.ArrowLeft] = new Rect(352, 495, 12, 11),
        [IconKind.ArrowRight] = new Rect(365, 495, 12, 11),
        [IconKind.Ok] = new Rect(128, 256, 64, 64),
        [IconKind.Plus] = new Rect(184, 345, 7, 8),
        [IconKind.Minus] = new Rect(177, 345, 7, 8),
    };

    private static Bitmap? _menuTiles;
    private static Bitmap? _cursors;
    private static bool _menuTilesLoadAttempted;
    private static bool _cursorsLoadAttempted;

    public static Bitmap? MenuTiles
    {
        get
        {
            if (!_menuTilesLoadAttempted)
            {
                _menuTilesLoadAttempted = true;
                var path = Path.Combine(BundledContent.FolderPath, "Maps", "MenuTiles.png");
                if (File.Exists(path))
                    _menuTiles = new Bitmap(path);
            }

            return _menuTiles;
        }
    }

    public static Bitmap? Cursors
    {
        get
        {
            if (!_cursorsLoadAttempted)
            {
                _cursorsLoadAttempted = true;
                var path = Path.Combine(BundledContent.FolderPath, "LooseSprites", "Cursors.png");
                if (File.Exists(path))
                    _cursors = new Bitmap(path);
            }

            return _cursors;
        }
    }

    public static bool TryGetIcon(IconKind kind, out Bitmap bitmap, out Rect source)
    {
        bitmap = null!;
        source = default;

        if (Cursors is not { } cursors || !IconSourceRects.TryGetValue(kind, out source))
            return false;

        bitmap = cursors;
        return true;
    }
}
