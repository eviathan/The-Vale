using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using StardewTools.Core.Models;

namespace StardewTools.SaveEditor.ViewModels;

/// <summary>
/// Shell for the "Save File" tab: owns the loaded save and the Open/Save flow, and hands
/// the loaded SaveGameEditor to each sub-tab. All the sub-tabs write straight through to
/// the in-memory save as their fields change - Save() just flushes that to disk.
/// </summary>
public partial class SaveEditorViewModel : ViewModelBase
{
    private SaveGameEditor? _save;
    private string? _filePath;

    [ObservableProperty] private string _statusMessage = "Open a Stardew Valley save file to begin.";
    [ObservableProperty] private bool _isSaveLoaded;

    public PlayerTabViewModel Player { get; } = new();
    public InventoryTabViewModel Inventory { get; } = new();
    public StatsTabViewModel Stats { get; } = new();
    public AchievementsTabViewModel Achievements { get; } = new();
    public RelationshipsTabViewModel Relationships { get; } = new();
    public StorageTabViewModel Storage { get; } = new();
    public FarmTabViewModel Farm { get; } = new();
    public MapTabViewModel Map { get; } = new();

    public void Load(string path)
    {
        _save = SaveGameEditor.Load(path);
        _filePath = path;

        Player.Bind(_save);
        Inventory.Bind(_save);
        Stats.Bind(_save);
        Achievements.Bind(_save);
        Relationships.Bind(_save);
        Storage.Bind(_save);
        Farm.Bind(_save);
        Map.Bind(_save);

        IsSaveLoaded = true;
        StatusMessage = $"Loaded {Path.GetFileName(path)}";

        var settings = AppSettings.Load();
        settings.LastSaveFilePath = path;
        settings.Save();
    }

    public void Save()
    {
        if (_save is null || _filePath is null)
            return;

        // Keep one pristine copy of whatever the file looked like before we ever touched it.
        var backupPath = _filePath + ".bak";
        if (!File.Exists(backupPath))
            File.Copy(_filePath, backupPath);

        _save.Save(_filePath);
        StatusMessage = $"Saved {Path.GetFileName(_filePath)} (backup: {Path.GetFileName(backupPath)})";
    }
}
