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
