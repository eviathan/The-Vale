using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private readonly UndoManager _undo;

    [ObservableProperty] private string _statusMessage = "Open a Stardew Valley save file to begin.";
    [ObservableProperty] private bool _isSaveLoaded;
    [ObservableProperty] private bool _canUndo;
    [ObservableProperty] private bool _canRedo;

    public SaveEditorViewModel()
    {
        _undo = new UndoManager(OnUndoRedoRestore);
        _undo.StackChanged += () =>
        {
            CanUndo = _undo.CanUndo;
            CanRedo = _undo.CanRedo;
        };
    }

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

        var failedTabs = BindAll(_save);
        _undo.Attach(_save);

        IsSaveLoaded = true;
        StatusMessage = failedTabs.Count == 0
            ? $"Loaded {Path.GetFileName(path)}"
            : $"Loaded {Path.GetFileName(path)} - but these tabs hit an unexpected field shape and didn't load: {string.Join(", ", failedTabs)}. Everything else is fine to use.";

        var settings = AppSettings.Load();
        settings.LastSaveFilePath = path;
        settings.Save();
    }

    /// <summary>Binds every tab independently, catching per-tab so one tab's unexpected field
    /// shape can't block the whole save from opening (see class remarks) - shared by the initial
    /// Load() and by undo/redo's restore step (OnUndoRedoRestore), both of which need every tab
    /// re-pointed at a new SaveGameEditor instance. Returns the names of any tabs that failed.</summary>
    private List<string> BindAll(SaveGameEditor save)
    {
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

        TryBind("Player", () => Player.Bind(save));
        TryBind("Inventory", () => Inventory.Bind(save));
        TryBind("Stats", () => Stats.Bind(save));
        TryBind("Achievements", () => Achievements.Bind(save));
        TryBind("Quests", () => Quests.Bind(save));
        TryBind("Recipes", () => Recipes.Bind(save));
        TryBind("Powers", () => Powers.Bind(save));
        TryBind("Collections", () => Collections.Bind(save));
        TryBind("Relationships", () => Relationships.Bind(save));
        TryBind("Storage", () => Storage.Bind(save));
        TryBind("Farm", () => Farm.Bind(save));
        TryBind("Map", () => Map.Bind(save));

        return failedTabs;
    }

    /// <summary>UndoManager's restore callback - swaps in the restored SaveGameEditor and rebinds
    /// every tab against it, exactly like a fresh Load() (see BindAll remarks). Each tab's Bind()
    /// already resets its own transient selection state, so stale references to XElements from
    /// before the undo/redo (which no longer exist in the restored tree) are naturally discarded
    /// without any undo-specific cleanup code.</summary>
    private void OnUndoRedoRestore(SaveGameEditor restored)
    {
        _save = restored;
        var failedTabs = BindAll(restored);
        StatusMessage = failedTabs.Count == 0
            ? StatusMessage
            : $"Undo/redo hit an unexpected field shape on: {string.Join(", ", failedTabs)}.";
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => _undo.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => _undo.Redo();

    partial void OnCanUndoChanged(bool value) => UndoCommand.NotifyCanExecuteChanged();
    partial void OnCanRedoChanged(bool value) => RedoCommand.NotifyCanExecuteChanged();

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
