# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Important Notes

- **Async Support**: All format parsers support both sync (`Parse()`) and async (`ParseAsync()`) methods with 90-95% memory reduction via ArrayPool usage
- **Sample Files**: Test sample files are automatically copied to test output directory during build - reference them directly by filename in tests (e.g., `"input.vbi"`)
- **Memory Benchmarking**: Use `MemoryBenchmarkTests` class to verify performance claims for async parsing methods
- **RCWT Support**: Full Raw Caption With Timing format support with automatic T42 payload conversion, FTS timing, and field alternation

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

# Run memory benchmark tests
dotnet test --filter "MemoryBenchmarkTests"

# Run specific memory benchmark  
dotnet test --filter "VBI_AsyncVsSync_MemoryAllocationComparison" --logger "console;verbosity=detailed"
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
- `-r, --rows <string>`: Filter by rows (comma-separated or hyphen ranges, e.g., `1,2,5-8,15`)
- `-c, --caps`: Use caption rows (1-24) instead of default rows (0-24)
- `-f, --format <bin|vbi|vbid|t42>`: Input format override (auto-detected from file extension)
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
- `-o, --output-format <vbi|vbid|t42|rcwt>`: Output format [required]
- `-f, --output-file <file>`: Output file path (writes to stdout if not specified)
- `-m, --magazine <int>`: Filter by magazine number (default: all magazines)
- `-r, --rows <string>`: Filter by rows (comma-separated or hyphen ranges)
- `-c, --caps`: Use caption rows (1-24) instead of default rows (0-24)
- `-k, --keep`: Write blank bytes if rows or magazine doesn't match
- `-l, --line-count <int>`: Number of lines per frame for timecode incrementation (default: 2)
- `-V, --verbose`: Enable verbose output

**Usage examples:**

```bash
# Filter stdin for all magazines, caption rows only
cat input.vbi | dotnet run --project apps/opx -- filter -c

# Filter specific file for magazine 1, rows 0 and 23
dotnet run --project apps/opx -- filter -m 1 -r 0,23 input.vbi

# Filter VBI data for magazine 8, rows 20-22 range
dotnet run --project apps/opx -- filter -m 8 -r 20-22 input.vbi

# Extract data and video streams from MXF
dotnet run --project apps/opx -- extract -k d,v input.mxf

# Restripe MXF with new timecode
dotnet run --project apps/opx -- restripe -t 10:00:00:00 input.mxf

# Convert VBI to T42 format (auto-detect input format)
dotnet run --project apps/opx -- convert -o t42 input.vbi

# Convert MXF data stream to T42 with file output
dotnet run --project apps/opx -- convert -i mxf -o t42 -f output.t42 input.mxf

# Convert T42 to VBI with filtering and verbose output
dotnet run --project apps/opx -- convert -i t42 -o vbi -m 8 -r 20-22 -V input.t42

# Convert VBI to T42 with caption rows only
dotnet run --project apps/opx -- convert -o t42 -c input.vbi

# Convert MXF to VBI preserving structure with blank bytes
dotnet run --project apps/opx -- convert -i mxf -o vbi -k input.mxf

# Convert VBI to RCWT (Raw Caption With Timing) format
dotnet run --project apps/opx -- convert -i vbi -o rcwt -f output.rcwt input.vbi

# Convert T42 to RCWT with verbose output
dotnet run --project apps/opx -- convert -i t42 -o rcwt -V input.t42
```

**Note:** For filter command, if input file is not specified, reads from stdin. Format is auto-detected from file extension or can be overridden with -f option.

### RCWT (Raw Caption With Timing) Format

RCWT is a container format that wraps T42 teletext data with timing and field information. Each RCWT packet contains:

**Packet Structure (53 bytes total):**
- 1 byte: Packet type (0x03)
- 8 bytes: FTS (Frame Time Stamp) in milliseconds, little-endian with zero padding
- 1 byte: Field marker (0xAF for field 0, 0xAB for field 1)
- 1 byte: Framing code (0x27)
- 42 bytes: T42 teletext data payload

**Usage in Code:**
```csharp
// Convert Line to RCWT format with timing
var rcwtData = line.ToRCWT(fts: 1000, fieldNumber: 0);

// State management is handled automatically during conversion
Functions.ResetRCWTHeader(); // Call at start of new conversion session
var (fts, fieldNumber) = Functions.GetRCWTState(timecode); // Uses parser's timecode
```

**Features:**
- Automatic T42 payload conversion from VBI/other formats
- Thread-safe FTS and field number state management  
- File header written once per conversion session (11 bytes: [204, 204, 237, 204, 0, 80, 0, 2, 0, 0, 0])
- Configurable timing increments (default: 40ms for 25fps)
- Field alternation (0 → 1 → 0 → 1...)
- FTS timing synchronized with parser's LineCount setting

