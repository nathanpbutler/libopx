using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using simpleRestriper.Models;
using simpleRestriper.Services;

namespace simpleRestriper.ViewModels;

public partial class MainViewModel : INotifyPropertyChanged
{
    private readonly RestripeService _restripeService = new();
    private string _timecodeInput = string.Empty;
    private bool _isZeroChecked;
    private bool _isProcessing;
    private string _statusText = string.Empty;
    private string _validationHint = string.Empty;
    private bool _isTimecodeValid;
    private RelayCommand? _clearCommand;
    private AsyncRelayCommand? _restripeCommand;

    public ObservableCollection<MxfFileInfo> Files { get; } = [];

    public string TimecodeInput
    {
        get => _timecodeInput;
        set
        {
            if (SetField(ref _timecodeInput, value))
            {
                ValidateTimecode();
                OnPropertyChanged(nameof(CanRestripe));
                RaiseCommandsCanExecuteChanged();
            }
        }
    }

    public bool IsZeroChecked
    {
        get => _isZeroChecked;
        set
        {
            if (SetField(ref _isZeroChecked, value))
            {
                if (value) TimecodeInput = "00:00:00:00";
                OnPropertyChanged(nameof(CanRestripe));
                RaiseCommandsCanExecuteChanged();
            }
        }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            if (SetField(ref _isProcessing, value))
            {
                OnPropertyChanged(nameof(CanRestripe));
                OnPropertyChanged(nameof(IsNotProcessing));
                RaiseCommandsCanExecuteChanged();
            }
        }
    }

    public bool IsNotProcessing => !IsProcessing;

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public string ValidationHint
    {
        get => _validationHint;
        set => SetField(ref _validationHint, value);
    }

    public bool CanRestripe => !IsProcessing &&
                               Files.Count > 0 &&
                               (IsZeroChecked || _isTimecodeValid);

    private void RaiseCommandsCanExecuteChanged()
    {
        _restripeCommand?.RaiseCanExecuteChanged();
        _clearCommand?.RaiseCanExecuteChanged();
    }

    public int MinTimebase => Files.Count > 0
        ? Files.Where(f => f.Timebase > 0).Select(f => f.Timebase).DefaultIfEmpty(25).Min()
        : 25;

    // Commands (cached to avoid recreating on every access)
    public ICommand ClearCommand => _clearCommand ??= new RelayCommand(ClearFiles, () => !IsProcessing);
    public ICommand RestripeCommand => _restripeCommand ??= new AsyncRelayCommand(() => RestripeAllAsync(), () => CanRestripe);

    public async Task AddFilesAsync(IEnumerable<string> filePaths)
    {
        var newMxfFiles = filePaths
            .Where(path => path.EndsWith(".mxf", StringComparison.OrdinalIgnoreCase))
            .Where(path => !Files.Any(f => f.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase)));

        foreach (var path in newMxfFiles)
        {
            var fileInfo = await _restripeService.LoadFileAsync(path);
            Files.Add(fileInfo);
        }

        UpdateValidationHint();
        ValidateTimecode(); // Re-validate since MinTimebase may have changed
        OnPropertyChanged(nameof(CanRestripe));
        OnPropertyChanged(nameof(MinTimebase));
        RaiseCommandsCanExecuteChanged();
        UpdateStatusText();
    }

    public void ClearFiles()
    {
        Files.Clear();
        UpdateValidationHint();
        ValidateTimecode(); // Re-validate since MinTimebase may have changed
        OnPropertyChanged(nameof(CanRestripe));
        OnPropertyChanged(nameof(MinTimebase));
        RaiseCommandsCanExecuteChanged();
        UpdateStatusText();
    }

    public async Task RestripeAllAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRestripe) return;

        IsProcessing = true;
        var timecode = IsZeroChecked ? "00:00:00:00" : TimecodeInput;
        var total = Files.Count;
        var succeeded = 0;
        var failed = 0;

        try
        {
            for (int i = 0; i < Files.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var file = Files[i];
                StatusText = $"Processing {i + 1} of {total}...";

                var progress = new Progress<double>(p => file.Progress = p);
                await _restripeService.RestripeAsync(file, timecode, progress, cancellationToken);

                if (file.Status == RestripeStatus.Success)
                    succeeded++;
                else if (file.Status == RestripeStatus.Error)
                    failed++;
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled";
            return;
        }
        finally
        {
            IsProcessing = false;
        }

        StatusText = failed > 0
            ? $"{succeeded} succeeded, {failed} failed"
            : $"{succeeded} succeeded";
    }

    private void ValidateTimecode()
    {
        if (string.IsNullOrWhiteSpace(TimecodeInput))
        {
            _isTimecodeValid = false;
            return;
        }

        // Match HH:MM:SS:FF format
        var match = TimecodeRegex().Match(TimecodeInput);
        if (!match.Success)
        {
            _isTimecodeValid = false;
            return;
        }

        var frames = int.Parse(match.Groups[4].Value);
        var maxFrames = MinTimebase - 1;

        _isTimecodeValid = frames <= maxFrames;
    }

    private void UpdateValidationHint()
    {
        if (Files.Count == 0)
        {
            ValidationHint = string.Empty;
            return;
        }

        var minTimebase = MinTimebase;
        ValidationHint = $"(valid: 0-{minTimebase - 1} for {minTimebase}fps files)";
    }

    private void UpdateStatusText()
    {
        if (Files.Count == 0)
        {
            StatusText = string.Empty;
            return;
        }

        var ready = Files.Count(f => f.Status == RestripeStatus.Pending);
        StatusText = $"{ready} file{(ready == 1 ? "" : "s")} ready";
    }

    [GeneratedRegex(@"^(\d{2}):(\d{2}):(\d{2}):(\d{2})$")]
    private static partial Regex TimecodeRegex();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public class AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public async void Execute(object? parameter) => await execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
