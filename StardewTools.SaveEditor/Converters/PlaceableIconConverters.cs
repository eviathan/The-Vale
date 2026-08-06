using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StardewTools.SaveEditor.MapAssets;

namespace StardewTools.SaveEditor.Converters;

/// <summary>
/// Small preview icons for the "Place item"/"Place building" picker dropdowns, so scrolling a
/// list of 700+ objects by name alone isn't the only way to find the right one. Always reads
/// from BundledContent (the tile art committed to the repo) rather than the user's possibly-
/// redirected ContentFolder - these are convenience thumbnails in a picker list, not the actual
/// map render, and BundledContent is guaranteed present out of the box (see MapTabViewModel's
/// ContentFolder default) with no binding path needed from inside a DataTemplate.
/// </summary>
public sealed class PlaceableItemIconConverter : IValueConverter
{
    public static readonly PlaceableItemIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not PlaceableItem item || !BundledContent.IsAvailable)
            return null;

        var folder = BundledContent.FolderPath;
        var found = item.IsBigCraftable
            ? BigCraftableSprites.TryGetSprite(folder, item.Index, out var bitmap, out var source)
            : ObjectSprites.TryGetSprite(folder, item.Index, out bitmap, out source);

        return found ? Crop(bitmap, source) : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static CroppedBitmap Crop(Bitmap bitmap, Rect source)
        => new(bitmap, new PixelRect((int)source.X, (int)source.Y, (int)source.Width, (int)source.Height));
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
