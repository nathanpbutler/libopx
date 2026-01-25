# simpleRestriper Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a cross-platform Avalonia GUI app for bulk MXF timecode restriping that wraps libopx's FormatIO.Restripe functionality.

**Architecture:** Single-window MVVM app with drag-drop file list, timecode input with validation against file timebases, and async batch processing with per-file status feedback. Uses Avalonia.Themes.Simple for minimal styling.

**Tech Stack:** .NET 10, Avalonia 11.x, Avalonia.Themes.Simple, libopx (project reference)

---

## Task 1: Create Avalonia Project Structure

**Files:**
- Create: `apps/simpleRestriper/simpleRestriper.csproj`
- Create: `apps/simpleRestriper/Program.cs`
- Create: `apps/simpleRestriper/App.axaml`
- Create: `apps/simpleRestriper/App.axaml.cs`

**Step 1: Create the project file**

Create `apps/simpleRestriper/simpleRestriper.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../lib/libopx.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.2.*" />
    <PackageReference Include="Avalonia.Desktop" Version="11.2.*" />
    <PackageReference Include="Avalonia.Themes.Simple" Version="11.2.*" />
    <PackageReference Include="Avalonia.Diagnostics" Version="11.2.*" Condition="'$(Configuration)' == 'Debug'" />
  </ItemGroup>

</Project>
```

**Step 2: Create app.manifest**

Create `apps/simpleRestriper/app.manifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="simpleRestriper"/>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{e2011457-1546-43c5-a5fe-008deee3d3f0}" />
      <supportedOS Id="{35138b9a-5d96-4fbd-8e2d-a2440225f93a}" />
      <supportedOS Id="{4a2f28e3-53b9-4441-ba9c-d69d4a4a6e38}" />
      <supportedOS Id="{1f676c76-80e1-4239-95bb-83d0f6d0da78}" />
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>
</assembly>
```

**Step 3: Create Program.cs**

Create `apps/simpleRestriper/Program.cs`:

```csharp
using Avalonia;

namespace simpleRestriper;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
```

**Step 4: Create App.axaml**

Create `apps/simpleRestriper/App.axaml`:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="simpleRestriper.App"
             RequestedThemeVariant="Light">
    <Application.Styles>
        <SimpleTheme />
    </Application.Styles>
</Application>
```

**Step 5: Create App.axaml.cs**

Create `apps/simpleRestriper/App.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using simpleRestriper.ViewModels;
using simpleRestriper.Views;

namespace simpleRestriper;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
```

**Step 6: Verify project builds**

Run: `dotnet build apps/simpleRestriper/simpleRestriper.csproj`
Expected: Build succeeds (will have warnings about missing Views/ViewModels until next tasks)

**Step 7: Commit**

```bash
git add apps/simpleRestriper/
git commit -m "feat(simpleRestriper): scaffold Avalonia project structure"
```

---

## Task 2: Create MxfFileInfo Model

**Files:**
- Create: `apps/simpleRestriper/Models/MxfFileInfo.cs`

**Step 1: Create the model class**

Create `apps/simpleRestriper/Models/MxfFileInfo.cs`:

```csharp
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

    public required string FilePath { get; init; }
    public string FileName => Path.GetFileName(FilePath);

    public Timecode? TimecodeComponent
    {
        get => _timecodeComponent;
        set => SetField(ref _timecodeComponent, value);
    }

    public Timecode? SmpteTimecode
    {
        get => _smpteTimecode;
        set => SetField(ref _smpteTimecode, value);
    }

    public int Timebase { get; set; }

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
```

**Step 2: Verify build**

Run: `dotnet build apps/simpleRestriper/simpleRestriper.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add apps/simpleRestriper/Models/
git commit -m "feat(simpleRestriper): add MxfFileInfo model"
```

---

## Task 3: Create RestripeService

**Files:**
- Create: `apps/simpleRestriper/Services/RestripeService.cs`

**Step 1: Create the service class**

Create `apps/simpleRestriper/Services/RestripeService.cs`:

```csharp
using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Enums;
using simpleRestriper.Models;

