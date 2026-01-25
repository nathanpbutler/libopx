using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using simpleRestriper.ViewModels;

namespace simpleRestriper.Views;

public partial class MainWindow : Window
{
    private bool _isFormattingTimecode;

    public MainWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnTimecodeTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isFormattingTimecode || sender is not TextBox textBox)
            return;

        _isFormattingTimecode = true;
        try
        {
            var text = textBox.Text ?? string.Empty;
            var caretIndex = textBox.CaretIndex;

            // Count digits before caret position
            var digitsBefore = text.Take(caretIndex).Count(char.IsDigit);

            // Format the text
            var formatted = FormatTimecode(text);

            if (formatted != text)
            {
                textBox.Text = formatted;

                // Calculate new caret position based on digits typed
                var newCaretIndex = 0;
                var digitsFound = 0;
                foreach (var c in formatted)
                {
                    if (digitsFound >= digitsBefore)
                        break;
                    newCaretIndex++;
                    if (char.IsDigit(c))
                        digitsFound++;
                }

                // Move past any colon that follows
                if (newCaretIndex < formatted.Length && formatted[newCaretIndex] == ':')
                    newCaretIndex++;

                textBox.CaretIndex = Math.Min(newCaretIndex, formatted.Length);
            }
        }
        finally
        {
            _isFormattingTimecode = false;
        }
    }

    private static string FormatTimecode(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Extract only digits
        var digits = new string(input.Where(char.IsDigit).ToArray());

        // Limit to 8 digits (HHMMSSFF)
        if (digits.Length > 8)
            digits = digits[..8];

        // Insert colons at appropriate positions
        return digits.Length switch
        {
            0 => string.Empty,
            1 or 2 => digits,
            3 or 4 => $"{digits[..2]}:{digits[2..]}",
            5 or 6 => $"{digits[..2]}:{digits[2..4]}:{digits[4..]}",
            _ => $"{digits[..2]}:{digits[2..4]}:{digits[4..6]}:{digits[6..]}"
        };
    }

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
