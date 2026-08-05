namespace StardewTools.SaveEditor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public SaveEditorViewModel SaveEditor { get; } = new();
    public TrainerViewModel Trainer { get; } = new();

    public MainWindowViewModel()
    {
        Trainer.Start();
    }
}
