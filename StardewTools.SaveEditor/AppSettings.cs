using System;
using System.IO;
using System.Text.Json;

namespace StardewTools.SaveEditor;

/// <summary>
/// Small local settings file for things that should survive across app launches but aren't
/// part of any save - currently just the Map tab's extracted-assets folder path. Stored
/// under the user's config directory, never in the repo (it's a machine-local path).
/// </summary>
public sealed class AppSettings
{
    public string? MapContentFolder { get; set; }

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
