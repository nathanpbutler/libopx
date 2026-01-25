# simpleRestriper Design

A cross-platform GUI utility for bulk MXF timecode restriping.

## Overview

**Purpose:** Provide a simple drag-and-drop interface for restriping multiple MXF files at once, wrapping libopx's existing restripe functionality.

**Location:** `/apps/simpleRestriper/` in the libopx repository, developed on branch `feature/simple-restriper`.

**Tech stack:**
- .NET 10
- Avalonia UI with Simple theme (`Avalonia.Themes.Simple`)
- MVVM architecture
- Direct integration with libopx via `FormatIO`

## Project Structure

```
apps/simpleRestriper/
├── simpleRestriper.csproj      # Avalonia app, references ../../lib/
├── App.axaml                   # Application entry with SimpleTheme
├── MainWindow.axaml            # Single-window UI
├── MainWindow.axaml.cs         # View code-behind
├── ViewModels/
│   └── MainViewModel.cs        # MVVM logic, file list, restripe orchestration
├── Models/
│   └── MxfFileInfo.cs          # Per-file data (path, TC, SMPTE, timebase, status)
└── Services/
    └── RestripeService.cs      # Wraps libopx FormatIO restripe calls
```

## User Interface

```
┌─────────────────────────────────────────────────────────────┐
│  simpleRestriper                                      [—][×]│
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  interview_01.mxf                                    │   │
│  │  TC: 01:23:45:12 | SMPTE: 01:23:45:12 @ 25fps    ✓   │   │
│  ├──────────────────────────────────────────────────────┤   │
│  │  interview_02.mxf                                    │   │
│  │  TC: 00:00:00:00 | SMPTE: 10:00:00:00 @ 25fps    ⚠   │   │
│  ├──────────────────────────────────────────────────────┤   │
│  │                                                      │   │
│  │         (drag files here or click Browse)            │   │
│  │                                                      │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌──────────────┐                                           │
│  │   Browse...  │   [Clear List]                            │
│  └──────────────┘                                           │
│                                                             │
│  New Timecode: [____________]  ☐ Zero                       │
│                (valid: 0-24 for 25fps files)                │
│                                                             │
│  ┌──────────────┐                                           │
│  │   Restripe   │   5 files ready                           │
│  └──────────────┘                                           │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**Key elements:**
- File list with drag-drop support and scrolling
- Each file shows: filename, TimecodeComponent (TC), first SMPTE timecode, and timebase
- TC/SMPTE mismatch shown with ⚠ indicator
- Timecode input validated against the lowest timebase in the file list
- "Zero" checkbox disables text field and uses `00:00:00:00`
- Status text updates during/after processing

## Data Model

```csharp
public class MxfFileInfo
{
    public string FilePath { get; set; }
    public string FileName => Path.GetFileName(FilePath);
    public Timecode? TimecodeComponent { get; set; }  // From MXF metadata
    public Timecode? SmpteTimecode { get; set; }      // First SMPTE TC in stream
    public int Timebase { get; set; }                 // e.g., 25, 30
    public bool HasMismatch => TimecodeComponent != SmpteTimecode;
    public RestripeStatus Status { get; set; }        // Pending, Processing, Success, Error
    public string? ErrorMessage { get; set; }
}

public enum RestripeStatus { Pending, Processing, Success, Error }
```

## File Loading Behavior

- On Browse or drag-drop, read each MXF to extract TimecodeComponent, first SMPTE timecode, and timebase using FormatIO
- Duplicate files ignored (check by full path)
- Invalid/non-MXF files show error status immediately
- Track lowest timebase across all files for frame validation
- Frame number validated on keystroke: `0 <= FF < minTimebase`

## Restripe Operation Flow

**Pre-flight:**
- "Restripe" button disabled until: at least one file loaded AND (valid timecode OR "Zero" checked)
- Button shows count: "Restripe 5 files"

**Processing:**
1. Disable all inputs
2. For each file:
   - Set status to `Processing`
   - Call FormatIO restripe with target timecode
   - On success: status = `Success` ✓
   - On error: status = `Error` ✗, capture message
   - Update progress: "Processing 3 of 5..."
3. Re-enable inputs
4. Show summary: "5 succeeded" or "4 succeeded, 1 failed"

**Error handling:**
- Continue processing remaining files on error
- Hover/click error row to see error message
- Files remain in list for retry or review

**Post-restripe:**
- Successful files show updated TC values (re-read from file)
- User can modify timecode and restripe again

## Project Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../lib/libopx.csproj" />
    <PackageReference Include="Avalonia" Version="11.*" />
    <PackageReference Include="Avalonia.Desktop" Version="11.*" />
    <PackageReference Include="Avalonia.Themes.Simple" Version="11.*" />
  </ItemGroup>
</Project>
```

## Platform Considerations

- Single executable via `PublishSingleFile` + `SelfContained` for distribution
- No platform-specific code - Avalonia handles Windows/macOS/Linux
- File dialogs use Avalonia's `StorageProvider` API
