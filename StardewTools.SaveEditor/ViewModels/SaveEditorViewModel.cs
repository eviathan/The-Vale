using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public QuestsTabViewModel Quests { get; } = new();
    public RecipesTabViewModel Recipes { get; } = new();
    public PowersTabViewModel Powers { get; } = new();
    public CollectionsTabViewModel Collections { get; } = new();
    public RelationshipsTabViewModel Relationships { get; } = new();
    public StorageTabViewModel Storage { get; } = new();
    public FarmTabViewModel Farm { get; } = new();
    public MapTabViewModel Map { get; } = new();

    /// <summary>
    /// Binds every tab independently, catching per-tab so one tab's unexpected field shape
    /// (real save data keeps turning up variations this tool hasn't seen before - see
    /// StatsEditor/FarmEditor remarks for two found this way) can't block the whole save from
    /// opening. A tab that failed to bind just shows whatever it had before (usually empty) and
    /// gets named in the status message - everything else still works normally.
    /// </summary>
    public void Load(string path)
    {
        _save = SaveGameEditor.Load(path);
        _filePath = path;

        var failedTabs = new List<string>();

        void TryBind(string name, Action bind)
        {
            try
            {
                bind();
            }
            catch (Exception ex)
            {
                failedTabs.Add(name);
                System.Diagnostics.Debug.WriteLine($"{name} tab failed to bind: {ex}");
            }
        }

        TryBind("Player", () => Player.Bind(_save));
        TryBind("Inventory", () => Inventory.Bind(_save));
        TryBind("Stats", () => Stats.Bind(_save));
        TryBind("Achievements", () => Achievements.Bind(_save));
        TryBind("Quests", () => Quests.Bind(_save));
        TryBind("Recipes", () => Recipes.Bind(_save));
        TryBind("Powers", () => Powers.Bind(_save));
        TryBind("Collections", () => Collections.Bind(_save));
        TryBind("Relationships", () => Relationships.Bind(_save));
        TryBind("Storage", () => Storage.Bind(_save));
        TryBind("Farm", () => Farm.Bind(_save));
        TryBind("Map", () => Map.Bind(_save));

        IsSaveLoaded = true;
        StatusMessage = failedTabs.Count == 0
            ? $"Loaded {Path.GetFileName(path)}"
            : $"Loaded {Path.GetFileName(path)} - but these tabs hit an unexpected field shape and didn't load: {string.Join(", ", failedTabs)}. Everything else is fine to use.";

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
