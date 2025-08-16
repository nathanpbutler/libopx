# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Common Commands

### Build

```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build lib/libopx.csproj
dotnet build apps/opx/opx.csproj
dotnet build tests/libopx.Tests.csproj

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

### Publish opx Tool

```bash
# Publish opx as single-file executable
dotnet publish apps/opx -c Release -r win-x64 --self-contained
dotnet publish apps/opx -c Release -r linux-x64 --self-contained
dotnet publish apps/opx -c Release -r osx-x64 --self-contained
```

### Run opx tool

```bash
dotnet run --project apps/opx -- [command] [options]
```

**Available commands:**

#### filter - Filter teletext data by magazine and rows

```bash
dotnet run --project apps/opx -- filter [options] <input-file?>
```

**Options:**

- `-m, --magazine <int>`: Filter by magazine number (default: all magazines)
- `-r, --rows <int[]>`: Filter by number of rows (comma-separated, default: caption rows)
- `-f, --format <bin|vbi|vbid|t42>`: Input format override (default: vbi)
- `-l, --line-count <int>`: Number of lines per frame for timecode incrementation (default: 2)
- `-V, --verbose`: Enable verbose output

#### extract - Extract/demux streams from MXF files

```bash
dotnet run --project apps/opx -- extract [options] <input-file>
```

**Options:**

- `-o, --output <string>`: Output base path - files will be created as \<base>_d.raw, \<base>_v.raw, etc
- `-k, --key <d|v|s|t|a>`: Extract specific keys (data, video, system, timecode, audio)
- `-d, --demux`: Extract all keys found, output as \<base>_\<hexkey>.raw
- `-n`: Use Key/Essence names instead of hex keys (use with -d)
- `--klv`: Include key and length bytes in output files, use .klv extension
- `-V, --verbose`: Enable verbose output

#### restripe - Restripe MXF file with new start timecode

```bash
dotnet run --project apps/opx -- restripe [options] <input-file>
```

**Options:**

- `-t, --timecode <string>`: New start timecode (HH:MM:SS:FF) [required]
- `-V, --verbose`: Enable verbose output
- `-pp, --print-progress`: Print progress during parsing

#### convert - Convert between different teletext data formats

```bash
dotnet run --project apps/opx -- convert [options] <input-file?>
```

**Options:**

- `-i, --input-format <bin|vbi|vbid|t42|mxf>`: Input format (auto-detected from file extension if not specified)
- `-o, --output-format <vbi|vbid|t42>`: Output format [required]
- `-f, --output-file <file>`: Output file path (writes to stdout if not specified)
- `-m, --magazine <int>`: Filter by magazine number (default: all magazines)
- `-r, --rows <int[]>`: Filter by number of rows (comma-separated, default: all rows)
- `-l, --line-count <int>`: Number of lines per frame for timecode incrementation (default: 2)
- `-V, --verbose`: Enable verbose output

**Usage examples:**

```bash
# Filter stdin for all magazines, caption rows only
cat input.vbi | dotnet run --project apps/opx -- filter

# Filter specific file for magazine 1, rows 0 and 23
dotnet run --project apps/opx -- filter -m 1 -r 0,23 input.vbi

# Extract data and video streams from MXF
dotnet run --project apps/opx -- extract -k d,v input.mxf

# Restripe MXF with new timecode
dotnet run --project apps/opx -- restripe -t 10:00:00:00 input.mxf

# Convert VBI to T42 format (auto-detect input format)
dotnet run --project apps/opx -- convert -o t42 input.vbi

# Convert MXF data stream to T42 with file output
dotnet run --project apps/opx -- convert -i mxf -o t42 -f output.t42 input.mxf

# Convert T42 to VBI with filtering and verbose output
dotnet run --project apps/opx -- convert -i t42 -o vbi -m 8 -r 20,22 -V input.t42
```

**Note:** For filter command, if input file is not specified, reads from stdin. Format is auto-detected from file extension or can be overridden with -f option.

### Parse VBI and T42 files

```csharp
// Parse VBI file with magazine and row filtering, converting to T42 format
using var vbi = new VBI("input.vbi");
foreach (var line in vbi.Parse(magazine: null, rows: Constants.CAPTION_ROWS))
{
    Console.WriteLine(line);
}

// Parse T42 file with filtering
using var t42 = new T42("input.t42");
foreach (var line in t42.Parse(magazine: null, rows: Constants.CAPTION_ROWS))
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

- **apps/**: Application projects folder
  - **opx/**: Unified CLI tool combining extract, filter, and restripe functionality
    - Uses System.CommandLine for argument parsing with subcommands
    - Configured for single-file publishing with ReadyToRun optimization
    - Supports BIN, VBI, T42, and MXF format processing
    - Three main commands: filter (teletext filtering), extract (MXF stream extraction), restripe (MXF timecode modification)

- **scripts/**: PowerShell and shell scripts for test file creation and processing
- **temp/**: Sample test data files and processing output

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
- **Publishing**: Command-line tool (opx) configured for single-file deployment with ReadyToRun optimization
- See `lib/EXAMPLES.md` for detailed usage patterns of all format parsers
