using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Drives StardewXnbHack (github.com/Pathoschild/StardewXnbHack, MIT licensed - just code,
/// no game assets) end-to-end so extracting real tile art is a button in this app rather
/// than a manual "download a tool, drop it in your game folder, run it from a terminal,
/// then browse to the output" chore. Downloads the tool itself on demand (cached in the game
/// folder for next time); still requires SMAPI to already be installed, same as the Trainer
/// mod, since that's how the tool reads assets through a real game instance.
/// </summary>
public static class TileArtExtractor
{
    private const string ToolExeName = "StardewXnbHack";

    public static async Task<string> ExtractAsync(string gameFolder, IProgress<string>? progress = null)
    {
        var toolPath = Path.Combine(gameFolder, ToolExeName);
        if (!File.Exists(toolPath))
        {
            progress?.Report("Downloading StardewXnbHack...");
            await DownloadToolAsync(gameFolder);
        }

        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(toolPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        progress?.Report("Extracting assets (this reads every game asset through a temporary game instance - can take a minute)...");

        var startInfo = new System.Diagnostics.ProcessStartInfo(toolPath)
        {
            WorkingDirectory = gameFolder,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException("Couldn't start StardewXnbHack.");

        // StardewXnbHack prints a line per asset processed (thousands of them) - redirecting
        // stdout/stderr without actively draining them fills the OS pipe buffer and makes the
        // child process block on its own output, hanging forever. Reading continuously here,
        // concurrently with the wait below (not after it), is what actually prevents that.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        // The tool prints a "Press any key to exit" prompt when done; feeding it a newline
        // lets it exit cleanly instead of hanging forever waiting for real console input.
        var stdinTask = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            try { await process.StandardInput.WriteLineAsync(); } catch (Exception) { /* process may have already exited */ }
        });

        await process.WaitForExitAsync();
        await stdinTask;
        var stderr = await stderrTask;
        await stdoutTask;

        var outputFolder = Path.Combine(gameFolder, "Content (unpacked)");
        if (!Directory.Exists(outputFolder))
            throw new InvalidOperationException($"Extraction didn't produce an output folder. {stderr}");

        progress?.Report("Done.");
        return outputFolder;
    }

    private static async Task DownloadToolAsync(string gameFolder)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("StardewTools");

        var releaseJson = await http.GetStringAsync("https://api.github.com/repos/Pathoschild/StardewXnbHack/releases/latest");
        using var release = JsonDocument.Parse(releaseJson);

        var platformTag = OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : "Linux";
        var asset = release.RootElement.GetProperty("assets").EnumerateArray()
            .FirstOrDefault(a => a.GetProperty("name").GetString()?.Contains(platformTag) == true);

        if (asset.ValueKind == JsonValueKind.Undefined)
            throw new InvalidOperationException($"No StardewXnbHack release asset found for {platformTag}.");

        var downloadUrl = asset.GetProperty("browser_download_url").GetString()!;

        var tempZip = Path.GetTempFileName();
        try
        {
            await using (var stream = await http.GetStreamAsync(downloadUrl))
            await using (var file = File.Create(tempZip))
                await stream.CopyToAsync(file);

            using var archive = ZipFile.OpenRead(tempZip);
            // The zip contains one folder (e.g. "StardewXnbHack 1.1.2 for macOS/StardewXnbHack") - flatten it.
            var entry = archive.Entries.First(e => e.Name == ToolExeName);
            entry.ExtractToFile(Path.Combine(gameFolder, ToolExeName), overwrite: true);
        }
        finally
        {
            File.Delete(tempZip);
        }
    }
}
