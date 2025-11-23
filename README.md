# libopx

[![NuGet](https://img.shields.io/nuget/v/libopx?style=flat-square)](https://www.nuget.org/packages/libopx)
[![GitHub Release](https://img.shields.io/github/v/release/nathanpbutler/libopx?style=flat-square)](https://github.com/nathanpbutler/libopx/releases)
[![Build Status](https://img.shields.io/github/actions/workflow/status/nathanpbutler/libopx/ci.yml?branch=main&style=flat-square)](https://github.com/nathanpbutler/libopx/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9-blue?style=flat-square)](https://dotnet.microsoft.com/download/dotnet/9.0)

A .NET 9 C# library for parsing and extracting data from MXF (Material Exchange Format) files and their ancillary data streams (ANC), raw Vertical Blanking Interval (VBI), Teletext packet stream (T42), and MPEG-TS (Transport Stream) files, with SMPTE timecode and Teletext caption support.

## Why "opx"?

The **opx** in libopx is a play on both MXF's "Operational Pattern" designations, such as **OP-1a** and **OP-Atom**, and the Free TV Australia "Operational Practice" standards **OP-42** and **OP-47** for closed captions and Teletext subtitles (the primary focus of this library).

## Features

- **Multi-format support**: MXF, ANC, VBI, T42, and MPEG-TS file parsing
- **Format and Subtitle conversion**: Automatic VBI ↔ T42 conversion, plus RCWT (Raw Captions With Time) and EBU STL output
- **MPEG-TS teletext extraction**: Automatic PAT/PMT parsing with DVB teletext support
- **Teletext filtering**: Magazine and row-based filtering with Unicode mapping
- **SMPTE timecode**: Full timecode calculations with various frame rates
- **MXF processing**: Stream extraction and demuxing capabilities
- **CLI tool**: Unified `opx` command-line interface for easy access to all features

Convert between HD and SD T42 and VBI formats or vice versa. You can even pipe that data to other applications like `ffmpeg` or `mpv`.

## Examples

**Piping Teletext Data**: Extract ancillary data from an MXF file, convert it to vertical blanking interval (VBI) format, and pipe it directly to `mpv` for display:

![Piping Example](https://raw.githubusercontent.com/nathanpbutler/libopx/main/assets/mpv-piping-example.jpg)

**ANSI Escaped Teletext**: Supports parsing and filtering of teletext data with ANSI escape sequences:

![VHS Teletext Comparison](https://raw.githubusercontent.com/nathanpbutler/libopx/main/assets/filtering.png)

## Quick Start

### Installation

Add the package reference to your project file from [NuGet.org](https://www.nuget.org/packages/libopx).

```bash
dotnet add package libopx
```

Or download the latest release from [GitHub Releases](https://github.com/nathanpbutler/libopx/releases).

### Using the CLI Tool

```bash
# Filter teletext data by magazine and rows
opx filter -m 8 -r 20,22 input.vbi

# Filter MPEG-TS teletext (auto-detects PIDs)
opx filter input.ts

# Filter MPEG-TS with specific PID
opx filter --pid 70 input.ts

# Convert between formats (auto-detected from extension)
opx convert input.vbi output.t42

# Convert MPEG-TS to T42
opx convert --pid 70 input.ts output.t42

# Convert to EBU STL subtitle format
opx convert -c input.mxf output.stl

# Extract specific streams from MXF files (data, video, audio, etc.)
opx extract -k d,v input.mxf
```

### Using the Library

```csharp
using nathanbutlerDEV.libopx.Formats;

// Parse and filter teletext data from VBI
using var vbi = new VBI("input.vbi");
foreach (var line in vbi.Parse(magazine: 8, rows: new[] { 20, 22 }))
{
    Console.WriteLine(line);
}

// Parse and filter teletext from MPEG-TS (auto-detects PIDs)
using var ts = new TS("input.ts");
foreach (var line in ts.Parse(magazine: 8))
{
    Console.WriteLine(line);
}

// Parse MPEG-TS with specific PID
using var ts2 = new TS("input.ts") { PIDs = new[] { 70 } };
foreach (var line in ts2.Parse())
{
    Console.WriteLine(line);
}
```

## Documentation

- **[Library Documentation](lib/README.md)** - Detailed API reference and usage examples
- **[CLI Tool Documentation](apps/opx/README.md)** - Complete command reference and examples

## Project Structure

```plaintext
libopx/            # Root directory
├── .github/       # GitHub configuration
│   └── workflows/ # CI workflows
├── apps/opx/      # CLI tool
├── assets/        # Images and media samples for documentation
├── docs/          # Documentation files
├── lib/           # Main library
│   ├── Core/      # Core functionality
│   ├── Enums/     # Enumerations
│   ├── Formats/   # Format parsers (MXF, ANC VBI, T42, TS)
│   ├── Handlers/  # Data handlers
│   └── SMPTE/     # SMPTE metadata system
├── scripts/       # Development scripts
└── tests/         # xUnit test suite
    ├── Core/      # Core tests
    ├── Formats/   # Format parser tests
    └── Handlers/  # Data handler tests
```

## Development

```bash
# Clone and build
git clone https://github.com/nathanpbutler/libopx
cd libopx
dotnet build

# Run tests
dotnet test
```

## Requirements

- .NET 9 or later
- Supported platforms: Windows, Linux, macOS

## Dependencies

- System.CommandLine (v2.0.0) (command-line parsing)
- xUnit (testing)
- coverlet (code coverage)

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Ensure all tests pass
6. Submit a pull request

## License

MIT - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- **[vhs-teletext](https://github.com/ali1234/vhs-teletext)** - Software to recover teletext data from VHS recordings (inspiration for `filter` command)
- **[MXFInspect](https://github.com/Myriadbits/MXFInspect)** - Tool for displaying the internal structure of MXF files (was super helpful for understanding MXF parsing and the intricacies of SMPTE timecodes)
- **[bmxtranswrap](https://github.com/bbc/bmx)** - BBC's MXF processing library and utilities (inspiration for `extract` command)
- **[SubtitleEdit](https://github.com/SubtitleEdit/subtitleedit)** - An incredibly useful C# library and software for subtitle editing and processing
- **[CCExtractor](https://github.com/CCExtractor/ccextractor)** - Closed caption and subtitle extraction and processing
- **[ffmpeg](https://ffmpeg.org/)** - The swiss army knife of multimedia processing

## Support

For questions and support, please [open an issue](https://github.com/nathanpbutler/libopx/issues).
