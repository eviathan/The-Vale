using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using StardewTools.SaveEditor.ViewModels;

namespace StardewTools.SaveEditor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private SaveEditorViewModel ViewModel => ((MainWindowViewModel)DataContext!).SaveEditor;

    private async void OnOpenClicked(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Open Stardew Valley save file",
            AllowMultiple = false,
        });

        var file = files.FirstOrDefault();
        if (file is null)
            return;

        try
        {
            ViewModel.Load(file.Path.LocalPath);
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Failed to open: {ex.Message}";
        }
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.Save();
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Failed to save: {ex.Message}";
        }
    }

    private async void OnBrowseMapFolderClicked(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Select the folder unpacked by StardewXnbHack (contains a 'Maps' subfolder)",
            AllowMultiple = false,
        });

        var folder = folders.FirstOrDefault();
        if (folder is not null)
            ViewModel.Map.ContentFolder = folder.Path.LocalPath;
    }
}
