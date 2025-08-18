# libopx Library

[![NuGet](https://img.shields.io/nuget/v/libopx?style=flat-square)](https://www.nuget.org/packages/libopx)
[![.NET](https://img.shields.io/badge/.NET-9-blue?style=flat-square)](https://dotnet.microsoft.com/download/dotnet/9.0)

A .NET 9 C# library for parsing and extracting data from MXF (Material Exchange Format), BIN (MXF caption data stream), VBI (Vertical Blanking Interval), and T42 (Teletext packet stream) files, with SMPTE timecode and Teletext caption support.

## Installation

### NuGet Package

```bash
dotnet add package libopx
```

### From Source

```bash
git clone https://github.com/nathanpbutler/libopx.git
cd libopx
dotnet build
```

## Quick Start

### Basic Parsing

```csharp
using nathanbutlerDEV.libopx.Formats;

// Parse VBI file with filtering
using var vbi = new VBI("input.vbi");
foreach (var line in vbi.Parse(magazine: 8, rows: new[] { 20, 22 }))
{
    Console.WriteLine(line);
}

// Parse T42 file
using var t42 = new T42("input.t42");
foreach (var line in t42.Parse(magazine: null))
{
    Console.WriteLine(line);
}

// Parse MXF file
using var mxf = new MXF("input.mxf");
foreach (var packet in mxf.Parse(magazine: null, rows: Constants.CAPTION_ROWS))
{
    foreach (var line in packet.Lines)
    {
        Console.WriteLine(line);
    }
}
```

### Format Conversion

```csharp
using nathanbutlerDEV.libopx.Formats;
using nathanbutlerDEV.libopx.Enums;

// Convert VBI to T42
using var parser = new VBI("input.vbi");
parser.OutputFormat = Format.T42;
parser.SetOutput("output.t42");

foreach (var line in parser.Parse(magazine: null, rows: Constants.DEFAULT_ROWS))
{
    parser.Output.Write(line.Data);
}

parser.Dispose(); // Ensure output is flushed
```

### Filtering Teletext Data

```csharp
using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Formats;

// Filter by magazine and specific rows
using var vbi = new VBI("input.vbi");
foreach (var line in vbi.Parse(magazine: 8, rows: new[] { 20, 22 }))
{
    Console.WriteLine(line);
}

// Use predefined caption rows
using var t42 = new T42("input.t42");
foreach (var line in t42.Parse(magazine: null, rows: Constants.CAPTION_ROWS))
{
    Console.WriteLine(line);
}
```

## API Reference

### Format Parsers

All format parsers implement a consistent interface for parsing and filtering:

#### VBI Parser

```csharp
using var vbi = new VBI("input.vbi");
// Returns IEnumerable<Line>
foreach (var line in vbi.Parse(magazine: null, rows: null))
{
    Console.WriteLine($"Magazine: {line.Magazine}, Row: {line.Row}, Data: {line.Data}");
}
```

#### T42 Parser

```csharp
using var t42 = new T42("input.t42");
// Returns IEnumerable<Line>
foreach (var line in t42.Parse(magazine: null, rows: null))
{
    Console.WriteLine($"Timecode: {line.Timecode}, Data: {line.Data}");
}
```

#### BIN Parser

```csharp
using var bin = new BIN("input.bin");
// Returns IEnumerable<Packet>
foreach (var packet in bin.Parse(magazine: null, rows: null))
{
    Console.WriteLine($"Timecode: {packet.Timecode}");
    foreach (var line in packet.Lines)
    {
        Console.WriteLine($"  Line: {line}");
    }
}
```

#### MXF Parser

```csharp
using var mxf = new MXF("input.mxf");
// Returns IEnumerable<Packet>
foreach (var packet in mxf.Parse(magazine: null, rows: null))
{
    Console.WriteLine($"Timecode: {packet.Timecode}");
    foreach (var line in packet.Lines.Where(l => l.Type != Format.Unknown))
    {
        Console.WriteLine($"  Line: {line}");
    }
}
```

### Advanced Format Conversion

#### VBI ↔ T42 Conversion

```csharp
// VBI to T42
using var vbi = new VBI("input.vbi");
vbi.OutputFormat = Format.T42;
vbi.SetOutput("output.t42");

// T42 to VBI
using var t42 = new T42("input.t42");
t42.OutputFormat = Format.VBI;
t42.SetOutput("output.vbi");

// T42 to VBI Double (2-line format)
using var t42Double = new T42("input.t42");
t42Double.OutputFormat = Format.VBI_DOUBLE;
t42Double.SetOutput("output.vbid");
```

### SMPTE Timecode

```csharp
using nathanbutlerDEV.libopx;

// Create timecode from string
var timecode = new Timecode("10:00:00:00");

// Create timecode from components
var timecode2 = new Timecode(10, 0, 0, 0, 25); // 25fps

// Timecode calculations
var nextFrame = timecode.AddFrames(1);
var oneSecondLater = timecode.AddSeconds(1);

Console.WriteLine($"Original: {timecode}");
Console.WriteLine($"Next frame: {nextFrame}");
Console.WriteLine($"One second later: {oneSecondLater}");
```

### Constants and Enums

```csharp
using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Enums;

// Predefined row sets
var captionRows = Constants.CAPTION_ROWS;     // Rows 1-24
var defaultRows = Constants.DEFAULT_ROWS;     // Rows 0-24

// Predefined magazine set
var defaultMagazines = Constants.DEFAULT_MAGAZINES;  // Magazines 1-8

// Format types
var vbiFormat = Format.VBI;
var t42Format = Format.T42;
var vbiDoubleFormat = Format.VBI_DOUBLE;
```

## Architecture

### Core Components

- **Formats/**: Format parsers for MXF, BIN, VBI, and T42 files
- **SMPTE/**: Complete SMPTE metadata system with XML-based definitions
- **Enums/**: Enumeration definitions (LineFormat, Function)
- **Timecode.cs**: SMPTE timecode handling with frame rate support
- **TeletextCharset.cs**: Teletext character set mapping to Unicode
- **Constants.cs**: Project-wide constants and default values

### Key Features

- **Streaming Parsers**: All parsers use `IEnumerable` with `yield return` for memory-efficient processing
- **Automatic Format Detection**: Input format detection based on file extensions
- **Unicode Support**: Proper teletext character mapping via `TeletextCharset`
- **Filtering Capabilities**: Magazine and row-based filtering during parsing
- **Format Conversion**: Seamless conversion between VBI and T42 formats

### Design Patterns

**Consistent Interface**: All format parsers follow the same pattern:

```csharp
IEnumerable<T> Parse(int? magazine = null, int[]? rows = null)
```

**Resource Management**: All parsers implement `IDisposable` for proper resource cleanup

**Streaming Processing**: Memory-efficient processing using `yield return` for large files

## Performance Considerations

- Use `using` statements to ensure proper resource disposal
- For large files, consider filtering during parsing rather than post-processing
- The library uses buffered reading for optimal I/O performance
- Memory usage remains constant regardless of file size due to streaming architecture

## Examples

See [EXAMPLES.md](EXAMPLES.md) for detailed usage patterns and code examples.

## Requirements

- .NET 9 or later
- Supported platforms: Windows, Linux, macOS

## Dependencies

- No external dependencies for core library functionality
- Uses only .NET 9 standard library components
