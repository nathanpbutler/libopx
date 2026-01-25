using System.ComponentModel;
using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx;

namespace simpleRestriper.Models;

public enum RestripeStatus
{
    Pending,
    Processing,
    Success,
    Error
}

public class MxfFileInfo : INotifyPropertyChanged
{
    private RestripeStatus _status = RestripeStatus.Pending;
    private string? _errorMessage;
    private Timecode? _timecodeComponent;
    private Timecode? _smpteTimecode;
    private int _timebase;

    public required string FilePath { get; init; }
    public string FileName => Path.GetFileName(FilePath);

    public Timecode? TimecodeComponent
    {
        get => _timecodeComponent;
        set
        {
            if (SetField(ref _timecodeComponent, value))
                OnPropertyChanged(nameof(HasMismatch));
        }
    }

    public Timecode? SmpteTimecode
    {
        get => _smpteTimecode;
        set
        {
            if (SetField(ref _smpteTimecode, value))
                OnPropertyChanged(nameof(HasMismatch));
        }
    }

    public int Timebase
    {
        get => _timebase;
        set => SetField(ref _timebase, value);
    }

    public bool HasMismatch => TimecodeComponent != null &&
                               SmpteTimecode != null &&
                               !TimecodeComponent.Equals(SmpteTimecode);

    public RestripeStatus Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }

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
