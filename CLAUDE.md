# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Common Commands

### Build

```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build lib/libopx.csproj
dotnet build tools/mxfExtract/mxfExtract.csproj
dotnet build tools/filter/filter.csproj

# Release build
dotnet build -c Release
```

### Test

```bash
# Run all tests
dotnet test

# Run specific test method
dotnet test --filter "TestMethodName"

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "ClassName"
```

### Publish Tools

```bash
# Publish mxfExtract as single-file executable
dotnet publish tools/mxfExtract -c Release -r win-x64 --self-contained
dotnet publish tools/mxfExtract -c Release -r linux-x64 --self-contained
dotnet publish tools/mxfExtract -c Release -r osx-x64 --self-contained

# Publish filter as single-file executable
dotnet publish tools/filter -c Release -r win-x64 --self-contained
dotnet publish tools/filter -c Release -r linux-x64 --self-contained
dotnet publish tools/filter -c Release -r osx-x64 --self-contained
```

### Run mxfExtract tool

```bash
dotnet run --project tools/mxfExtract -- [options] <input-file>
```

**Available options:**

- `-k, --keys <d|v|s|t|a>`: Extract specific keys (data, video, system, timecode, all)
- `-d, --demux`: Demux mode - extract all found keys as individual files
- `-n, --name`: Use human-readable names instead of hex for output files
- `--klv`: Include key/length bytes in output
- `-v, --version`: Show version information

### Run filter tool

```bash
dotnet run --project tools/filter -- [options] <input-file?>
```

**Available options:**

- `-m, --magazine <int>`: Filter by magazine number (default: 8)
- `-r, --rows <int[]>`: Filter by number of rows (comma-separated, default: caption rows)
- `-f, --format <bin|vbi|vbid|t42>`: Input format override (default: vbi)
- `-l, --line-count <int>`: Number of lines per frame for timecode incrementation (default: 2)
- `-V, --verbose`: Enable verbose output
- `-v, --version`: Show version information

**Usage examples:**

```bash
# Filter stdin for magazine 8, caption rows only
cat input.vbi | dotnet run --project tools/filter

# Filter specific file for magazine 1, rows 0 and 23
dotnet run --project tools/filter -- -m 1 -r 0,23 input.vbi

# Process BIN file with verbose output
dotnet run --project tools/filter -- -f bin --verbose input.bin

# Publish filter as single-file executable
dotnet publish tools/filter -c Release -r win-x64 --self-contained
dotnet publish tools/filter -c Release -r linux-x64 --self-contained
dotnet publish tools/filter -c Release -r osx-x64 --self-contained
```

**Note:** If input file is not specified, reads from stdin. Format is auto-detected from file extension or can be overridden with -f option.

### Parse VBI and T42 files

```csharp
# Parse VBI file with magazine and row filtering, converting to T42 format
using var vbi = new VBI("input.vbi");
foreach (var line in vbi.Parse(magazine: 8, rows: Constants.CAPTION_ROWS))
{
    Console.WriteLine(line);
}

# Parse T42 file with filtering
using var t42 = new T42("input.t42");
foreach (var line in t42.Parse(magazine: 8, rows: Constants.CAPTION_ROWS))
{
    Console.WriteLine(line);
}
```

## Project Architecture

This is a .NET 9 C# library (`libopx`) for parsing and extracting data from MXF (Material Exchange Format), BIN, and VBI (Vertical Blanking Interval) files, with SMPTE timecode and teletext support.

### Solution Structure

The project uses a multi-project solution (`libopx.sln`) with the following structure:

- **lib/**: Main library (`libopx.csproj`) containing core classes
  - **Formats/**: Format parsers (`MXF.cs`, `BIN.cs`, `VBI.cs`, `T42.cs`)
  - `Timecode.cs` & `TimecodeComponent.cs`: SMPTE timecode handling
  - `Keys.cs`: MXF key definitions and SMPTE element mappings
  - `Packet.cs` & `Line.cs`: Data structure classes
  - `Functions.cs`: Utility functions
  - `TeletextCharset.cs`: Teletext character set mapping to Unicode
  - `Constants.cs`: Project-wide constants and default values
  - `Enums/`: Enumeration definitions (LineFormat, Function)
  - **SMPTE/**: Complete SMPTE metadata system with XML-based definitions

- **tests/**: xUnit test suite (`libopx.Tests.csproj`)
  - Performance tests and timecode validation
  - Uses coverlet for code coverage collection
  - Includes sample test data files from `samples/` directory

- **tools/**: Command-line tools folder
  - **mxfExtract/**: CLI tool for extracting streams from MXF files
    - Uses System.CommandLine for argument parsing
    - Configured for single-file publishing with ReadyToRun optimization
  - **filter/**: CLI tool for teletext stream filtering and format conversion
    - Supports BIN, VBI, and T42 format input with auto-detection
    - Filters by magazine number and row selection
    - Streaming from stdin or file input
    - Uses System.CommandLine for argument parsing

- **temp/**: Sample test data files

### Key Architectural Patterns

**Streaming Parsers**: All format parsers use IEnumerable with yield return for memory-efficient processing:

- `BIN.Parse()` returns IEnumerable\<Packet\> with filtering by magazine and rows
- `VBI.Parse()` returns IEnumerable\<Line\> with automatic VBI-to-T42 conversion
- `T42.Parse()` returns IEnumerable\<Line\> with filtering and optional format conversion
- All support streaming from stdin and filtering during parsing

**Format Conversion**: Automatic format conversion capabilities:

- VBI to T42 conversion using `VBI.ToT42()` method
- T42 to VBI conversion using `T42.ToVBI()` method
- `Line.ParseLine()` handles format detection and conversion based on OutputFormat
- Support for VBI, VBI_DOUBLE, and T42 line formats

**MXF Processing**: Stream-based parsing with:

- Required key filtering (`AddRequiredKey()` method)
- Extract mode for stream extraction to separate files
- Demux mode for extracting all found keys as individual files  
- KLV mode for including key/length bytes in output
- Sequential timecode validation

The SMPTE namespace contains comprehensive metadata definitions loaded from XML files, supporting the full SMPTE standard for essence elements, groups, types, and labels.

### Development Notes

- **Target Framework**: .NET 9 with modern C# features (required members, implicit usings, nullable reference types)
- **Build Configurations**: Supports Debug/Release builds across Any CPU, x64, and x86 platforms
- **Performance**: Optimized with reusable buffers and stream-based processing
- **Output**: File-based output with configurable extensions and naming schemes
- **Timecode**: Extensive calculations supporting various frame rates and drop-frame modes
- **Teletext**: Magazine/row filtering with Unicode character mapping via `TeletextCharset`
- **Dependencies**:
  - System.CommandLine (v2.0.0-beta6) for CLI argument parsing
  - xUnit with coverlet for testing and coverage
- **Publishing**: Command-line tools (mxfExtract, filter) configured for single-file deployment with ReadyToRun optimization
- See `lib/EXAMPLES.md` for detailed usage patterns of all format parsers