namespace simpleRestriper.Services;

public class RestripeService
{
    /// <summary>
    /// Loads MXF file metadata including TimecodeComponent and first SMPTE timecode.
    /// </summary>
    public async Task<MxfFileInfo> LoadFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var fileInfo = new MxfFileInfo { FilePath = filePath };

            try
            {
                using var io = FormatIO.Open(filePath);

                // Get TimecodeComponent from MXF metadata via first packet's timecode
                // FormatIO.Open automatically reads the TimecodeComponent for MXF files
                var firstPacket = io.ParsePackets().FirstOrDefault();

                if (firstPacket?.Timecode != null)
                {
                    fileInfo.Timebase = firstPacket.Timecode.Timebase;
                    fileInfo.SmpteTimecode = firstPacket.Timecode;

                    // TimecodeComponent is read during FormatIO.Open for MXF files
                    // and stored in ParseOptions.StartTimecode - we need to read it separately
                    fileInfo.TimecodeComponent = ReadTimecodeComponent(filePath);
                }
            }
            catch (Exception ex)
            {
                fileInfo.Status = RestripeStatus.Error;
                fileInfo.ErrorMessage = ex.Message;
            }

            return fileInfo;
        }, cancellationToken);
    }

    /// <summary>
    /// Reads the TimecodeComponent metadata from an MXF file.
    /// </summary>
    private static Timecode? ReadTimecodeComponent(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var keyBuffer = new byte[16]; // KLV key size

            // Search for TimecodeComponent within first 128KB
            while (stream.Position < 128000)
            {
                var keyBytesRead = stream.Read(keyBuffer, 0, 16);
                if (keyBytesRead != 16) break;

                var keyType = Keys.GetKeyType(keyBuffer.AsSpan(0, 16));
                var length = ReadBerLength(stream);
                if (length < 0) break;

                if (keyType == KeyType.TimecodeComponent)
                {
                    var data = new byte[length];
                    var dataBytesRead = stream.Read(data, 0, length);
                    if (dataBytesRead != length) break;

                    var tc = TimecodeComponent.Parse(data);
                    return new Timecode(
                        tc.StartTimecode,
                        tc.RoundedTimecodeTimebase,
                        tc.DropFrame);
                }
                else
                {
                    stream.Seek(length, SeekOrigin.Current);
                }
            }
        }
        catch
        {
            // Ignore errors reading TimecodeComponent
        }

        return null;
    }

    private static int ReadBerLength(Stream input)
    {
        var firstByte = input.ReadByte();
        if (firstByte < 0) return -1;

        if ((firstByte & 0x80) == 0)
            return firstByte;

        var numLengthBytes = firstByte & 0x7F;
        if (numLengthBytes > 4 || numLengthBytes == 0) return -1;

        var lengthBytes = new byte[numLengthBytes];
        var bytesRead = input.Read(lengthBytes, 0, numLengthBytes);
        if (bytesRead != numLengthBytes) return -1;

        int length = 0;
        for (int i = 0; i < numLengthBytes; i++)
        {
            length = (length << 8) | lengthBytes[i];
        }

        return length;
    }

    /// <summary>
    /// Restripes an MXF file with the new timecode.
    /// </summary>
    public async Task RestripeAsync(MxfFileInfo file, string newTimecode, CancellationToken cancellationToken = default)
    {
        file.Status = RestripeStatus.Processing;
        file.ErrorMessage = null;

        try
        {
            using var io = FormatIO.Open(file.FilePath)
                .WithProgress(false)
                .WithVerbose(false);

            await io.RestripeAsync(newTimecode, cancellationToken);

            file.Status = RestripeStatus.Success;

            // Re-read the file to update displayed timecodes
            var updated = await LoadFileAsync(file.FilePath, cancellationToken);
            file.TimecodeComponent = updated.TimecodeComponent;
            file.SmpteTimecode = updated.SmpteTimecode;
        }
        catch (OperationCanceledException)
        {
            file.Status = RestripeStatus.Pending;
            throw;
        }
        catch (Exception ex)
        {
            file.Status = RestripeStatus.Error;
            file.ErrorMessage = ex.Message;
        }
    }
}
```

**Step 2: Verify build**

Run: `dotnet build apps/simpleRestriper/simpleRestriper.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add apps/simpleRestriper/Services/
git commit -m "feat(simpleRestriper): add RestripeService for MXF operations"
```

---

## Task 4: Create MainViewModel

**Files:**
- Create: `apps/simpleRestriper/ViewModels/MainViewModel.cs`

**Step 1: Create the view model**

Create `apps/simpleRestriper/ViewModels/MainViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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

    public int MinTimebase => Files.Count > 0
        ? Files.Where(f => f.Timebase > 0).Select(f => f.Timebase).DefaultIfEmpty(25).Min()
        : 25;

    public async Task AddFilesAsync(IEnumerable<string> filePaths)
    {
        foreach (var path in filePaths)
        {
            // Skip duplicates
            if (Files.Any(f => f.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Skip non-MXF files
            if (!path.EndsWith(".mxf", StringComparison.OrdinalIgnoreCase))
                continue;

            var fileInfo = await _restripeService.LoadFileAsync(path);
            Files.Add(fileInfo);
        }

        UpdateValidationHint();
        OnPropertyChanged(nameof(CanRestripe));
        OnPropertyChanged(nameof(MinTimebase));
        UpdateStatusText();
    }

    public void ClearFiles()
    {
        Files.Clear();
        UpdateValidationHint();
        OnPropertyChanged(nameof(CanRestripe));
        OnPropertyChanged(nameof(MinTimebase));
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

                await _restripeService.RestripeAsync(file, timecode, cancellationToken);

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
```

**Step 2: Verify build**

Run: `dotnet build apps/simpleRestriper/simpleRestriper.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add apps/simpleRestriper/ViewModels/
git commit -m "feat(simpleRestriper): add MainViewModel with file management and validation"
```

---

## Task 5: Create MainWindow View

**Files:**
- Create: `apps/simpleRestriper/Views/MainWindow.axaml`
- Create: `apps/simpleRestriper/Views/MainWindow.axaml.cs`

**Step 1: Create the XAML view**

Create `apps/simpleRestriper/Views/MainWindow.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:simpleRestriper.ViewModels"
        xmlns:models="using:simpleRestriper.Models"
        x:Class="simpleRestriper.Views.MainWindow"
        x:DataType="vm:MainViewModel"
        Title="simpleRestriper"
        Width="600" Height="450"
        MinWidth="500" MinHeight="350">

    <DockPanel Margin="12">
        <!-- Bottom controls -->
        <StackPanel DockPanel.Dock="Bottom" Spacing="8">
            <!-- Restripe button and status -->
            <DockPanel>
                <TextBlock DockPanel.Dock="Right"
                           Text="{Binding StatusText}"
                           VerticalAlignment="Center"
                           Margin="8,0,0,0"/>
                <Button Content="{Binding Files.Count, StringFormat='Restripe {0} files'}"
                        Command="{Binding RestripeCommand}"
                        IsEnabled="{Binding CanRestripe}"
                        HorizontalAlignment="Left"
                        Padding="16,8"/>
            </DockPanel>

            <!-- Timecode input -->
            <DockPanel>
                <CheckBox DockPanel.Dock="Right"
                          Content="Zero"
                          IsChecked="{Binding IsZeroChecked}"
                          IsEnabled="{Binding IsNotProcessing}"
                          VerticalAlignment="Center"
                          Margin="8,0,0,0"/>
                <StackPanel>
                    <DockPanel>
                        <TextBlock Text="New Timecode: "
                                   VerticalAlignment="Center"
                                   Width="100"/>
                        <TextBox Text="{Binding TimecodeInput}"
                                 IsEnabled="{Binding !IsZeroChecked}"
                                 Watermark="HH:MM:SS:FF"
                                 Width="120"/>
                    </DockPanel>
                    <TextBlock Text="{Binding ValidationHint}"
                               FontSize="11"
                               Foreground="Gray"
                               Margin="100,2,0,0"/>
                </StackPanel>
            </DockPanel>

            <!-- Browse and Clear buttons -->
            <StackPanel Orientation="Horizontal" Spacing="8">
                <Button Content="Browse..."
                        Click="OnBrowseClick"
                        IsEnabled="{Binding IsNotProcessing}"
                        Padding="16,8"/>
                <Button Content="Clear List"
                        Command="{Binding ClearCommand}"
                        IsEnabled="{Binding IsNotProcessing}"
                        Padding="16,8"/>
            </StackPanel>
        </StackPanel>

        <!-- File list -->
        <Border BorderBrush="Gray"
                BorderThickness="1"
                Margin="0,0,0,12"
                DragDrop.AllowDrop="True">
            <ListBox ItemsSource="{Binding Files}"
                     Background="White"
                     x:Name="FileListBox">
                <ListBox.ItemTemplate>
                    <DataTemplate DataType="models:MxfFileInfo">
                        <DockPanel Margin="4">
                            <!-- Status indicator -->
                            <TextBlock DockPanel.Dock="Right"
                                       VerticalAlignment="Center"
                                       Margin="8,0,0,0"
                                       FontSize="16">
                                <TextBlock.Text>
                                    <MultiBinding StringFormat="{}{0}">
                                        <Binding Path="Status"/>
                                    </MultiBinding>
                                </TextBlock.Text>
                            </TextBlock>

                            <StackPanel>
                                <TextBlock Text="{Binding FileName}"
                                           FontWeight="SemiBold"/>
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock Text="TC: "/>
                                    <TextBlock Text="{Binding TimecodeComponent, TargetNullValue='--:--:--:--'}"/>
                                    <TextBlock Text=" | SMPTE: "/>
                                    <TextBlock Text="{Binding SmpteTimecode, TargetNullValue='--:--:--:--'}"/>
                                    <TextBlock Text=" @ "/>
                                    <TextBlock Text="{Binding Timebase}"/>
                                    <TextBlock Text="fps"/>
                                    <TextBlock Text=" ⚠"
                                               Foreground="Orange"
                                               IsVisible="{Binding HasMismatch}"
                                               ToolTip.Tip="TimecodeComponent and SMPTE timecode do not match"/>
                                </StackPanel>
                                <TextBlock Text="{Binding ErrorMessage}"
                                           Foreground="Red"
                                           FontSize="11"
                                           IsVisible="{Binding ErrorMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
                            </StackPanel>
                        </DockPanel>
                    </DataTemplate>
                </ListBox.ItemTemplate>
            </ListBox>
        </Border>
    </DockPanel>
</Window>
```

**Step 2: Create the code-behind**

Create `apps/simpleRestriper/Views/MainWindow.axaml.cs`:

```csharp
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
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
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
```

**Step 3: Add Commands to ViewModel**

Update `apps/simpleRestriper/ViewModels/MainViewModel.cs` to add command properties after the constructor area:

Add these properties to the class:

```csharp
    // Add after the existing properties, before AddFilesAsync method
    public ICommand ClearCommand => new RelayCommand(ClearFiles, () => !IsProcessing);
    public ICommand RestripeCommand => new AsyncRelayCommand(RestripeAllAsync, () => CanRestripe);
```

And add a simple RelayCommand implementation at the end of the file (before the closing namespace brace):

```csharp
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
```

Also add `using System.Windows.Input;` at the top of the file.

**Step 4: Verify build**

Run: `dotnet build apps/simpleRestriper/simpleRestriper.csproj`
Expected: Build succeeds

**Step 5: Run the application**

Run: `dotnet run --project apps/simpleRestriper/simpleRestriper.csproj`
Expected: Window opens with Simple theme, shows file list area, Browse button, timecode input, and Restripe button

**Step 6: Commit**

```bash
git add apps/simpleRestriper/Views/
git add apps/simpleRestriper/ViewModels/MainViewModel.cs
git commit -m "feat(simpleRestriper): add MainWindow view with drag-drop and file picker"
```

---

## Task 6: Add Status Indicator Styling

**Files:**
- Modify: `apps/simpleRestriper/Views/MainWindow.axaml`
- Create: `apps/simpleRestriper/Converters/StatusToSymbolConverter.cs`

**Step 1: Create the converter**

Create `apps/simpleRestriper/Converters/StatusToSymbolConverter.cs`:

```csharp
using System.Globalization;
using Avalonia.Data.Converters;
using simpleRestriper.Models;

namespace simpleRestriper.Converters;

public class StatusToSymbolConverter : IValueConverter
{
    public static readonly StatusToSymbolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RestripeStatus status)
        {
            return status switch
            {
                RestripeStatus.Pending => "",
                RestripeStatus.Processing => "...",
                RestripeStatus.Success => "✓",
                RestripeStatus.Error => "✗",
                _ => ""
            };
        }
        return "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusToColorConverter : IValueConverter
{
    public static readonly StatusToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RestripeStatus status)
        {
            return status switch
            {
                RestripeStatus.Success => Avalonia.Media.Brushes.Green,
                RestripeStatus.Error => Avalonia.Media.Brushes.Red,
                RestripeStatus.Processing => Avalonia.Media.Brushes.Blue,
                _ => Avalonia.Media.Brushes.Gray
            };
        }
        return Avalonia.Media.Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
```

**Step 2: Update MainWindow.axaml to use converters**

Update the Window resources and status display in `apps/simpleRestriper/Views/MainWindow.axaml`:

Add after the opening `<Window>` tag:

```xml
    <Window.Resources>
        <converters:StatusToSymbolConverter x:Key="StatusToSymbol"/>
        <converters:StatusToColorConverter x:Key="StatusToColor"/>
    </Window.Resources>
```

Add the namespace at the top:

```xml
        xmlns:converters="using:simpleRestriper.Converters"
```

Update the status TextBlock in the DataTemplate to:

```xml
                            <!-- Status indicator -->
                            <TextBlock DockPanel.Dock="Right"
                                       VerticalAlignment="Center"
                                       Margin="8,0,0,0"
                                       FontSize="16"
                                       FontWeight="Bold"
                                       Text="{Binding Status, Converter={StaticResource StatusToSymbol}}"
                                       Foreground="{Binding Status, Converter={StaticResource StatusToColor}}"/>
```

**Step 3: Verify build and run**

Run: `dotnet run --project apps/simpleRestriper/simpleRestriper.csproj`
Expected: Status indicators show correctly styled symbols

**Step 4: Commit**

```bash
git add apps/simpleRestriper/Converters/
git add apps/simpleRestriper/Views/MainWindow.axaml
git commit -m "feat(simpleRestriper): add status indicator styling with converters"
```

---

## Task 7: Add Empty State and Polish

**Files:**
- Modify: `apps/simpleRestriper/Views/MainWindow.axaml`

**Step 1: Add empty state overlay**

Update the file list section in `apps/simpleRestriper/Views/MainWindow.axaml` to show a hint when empty:

Replace the file list Border with:

```xml
        <!-- File list -->
        <Border BorderBrush="Gray"
                BorderThickness="1"
                Margin="0,0,0,12"
                DragDrop.AllowDrop="True">
            <Grid>
                <!-- Empty state hint -->
                <TextBlock Text="Drag MXF files here or click Browse"
                           HorizontalAlignment="Center"
                           VerticalAlignment="Center"
                           Foreground="Gray"
                           IsVisible="{Binding !Files.Count}"/>

                <!-- File list -->
                <ListBox ItemsSource="{Binding Files}"
                         Background="Transparent"
                         x:Name="FileListBox"
                         IsVisible="{Binding Files.Count}">
                    <ListBox.ItemTemplate>
                        <DataTemplate DataType="models:MxfFileInfo">
                            <DockPanel Margin="4">
                                <!-- Status indicator -->
                                <TextBlock DockPanel.Dock="Right"
                                           VerticalAlignment="Center"
                                           Margin="8,0,0,0"
                                           FontSize="16"
                                           FontWeight="Bold"
                                           Text="{Binding Status, Converter={StaticResource StatusToSymbol}}"
                                           Foreground="{Binding Status, Converter={StaticResource StatusToColor}}"/>

                                <StackPanel>
                                    <TextBlock Text="{Binding FileName}"
                                               FontWeight="SemiBold"/>
                                    <StackPanel Orientation="Horizontal">
                                        <TextBlock Text="TC: "/>
                                        <TextBlock Text="{Binding TimecodeComponent, TargetNullValue='--:--:--:--'}"/>
                                        <TextBlock Text=" | SMPTE: "/>
                                        <TextBlock Text="{Binding SmpteTimecode, TargetNullValue='--:--:--:--'}"/>
                                        <TextBlock Text=" @ "/>
                                        <TextBlock Text="{Binding Timebase}"/>
                                        <TextBlock Text="fps"/>
                                        <TextBlock Text=" ⚠"
                                                   Foreground="Orange"
                                                   IsVisible="{Binding HasMismatch}"
                                                   ToolTip.Tip="TimecodeComponent and SMPTE timecode do not match"/>
                                    </StackPanel>
                                    <TextBlock Text="{Binding ErrorMessage}"
                                               Foreground="Red"
                                               FontSize="11"
                                               IsVisible="{Binding ErrorMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
                                </StackPanel>
                            </DockPanel>
                        </DataTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>
            </Grid>
        </Border>
```

**Step 2: Verify build and run**

Run: `dotnet run --project apps/simpleRestriper/simpleRestriper.csproj`
Expected: Empty state shows hint text, list appears when files are added

**Step 3: Commit**

```bash
git add apps/simpleRestriper/Views/MainWindow.axaml
git commit -m "feat(simpleRestriper): add empty state hint for file list"
```

---

## Task 8: Integration Testing

**Files:**
- No new files

**Step 1: Manual test with real MXF file**

1. Run: `dotnet run --project apps/simpleRestriper/simpleRestriper.csproj`
2. Drag an MXF file into the window
3. Verify TC and SMPTE timecodes are displayed
4. Verify timebase is shown
5. Enter a valid timecode (e.g., "10:00:00:00")
6. Click Restripe
7. Verify file shows success status
8. Verify timecodes are updated

**Step 2: Test edge cases**

1. Test Zero checkbox functionality
2. Test invalid timecode validation (e.g., frames > timebase)
3. Test drag-drop of non-MXF files (should be ignored)
4. Test duplicate file handling

**Step 3: Final commit**

```bash
git add -A
git commit -m "feat(simpleRestriper): complete initial implementation

Cross-platform Avalonia GUI for bulk MXF timecode restriping:
- Drag-drop and file picker support
- Shows TC, SMPTE timecode, and timebase per file
- Validates timecode against lowest file timebase
- Zero checkbox for quick reset to 00:00:00:00
- Async batch processing with per-file status
- Continue-on-error with summary display"
```

---

## Summary

The implementation is broken into 8 tasks:

1. **Project Structure** - Create Avalonia project with Simple theme
2. **Model** - Create MxfFileInfo data model
3. **Service** - Create RestripeService wrapping FormatIO
4. **ViewModel** - Create MainViewModel with file management and validation
5. **View** - Create MainWindow with drag-drop and controls
6. **Converters** - Add status indicator styling
7. **Polish** - Add empty state and UI refinements
8. **Testing** - Integration testing with real files

Each task includes specific files, complete code, and commit points.
