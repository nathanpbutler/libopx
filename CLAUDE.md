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

### Single test run

```bash
dotnet test --filter "TestMethodName"
```

## Project Architecture

This is a .NET 9 C# library (`libopx`) for parsing and extracting data from MXF (Material Exchange Format) and BIN files, with SMPTE timecode support.

### Core Structure

- **lib/**: Main library containing core classes
  - `MXF.cs` & `BIN.cs`: Primary format parsers in `Formats/` namespace
  - `Timecode.cs` & `TimecodeComponent.cs`: SMPTE timecode handling
  - `Keys.cs`: MXF key definitions and SMPTE element mappings
  - `Packet.cs` & `Line.cs`: Data structure classes
  - `Functions.cs`: Utility functions
  - `SMPTE/`: Complete SMPTE metadata system with XML-based definitions

- **tests/**: xUnit test suite with performance tests
- **tools/mxfExtract/**: CLI tool using System.CommandLine for extracting streams from MXF files

### Key Architectural Patterns

The MXF class is the primary entry point and uses a stream-based parsing approach with:

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
