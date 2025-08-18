# libopx

[![NuGet](https://img.shields.io/nuget/v/libopx?style=flat-square)](https://www.nuget.org/packages/libopx)
[![GitHub Release](https://img.shields.io/github/v/release/nathanpbutler/libopx?style=flat-square)](https://github.com/nathanpbutler/libopx/releases)
[![Build Status](https://img.shields.io/github/actions/workflow/status/nathanpbutler/libopx/ci.yml?branch=main&style=flat-square)](https://github.com/nathanpbutler/libopx/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9-blue?style=flat-square)](https://dotnet.microsoft.com/download/dotnet/9.0)

A .NET 9 C# library for parsing and extracting data from MXF (Material Exchange Format), BIN (MXF caption data stream), VBI (Vertical Blanking Interval), and T42 (Teletext packet stream) files, with SMPTE timecode and Teletext caption support.

## Features

- **Multi-format support**: BIN, VBI, T42, and MXF file parsing
- **Format conversion**: Automatic VBI ↔ T42 conversion
- **Teletext filtering**: Magazine and row-based filtering with Unicode mapping
- **SMPTE timecode**: Full timecode calculations with various frame rates
- **MXF processing**: Stream extraction and demuxing capabilities
- **CLI tool**: Unified `opx` command-line interface

**Format conversions**: Convert between HD and SD T42 and VBI formats or vice versa. You can even pipe that data to other applications like `ffmpeg` or `mpv`.

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

# Convert between formats
opx convert -o t42 input.vbi

# Extract streams from MXF files
opx extract -k d,v input.mxf
```

### Using the Library

```csharp
using nathanbutlerDEV.libopx.Formats;

// Parse and filter teletext data
using var vbi = new VBI("input.vbi");
foreach (var line in vbi.Parse(magazine: 8, rows: new[] { 20, 22 }))
{
    Console.WriteLine(line);
}
```

## Documentation

- **[Library Documentation](lib/README.md)** - Detailed API reference and usage examples
- **[CLI Tool Documentation](apps/opx/README.md)** - Complete command reference and examples

## Project Structure

```plaintext
libopx/
├── apps/opx/         # CLI tool
├── lib/              # Main library
│   ├── Formats/      # Format parsers (MXF, BIN, VBI, T42)
│   ├── SMPTE/        # SMPTE metadata system
│   └── Enums/        # Enumeration definitions
├── samples/          # Sample files for testing
├── scripts/          # Development scripts
└── tests/            # xUnit test suite
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

- System.CommandLine (v2.0.0-beta6)
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
