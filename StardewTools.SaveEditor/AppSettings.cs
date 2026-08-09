using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace StardewTools.SaveEditor;

/// <summary>A (RealItemId, IsBigCraftable) pair - both fields are needed to re-resolve a real
/// PlaceableItem, since RealItemId alone isn't unique across the Objects/BigCraftables id
/// spaces (real numeric collisions between the two, e.g. index 68 is a different item in
/// each). See PlaceableItem.RealItemId's own remarks.</summary>
public sealed class RecentPlaceableItemRef
{
    public string ItemId { get; set; } = "";
    public bool IsBigCraftable { get; set; }
}

/// <summary>A reusable named building-paint job - every real BuildingPaintColorEditor field
/// across all 3 slots, so applying a saved style reproduces the paint job exactly, not just the
/// picker-visible Hue/Saturation. See PaintStyleStore/SavedPaintStyle (ViewModels layer) for the
/// live in-memory view this backs.</summary>
public sealed class SavedPaintStyleRef
{
    public string Name { get; set; } = "";
    public bool Color1Default { get; set; }
    public int Color1Hue { get; set; }
    public int Color1Saturation { get; set; }
    public int Color1Lightness { get; set; }
    public bool Color2Default { get; set; }
    public int Color2Hue { get; set; }
    public int Color2Saturation { get; set; }
    public int Color2Lightness { get; set; }
    public bool Color3Default { get; set; }
    public int Color3Hue { get; set; }
    public int Color3Saturation { get; set; }
    public int Color3Lightness { get; set; }
}

/// <summary>
/// Small local settings file for things that should survive across app launches but aren't
/// part of any save - currently just the Map tab's extracted-assets folder path. Stored
/// under the user's config directory, never in the repo (it's a machine-local path).
/// </summary>
public sealed class AppSettings
{
    public string? MapContentFolder { get; set; }
    public string? LastSaveFilePath { get; set; }

    /// <summary>Most-recently-placed object-picker items, newest first - see MapTabViewModel's
    /// RecentPlaceableItems (in-memory, live view) and RecordRecentlyUsedItem (what pushes here).</summary>
    public List<RecentPlaceableItemRef> RecentPlaceableItems { get; set; } = new();

    /// <summary>User-saved building paint jobs, reusable across any paintable building - see
    /// PaintStyleStore (ViewModels layer).</summary>
    public List<SavedPaintStyleRef> PaintStyles { get; set; } = new();

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StardewTools", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception)
        {
            // Missing file (first run) or corrupt/old-format JSON - either way, defaults are fine.
            return new AppSettings();
        }
    }

    public void Save()
    {
        var path = FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this));
    }
}
