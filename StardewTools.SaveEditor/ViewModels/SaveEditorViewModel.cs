using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StardewTools.Core.Models;
using StardewTools.SaveEditor;

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

    /// <summary>Drives the main TabControl's SelectedIndex (MainWindow.axaml) - unbound by
    /// default (no VM-side tab switching existed before #6c's "select a chest on the map, jump to
    /// its contents on Storage" navigation needed one). Index is positional, not name-keyed - see
    /// StorageTabIndex remarks for why that coupling is called out explicitly rather than hidden.</summary>
    [ObservableProperty] private int _selectedTabIndex;

    /// <summary>The Storage tab's position among the main TabControl's direct TabItem children
    /// (MainWindow.axaml) - Player=0, Inventory=1, Stats=2, Achievements=3, Recipes=4,
    /// Collections=5, Powers=6, Quests=7, Relationships=8, Storage=9, Farm=10, Map=11. There's no
    /// name-based tab lookup in this app (a plain Avalonia TabControl, no x:Name'd TabItems), so
    /// this index must be kept in sync by hand if a tab is ever added/removed/reordered - flagged
    /// here specifically because it's the one place outside the XAML itself that depends on it.</summary>
    private const int StorageTabIndex = 9;

    public SaveEditorViewModel()
    {
        _undo = new UndoManager(OnUndoRedoRestore);
        _undo.StackChanged += () =>
        {
            CanUndo = _undo.CanUndo;
            CanRedo = _undo.CanRedo;
        };

        // CanApplyPreset reads IsSelected off a different ViewModel (PresetSectionOptionViewModel)
        // than the one the [RelayCommand] lives on, so the source generator's own
        // NotifyCanExecuteChangedFor wiring can't see it - wire it by hand instead.
        foreach (var option in PresetSectionOptions)
            option.PropertyChanged += (_, _) => ApplyPresetCommand.NotifyCanExecuteChanged();

        // "Regenerate Farm" lives entirely on the Farm tab (confirmation UI stays within its own
        // DataContext scope, no cross-tab XAML binding needed) but the actual wipe touches the
        // Map tab's state (Entities, rendering) - this callback is the one place those two meet.
        Farm.RegenerateConfirmed += save => Map.RegenerateFarmContent(save);
        Farm.GreenhouseUnlockedChanged += value => Map.GreenhouseUnlocked = value;

        // #6c: a chest selected on the Map tab can jump straight to its contents here, instead of
        // scrolling through the Storage tab's full chest list to find the right one.
        Map.GoToChestInStorage += chest =>
        {
            if (Storage.SelectChest(chest))
                SelectedTabIndex = StorageTabIndex;
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

    /// <summary>Cross-save recovery ("apply selected sections from another save onto this one" -
    /// see PresetSections). A "preset" is deliberately just an ordinary save file - this writes
    /// the CURRENT live state to a new path without touching _filePath/the undo stack, so opening
    /// it later (via LoadPresetSource, or even the normal Open Save button) works exactly like
    /// opening any other save.</summary>
    public void SavePreset(string path) => _save?.Save(path);

    private SaveGameEditor? _presetSource;

    [ObservableProperty] private string? _presetSourcePath;
    [ObservableProperty] private string? _presetSourceSummary;
    [ObservableProperty] private bool _isPresetPanelOpen;

    public IReadOnlyList<PresetSectionOptionViewModel> PresetSectionOptions { get; } =
        Enum.GetValues<PresetSection>()
            .Select(section => new PresetSectionOptionViewModel(section, PresetSections.Descriptions[section]))
            .ToList();

    /// <summary>Loads any other save file (a preset saved via SavePreset, a raw backup, another
    /// character's save, even the broken save this whole feature exists to recover from) as a
    /// read-only source, completely independent from _save/the undo stack - nothing here is
    /// written until ApplyPresetCommand runs, and _presetSource itself is never bound to any tab.</summary>
    public void LoadPresetSource(string path)
    {
        _presetSource = SaveGameEditor.Load(path);
        PresetSourcePath = path;

        var farmType = GameEnums.FindOrFirst(GameEnums.FarmTypes, _presetSource.Farm.WhichFarm).Name;
        PresetSourceSummary = $"{_presetSource.Player.Name} - {farmType} farm, Year {_presetSource.Year} {_presetSource.Season} {_presetSource.DayOfMonth} (House level {_presetSource.Player.HouseUpgradeLevel})";

        foreach (var option in PresetSectionOptions)
            option.IsSelected = false;

        IsPresetPanelOpen = true;
    }

    public void CancelPreset()
    {
        _presetSource = null;
        PresetSourcePath = null;
        PresetSourceSummary = null;
        IsPresetPanelOpen = false;
    }

    private bool CanApplyPreset() => _presetSource is not null && _save is not null && PresetSectionOptions.Any(o => o.IsSelected);

    [RelayCommand(CanExecute = nameof(CanApplyPreset))]
    private void ApplyPreset()
    {
        if (_presetSource is null || _save is null)
            return;

        var applied = new List<string>();
        foreach (var option in PresetSectionOptions.Where(o => o.IsSelected))
        {
            PresetSections.Copy(option.Section, _presetSource, _save);
            applied.Add(option.Section.ToString());
        }

        BindAll(_save);
        StatusMessage = $"Applied from {Path.GetFileName(PresetSourcePath)}: {string.Join(", ", applied)}. Not saved to disk yet - use Save when you're happy with the result (or Undo/Ctrl+Z to back it out).";
        CancelPreset();
    }
}

/// <summary>One row in the Apply Preset panel's section checklist.</summary>
public sealed partial class PresetSectionOptionViewModel : ViewModelBase
{
    public PresetSection Section { get; }
    public string Description { get; }

    [ObservableProperty] private bool _isSelected;

    public PresetSectionOptionViewModel(PresetSection section, string description)
    {
        Section = section;
        Description = description;
    }
}
