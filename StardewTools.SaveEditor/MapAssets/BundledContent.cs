using System;
using System.IO;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// The game's full asset extraction (~175MB), extracted once by a developer (via the Map
/// tab's Auto-Extract) and committed to the repo under /Resources, so a normal build renders
/// real tile art out of the box with no per-machine extraction or path setup. Bundled in full,
/// not just the folders currently read, since more of the map editor (buildings, grass,
/// furniture, etc.) will draw real sprites from it over time. The csproj copies
/// /Resources/Content (unpacked) next to the built exe.
/// </summary>
public static class BundledContent
{
    public static string FolderPath { get; } = Path.Combine(AppContext.BaseDirectory, "Content (unpacked)");

    public static bool IsAvailable => Directory.Exists(FolderPath);
}
