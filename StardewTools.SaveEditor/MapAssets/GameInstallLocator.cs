using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Best-effort search for an existing StardewXnbHack extraction next to a local game
/// install, so the Map tab can find real tile art on its own before falling back to
/// asking the user to browse for it. Doesn't create anything - only looks.
/// </summary>
public static class GameInstallLocator
{
    private static IEnumerable<string> CandidateGameFolders()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Steam (macOS)
        yield return Path.Combine(home, "Library/Application Support/Steam/steamapps/common/Stardew Valley/Contents/MacOS");
        // GOG Galaxy / direct .app install (macOS)
        yield return "/Applications/Stardew Valley.app/Contents/MacOS";
        // Steam (Windows/Linux, in case this ever runs there)
        yield return Path.Combine(home, ".steam/steam/steamapps/common/Stardew Valley");
        yield return @"C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley";
    }

    /// <summary>Returns the first "Content (unpacked)" folder found next to a candidate game install, or null.</summary>
    public static string? FindExtractedContentFolder()
        => CandidateGameFolders()
            .Select(gameFolder => Path.Combine(gameFolder, "Content (unpacked)"))
            .FirstOrDefault(Directory.Exists);
}
