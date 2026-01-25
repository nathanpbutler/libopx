using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using simpleRestriper.ViewModels;

namespace simpleRestriper.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select MXF Files",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("MXF Files") { Patterns = ["*.mxf"] },
                new FilePickerFileType("All Files") { Patterns = ["*.*"] }
            ]
        });

        if (files.Count > 0 && ViewModel != null)
        {
            var paths = files.Select(f => f.Path.LocalPath);
            await ViewModel.AddFilesAsync(paths);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files)) return;
        if (ViewModel == null) return;

        var files = e.Data.GetFiles();
        if (files == null) return;

        var paths = files
            .Select(f => f.Path.LocalPath)
            .Where(p => p.EndsWith(".mxf", StringComparison.OrdinalIgnoreCase));

        await ViewModel.AddFilesAsync(paths);
    }
}
