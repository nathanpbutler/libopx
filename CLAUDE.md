# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Common Commands

### Build

```bash
dotnet build
```

### Test

```bash
dotnet test
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

### Single test run

```bash
dotnet test --filter "TestMethodName"
```

### Parse VBI files

```bash
# Parse VBI file with magazine and row filtering, converting to T42 format
using var vbi = new VBI("input.vbi");
foreach (var line in vbi.Parse(magazine: 8, rows: Constants.CAPTION_ROWS))
{
    Console.WriteLine(line);
}
```

## Project Architecture

This is a .NET 9 C# library (`libopx`) for parsing and extracting data from MXF (Material Exchange Format), BIN, and VBI (Vertical Blanking Interval) files, with SMPTE timecode and teletext support.

### Core Structure

- **lib/**: Main library containing core classes
  - **Formats/**: Format parsers (`MXF.cs`, `BIN.cs`, `VBI.cs`)
  - `Timecode.cs` & `TimecodeComponent.cs`: SMPTE timecode handling
  - `Keys.cs`: MXF key definitions and SMPTE element mappings
  - `Packet.cs` & `Line.cs`: Data structure classes
  - `Functions.cs`: Utility functions
  - `T42.cs`: T42 teletext format processing
  - `TeletextCharset.cs`: Teletext character set mapping to Unicode
  - `Constants.cs`: Project-wide constants and default values
  - `Enums/`: Enumeration definitions (LineFormat, Function)
  - **SMPTE/**: Complete SMPTE metadata system with XML-based definitions

- **tests/**: xUnit test suite with performance tests and timecode validation
- **tools/mxfExtract/**: CLI tool using System.CommandLine for extracting streams from MXF files
- **temp/**: Sample test data files

### Key Architectural Patterns

**Streaming Parsers**: All format parsers use IEnumerable with yield return for memory-efficient processing:

- `BIN.Parse()` returns IEnumerable\<Packet\> with filtering by magazine and rows
- `VBI.Parse()` returns IEnumerable\<Line\> with automatic VBI-to-T42 conversion
- Both support streaming from stdin and filtering during parsing

**Format Conversion**: Automatic format conversion capabilities:

- VBI to T42 conversion using `VBI.ToT42()` method
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

- Uses modern C# features (required members, implicit usings, nullable reference types)
- Performance-optimized with reusable buffers and stream-based processing
- File-based output with configurable extensions and naming schemes
- Extensive timecode calculations supporting various frame rates and drop-frame modes
- Teletext support with magazine/row filtering and Unicode character mapping
- See `EXAMPLES.md` for detailed usage patterns of all format parsers
