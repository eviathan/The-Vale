using System;
using System.IO;

namespace StardewTools.SaveEditor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public SaveEditorViewModel SaveEditor { get; } = new();
    public TrainerViewModel Trainer { get; } = new();

    public MainWindowViewModel()
    {
        Trainer.Start();

        // Best-effort - a missing/moved/corrupt file from a previous session shouldn't block
        // startup, just leave the editor in its normal empty state.
        var lastPath = AppSettings.Load().LastSaveFilePath;
        if (!string.IsNullOrEmpty(lastPath) && File.Exists(lastPath))
        {
            try
            {
                SaveEditor.Load(lastPath);
            }
            catch (Exception ex)
            {
                SaveEditor.StatusMessage = $"Couldn't reopen last save ({Path.GetFileName(lastPath)}): {ex.Message}";
            }
        }
    }
}
