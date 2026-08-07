using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StardewTools.SaveEditor.MapAssets;
using StardewTools.SaveEditor.ViewModels;

namespace StardewTools.SaveEditor.Converters;

/// <summary>
/// Small preview icons for the "Place item"/"Place building"/"Add item" picker lists, so
/// scrolling a list of hundreds of entries by name alone isn't the only way to find the right
/// one. Always reads from BundledContent (the tile art committed to the repo) rather than the
/// user's possibly-redirected ContentFolder - these are convenience thumbnails in a picker list,
/// not the actual map render, and BundledContent is guaranteed present out of the box (see
/// MapTabViewModel's ContentFolder default) with no binding path needed from inside a
/// DataTemplate. Handles all three picker entry kinds the Inventory tab's "Add item" sidebar
/// mixes together (PlaceableItem/PlaceableTool/PlaceableWeapon) as well as the Map tab's
/// Object/BigCraftable-only picker (PlaceableItem alone).
/// </summary>
public sealed class PlaceableItemIconConverter : IValueConverter
{
    public static readonly PlaceableItemIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!BundledContent.IsAvailable)
            return null;

        var folder = BundledContent.FolderPath;
        bool found;
        Bitmap bitmap;
        Rect source;

        switch (value)
        {
            case PlaceableItem item:
                found = item.IsBigCraftable
                    ? BigCraftableSprites.TryGetSprite(folder, item.Index, out bitmap, out source)
                    : ObjectSprites.TryGetSprite(folder, item.Index, out bitmap, out source);
                break;
            case PlaceableTool tool:
                found = ToolSprites.TryGetSprite(folder, tool.IconSpriteIndex, out bitmap, out source);
                break;
            case PlaceableWeapon weapon:
                found = WeaponSprites.TryGetSprite(folder, weapon.SpriteIndex, out bitmap, out source);
                break;
            default:
                return null;
        }

        return found ? Crop(bitmap, source) : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    internal static CroppedBitmap Crop(Bitmap bitmap, Rect source)
        => new(bitmap, new PixelRect((int)source.X, (int)source.Y, (int)source.Width, (int)source.Height));
}

/// <summary>
/// Icon for a real inventory/chest item (an <see cref="ItemRowViewModel"/>). Plain objects/
/// BigCraftables resolve via ParentSheetIndex (springobjects.png/Craftables.png); Tool subclasses
/// and MeleeWeapon resolve via IndexOfMenuItemView against tools.png/weapons.png respectively -
/// both are real fields confirmed on real carried examples (an Axe and a Scythe), so this covers
/// every tool/weapon already in a save even though only a handful of tool classes can be
/// fabricated brand-new from the picker (see PlaceableTools remarks). Anything else (Rings,
/// Boots, Hats, Furniture, ...) has no resolvable icon here yet and returns null.
/// </summary>
public sealed class InventoryItemIconConverter : IValueConverter
{
    public static readonly InventoryItemIconConverter Instance = new();

    /// <summary>Every real Data/Tools.json ClassName - broader than PlaceableTools'
    /// fabricatable subset, since an item the player already owns (e.g. a Fishing Rod) should
    /// still show its real icon even though this tool can't fabricate a brand-new one.</summary>
    private static readonly HashSet<string> ToolClasses = new()
    {
        "Axe", "Hoe", "Pickaxe", "WateringCan", "MilkPail", "Pan", "Shears", "FishingRod", "Wand", "Lantern", "GenericTool",
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ItemRowViewModel row || !BundledContent.IsAvailable)
            return null;

        var folder = BundledContent.FolderPath;
        bool found;
        Bitmap bitmap;
        Rect source;

        if (row.Item.ItemType == "MeleeWeapon" && row.Item.IndexOfMenuItemView is int weaponIndex)
        {
            found = WeaponSprites.TryGetSprite(folder, weaponIndex, out bitmap, out source);
        }
        else if (ToolClasses.Contains(row.Item.ItemType) && row.Item.IndexOfMenuItemView is int toolIndex)
        {
            found = ToolSprites.TryGetSprite(folder, toolIndex, out bitmap, out source);
        }
        else if (row.Item.ParentSheetIndex is int objectIndex)
        {
            found = row.Item.BigCraftable
                ? BigCraftableSprites.TryGetSprite(folder, objectIndex, out bitmap, out source)
                : ObjectSprites.TryGetSprite(folder, objectIndex, out bitmap, out source);
        }
        else
        {
            return null;
        }

        return found ? PlaceableItemIconConverter.Crop(bitmap, source) : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class PlaceableBuildingIconConverter : IValueConverter
{
    public static readonly PlaceableBuildingIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not PlaceableBuilding building || !BundledContent.IsAvailable)
            return null;

        if (!BuildingSprites.TryGetBitmap(BundledContent.FolderPath, building.Name, out var bitmap))
            return null;

        // Fish Pond's sheet packs multiple composited layers (see FarmMapControl's Fish Pond
        // special case) - the rim alone (top-left 80x80) reads fine as a small list icon
        // without needing the full water-tint compositing a real map render does.
        var source = building.Name == "Fish Pond"
            ? new PixelRect(0, 0, 80, 80)
            : new PixelRect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);

        return new CroppedBitmap(bitmap, source);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
