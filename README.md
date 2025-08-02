# libopx - an MXF and OP-42/OP-47 Teletext processing library

A .NET 9 C# library for parsing and extracting data from MXF (Material Exchange Format), BIN (MXF caption data stream), VBI (Vertical Blanking Interval), and T42 (Teletext packet stream) files, with SMPTE timecode and Teletext caption support.

## Table of Contents

- [libopx - an MXF and OP-42/OP-47 Teletext processing library](#libopx---an-mxf-and-op-42op-47-teletext-processing-library)
  - [Table of Contents](#table-of-contents)
  - [Features](#features)
  - [Installation](#installation)
  - [Quick Start](#quick-start)
    - [Using the CLI Tool](#using-the-cli-tool)
    - [Using the Library](#using-the-library)
      - [Filtering Teletext Data](#filtering-teletext-data)
      - [Converting Between Formats](#converting-between-formats)
  - [CLI Commands](#cli-commands)
    - [filter - Filter teletext data](#filter---filter-teletext-data)
    - [extract - Extract/demux streams from MXF](#extract---extractdemux-streams-from-mxf)
    - [convert - Convert between teletext formats](#convert---convert-between-teletext-formats)
    - [restripe - Restripe MXF with new timecode](#restripe---restripe-mxf-with-new-timecode)
  - [Project Structure](#project-structure)
  - [Building](#building)
  - [Testing](#testing)
  - [Requirements](#requirements)
  - [Dependencies](#dependencies)
  - [Contributing](#contributing)
  - [License](#license)
  - [Support](#support)

## Features

- **Multi-format support**: BIN, VBI, T42, and MXF file parsing
- **Streaming parsers**: Memory-efficient processing with IEnumerable patterns
- **Format conversion**: Automatic VBI ↔ T42 conversion (WIP)
- **Teletext filtering**: Magazine and row-based filtering with Unicode mapping
- **SMPTE timecode**: Full timecode calculations with various frame rates
- **MXF processing**: Stream extraction and demuxing capabilities
- **CLI tool**: Unified `opx` command-line interface

## Installation

<!-- Add installation instructions -->
```bash
# Clone the repository
git clone https://github.com/nathanpbutler/libopx
cd libopx

# Build the solution
dotnet build

# Run tests (to be implemented)
dotnet test
```

## Quick Start

### Using the CLI Tool

```bash
# Filter teletext data by magazine and rows
dotnet run --project apps/opx -- filter -m 8 -r 20 -r 22 <input>

# Extract streams from MXF files
dotnet run --project apps/opx -- extract -k d,v input.mxf

# Restripe MXF with new timecode
dotnet run --project apps/opx -- restripe -t 10:00:00:00 input.mxf
```

### Using the Library

#### Filtering Teletext Data

```csharp
using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Formats;

// Parse MXF file with filtering
using var mxf = new MXF("input.mxf");
foreach (var line in mxf.Parse(magazine: 8, rows: Constants.CAPTION_ROWS))
{
    Console.WriteLine(line);
}

// Parse VBI file with filtering
using var vbi = new VBI("input.vbi");
foreach (var line in vbi.Parse(magazine: 8, rows: Constants.CAPTION_ROWS))
{
    Console.WriteLine(line);
}

// Parse T42 file
using var t42 = new T42("input.t42");
foreach (var line in t42.Parse(magazine: 8))
{
    Console.WriteLine(line);
}
```

#### Converting Between Formats

```csharp
using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Formats;
using nathanbutlerDEV.libopx.Enums;

// Format conversion pattern - works for VBI, T42, BIN, and MXF
using var parser = new VBI("input.vbi") // or T42, BIN, MXF
{
    OutputFormat = Format.T42 // or Format.VBI, Format.VBI_DOUBLE
};
parser.SetOutput("output.t42");

// For VBI/T42 (returns IEnumerable<Line>)
foreach (var line in parser.Parse(magazine: 8, rows: Constants.DEFAULT_ROWS))
{
    parser.Output.Write(line.Data);
}

// For BIN/MXF (returns IEnumerable<Packet>)
foreach (var packet in parser.Parse(magazine: 8, rows: Constants.DEFAULT_ROWS))
{
    foreach (var line in packet.Lines.Where(l => l.Type != Format.Unknown))
    {
        parser.Output.Write(line.Data);
    }
}

parser.Dispose(); // Ensure output is flushed
```

## CLI Commands

### filter - Filter teletext data

```bash
opx filter [options] <input-file?>
```

**Options:**

- `-m, --magazine <int>`: Filter by magazine number (default: 8)
- `-r, --rows <int[]>`: Filter by rows (comma-separated)
- `-f, --format <bin|vbi|vbid|t42>`: Input format override
- `-V, --verbose`: Enable verbose output

**Examples:**

```bash
# Filter VBI data for magazine 8, rows 20 and 22
opx filter -m 8 -r 20,22 input.vbi

# Filter T42 data for magazine 8
opx filter -m 8 input.t42

# Filter MXF data for magazine 8, rows 20 and 22
opx filter -m 8 -r 20,22 input.mxf
```

### extract - Extract/demux streams from MXF

```bash
opx extract [options] <input.mxf>
```

**Options:**

- `-o, --output <string>`: Output base path
- `-k, --key <d|v|s|t|a>`: Extract specific keys
- `-d, --demux`: Extract all keys found
- `--klv`: Include key and length bytes
- `-V, --verbose`: Enable verbose output

**Examples:**

```bash
# Extract all streams from MXF file
opx extract input.mxf

# Extract only data and video streams
opx extract -k d,v input.mxf
```

### convert - Convert between teletext formats

```bash
opx convert [options] <input-file?>
```

**Options:**

- `-i, --input-format <bin|vbi|vbid|t42|mxf>`: Input format (auto-detected if not specified)
- `-o, --output-format <vbi|vbid|t42>`: Output format [required]
- `-f, --output-file <file>`: Output file path (writes to stdout if not specified)
- `-m, --magazine <int>`: Filter by magazine number (default: 8)
- `-r, --rows <int[]>`: Filter by rows (comma-separated)
- `-l, --line-count <int>`: Lines per frame for timecode (default: 2)
- `-V, --verbose`: Enable verbose output

**Examples:**

```bash
# Convert VBI to T42 format (auto-detect input)
opx convert -o t42 input.vbi

# Convert MXF data stream to T42 with file output
opx convert -i mxf -o t42 -f output.t42 input.mxf

# Convert T42 to VBI with magazine/row filtering
opx convert -i t42 -o vbi -m 8 -r 20,22 input.t42

# Pipe a D10 MXF from FFmpeg to opx and convert to T42
ffmpeg -v error -i input.mxf -vf crop=720:2:0:28 -f rawvideo -pix_fmt gray - | ./opx convert -i vbi -o t42 -f output.t42 -
```

### restripe - Restripe MXF with new timecode

```bash
opx restripe [options] <input-file>
```

**Options:**

- `-t, --timecode <string>`: New start timecode (HH:MM:SS:FF) [required]
- `-V, --verbose`: Enable verbose output

## Project Structure

```plaintext
libopx/
 lib/                # Main library
   Formats/          # Format parsers (MXF, BIN, VBI, T42)
   SMPTE/            # SMPTE metadata system
   Enums/            # Enumeration definitions
 apps/
   opx/              # Unified CLI tool
 tests/              # xUnit test suite
 scripts/            # Utility scripts
```

## Building

```bash
# Build entire solution
dotnet build

# Release build
dotnet build -c Release

# Publish CLI tool as single-file executable
dotnet publish apps/opx -c Release -r win-x64 --self-contained
dotnet publish apps/opx -c Release -r linux-x64 --self-contained
dotnet publish apps/opx -c Release -r osx-x64 --self-contained
```

## Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test
dotnet test --filter "TestMethodName"
```

## Requirements

- .NET 9 or later
- Supported platforms: Windows, Linux, macOS

## Dependencies

- System.CommandLine (v2.0.0-beta6)
- xUnit (testing)
- coverlet (code coverage)

## Contributing

<!-- Add contribution guidelines -->
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Ensure all tests pass
6. Submit a pull request

## License

<!-- Add your license information -->
[License Type] - see the [LICENSE](LICENSE.md) file for details.

## Support

<!-- Add support information -->
For questions and support, please [open an issue](../../issues).