**T42 -> RCWT Conversion Status:**
- ✅ **T42 to RCWT**: Fully working - Fixed Line.Type detection, eliminated double-conversion, synchronized FTS with timecode
- 🚧 **VBI to RCWT**: Work in progress - VBI data converted to T42 first, then to RCWT packets
- 🚧 **BIN to RCWT**: Work in progress - BIN data extracted to T42, then to RCWT packets  
- 🚧 **MXF to RCWT**: Work in progress - MXF data stream extracted to T42, then to RCWT packets

**Key Fixes Applied:**
- Added `Line.SetCachedType()` method to properly identify T42 data for RCWT conversion
- Removed redundant ToRCWT() calls from parsers to prevent double-conversion headers
- Changed FTS calculation from line-based counter to timecode-based: `FTS = FrameNumber * 40ms`
- Ensured FTS increments respect parser's LineCount setting (every N lines, not every line)

### Parse VBI and T42 files

```csharp
// Sync parsing - VBI file with magazine and row filtering, converting to T42 format
using var vbi = new VBI("input.vbi");
foreach (var line in vbi.Parse(magazine: null, rows: Constants.CAPTION_ROWS))
{
    Console.WriteLine(line);
}

// Async parsing - Better performance with ArrayPool memory management
using var vbiAsync = new VBI("large_file.vbi");
await foreach (var line in vbiAsync.ParseAsync(magazine: 8, rows: [20, 22]))
{
    Console.WriteLine(line);
}

// MXF parsing with key filtering
using var mxf = new MXF("input.mxf");
mxf.AddRequiredKey("Data"); // or KeyType.Data enum
foreach (var packet in mxf.Parse(magazine: null, rows: null))
{
    Console.WriteLine(packet);
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

- `BIN.Parse()` / `BIN.ParseAsync()` returns IEnumerable/IAsyncEnumerable\<Packet\> with filtering by magazine and rows  
- `VBI.Parse()` / `VBI.ParseAsync()` returns IEnumerable/IAsyncEnumerable\<Line\> with automatic VBI-to-T42 conversion
- `T42.Parse()` / `T42.ParseAsync()` returns IEnumerable/IAsyncEnumerable\<Line\> with filtering and optional format conversion
- `MXF.Parse()` / `MXF.ParseAsync()` returns IEnumerable/IAsyncEnumerable\<Packet\> with key-based filtering
- All support streaming from stdin and filtering during parsing
- Async methods use ArrayPool for 90-95% memory reduction

**Format Conversion**: Automatic format conversion capabilities:

- VBI to T42 conversion using `VBI.ToT42()` method
- T42 to VBI conversion using `T42.ToVBI()` method
- `Line.ParseLine()` handles format detection and conversion based on OutputFormat
- Support for VBI, VBI_DOUBLE, T42, and RCWT line formats

**MXF Processing**: Stream-based parsing with:

- Required key filtering (`AddRequiredKey()` method) using KeyType enum (Data, Video, System, TimecodeComponent, Audio)
- Extract mode for stream extraction to separate files
- Demux mode for extracting all found keys as individual files  
- KLV mode for including key/length bytes in output
- Sequential timecode validation
- Full sync/async consistency with 84.5% memory reduction

The SMPTE namespace contains comprehensive metadata definitions loaded from XML files, supporting the full SMPTE standard for essence elements, groups, types, and labels.

### Development Notes

- **Target Framework**: .NET 9 with modern C# features (required members, implicit usings, nullable reference types)
- **Build Configurations**: Supports Debug/Release builds across Any CPU, x64, and x86 platforms
- **Performance**: Optimized with ArrayPool buffers and stream-based processing; async methods provide 90-95% memory reduction
- **Output**: File-based output with configurable extensions and naming schemes
- **Timecode**: Extensive calculations supporting various frame rates and drop-frame modes
- **Teletext**: Magazine/row filtering with Unicode character mapping via `TeletextCharset`
- **RCWT Format**: Complete Raw Caption With Timing implementation with T42 payload conversion and timing state management
- **Dependencies**:
  - System.CommandLine (v2.0.0-beta6) for CLI argument parsing
  - xUnit with coverlet for testing and coverage
- **Publishing**: Command-line tool (opx) configured for single-file deployment with ReadyToRun optimization
- **Testing**: Sample files in `samples/` are automatically copied to test output directory; reference them directly by filename in tests
- **Memory Benchmarking**: `MemoryBenchmarkTests` class validates async performance improvements across all formats
- See `lib/EXAMPLES.md` for detailed usage patterns of all format parsers and `ASYNC_FEATURES.md` for async implementation details

## Testing Guidelines

- Reference sample files directly by name (e.g., `"input.vbi"`) - they are automatically copied during build
- Use `MemoryBenchmarkTests` to verify performance claims when making async parsing changes
- Test both sync and async parsing methods to ensure feature parity
- All async methods now have perfect consistency with their sync counterparts
