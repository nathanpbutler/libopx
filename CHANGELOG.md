# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.1.1] - 2025-10-30

### Fixed

* **TS → VBI/VBI_DOUBLE conversion producing 0-byte output** - Fixed TS parser to properly
  convert extracted T42 data to requested output format. The `CreateLineFromT42` method now
  uses `Line.ParseLine()` to handle format conversion instead of manually setting properties.
* **Metadata loss when converting to VBI/VBI_DOUBLE formats** - Fixed `Line.ParseLine()` to
  preserve magazine and row metadata from input format when converting to VBI/VBI_DOUBLE.
  Since VBI formats have no readable metadata, the parser now extracts metadata from the
  pre-conversion T42 data, ensuring magazine/row filtering works correctly.
* **VBI → STL conversion producing only header with no subtitle data** - Updated VBI parser
  to convert to T42 first when outputting STL format, allowing proper metadata extraction.
  STL TTI blocks require magazine/row information which VBI format doesn't have. Both
  `Parse()` and `ParseAsync()` methods now handle STL output.
* **VBI.ValidOutputs array incomplete** - Added `Format.RCWT` and `Format.STL` to the list
  of supported output formats for VBI parser.

### Technical Details

* Modified `TS.CreateLineFromT42()` (TS.cs:644-667) to use `ParseLine()` for conversions
* Modified `Line.ParseLine()` (Line.cs:360-423) to save pre-conversion data for metadata extraction
* Modified `VBI.Parse()` and `VBI.ParseAsync()` (VBI.cs:172, 305) to handle STL output format

## [2.1.0] - 2025-10-30

### Added

* **MPEG-TS (Transport Stream) format support** - Complete implementation of TS parser
  with teletext extraction capabilities. Features:
  * Automatic PAT (Program Association Table) and PMT (Program Map Table) parsing
    for teletext PID detection
  * Manual PID specification via `PIDs` property and `--pid` CLI option
  * PES (Packetized Elementary Stream) data accumulation across TS packets
  * DVB teletext data unit extraction with proper framing byte handling
  * Support for both teletext (0x02) and teletext subtitle (0x03) data units
  * Verbose debugging output for troubleshooting stream parsing
* New `TS` parser class in `lib/Formats/TS.cs` with sync/async parsing methods
* New `Format.TS` enum value for MPEG-TS format detection
* `--pid` CLI option for both `filter` and `convert` commands
* `ParsePidsString` helper method in `CommandHelpers.cs` for PID parsing
* TS-specific constants in `Constants.cs` (packet sizes, sync bytes, stream types, PIDs)
* TS format support in `FilterAsync` and `ConvertAsync` with optional `pids` parameter
* Auto-detection for `.ts` file extension

### Fixed

* **LSB-first to MSB-first bit reversal** - DVB teletext data in MPEG-TS is transmitted
  with LSB-first bit order, requiring bit reversal before T42 processing. Added
  `ReverseBits` method to properly decode teletext bytes from transport streams
* **44-byte DVB teletext data units** - Updated data unit length validation to accept
  both 42-byte (raw T42) and 44-byte (2 framing bytes + 42 T42 bytes) data units,
  properly skipping framing bytes when present

### Documentation

* Updated `README.md` with MPEG-TS examples and format information
* Updated `EXAMPLES.md` with complete TS parsing and conversion examples
* Updated `AGENTS.md` with TS parser documentation and usage patterns
* Updated `.github/copilot-instructions.md` to include TS parser
* Updated `lib/README.md` with TS API reference and examples
* Updated `apps/opx/README.md` with TS CLI examples and `--pid` option documentation

## [2.0.0] - 2025-10-30

### Added

* RCWT (Raw Captions With Time) format support (initial implementation) (`aeb967a`)
* **EBU STL (EBU-Tech 3264) format support** - Full implementation of STL output format
  including GSI header generation and TTI block creation with proper timecode encoding
  and control code mapping (without odd-parity encoding). Features:
  * Automatic filtering of empty lines (space-only content)
  * Proper header row (row 0) text extraction (32 bytes vs 40 bytes for caption rows)
  * BCD timecode encoding for Time Code In/Out fields
  * Control code remapping from T42 to STL equivalents
* Header writing framework for RCWT and STL formats (foundation for future
  caption format output) (`f444334`)
* `SampleFiles.cs` helper for automatic test sample downloads from GitHub releases
* Support for `OPX_SAMPLES_VERSION` environment variable to control sample version

### Changed

* **BREAKING: Timecode class refactored with property encapsulation** - All properties
  (Hours, Minutes, Seconds, Frames, Timebase, DropFrame) now have private setters for
  better immutability. Implements `IEquatable<Timecode>` and `IComparable<Timecode>`
  interfaces with improved drop frame calculation logic and enhanced validation
* Refactored BIN class to MXFData nested class to clarify that BIN files are
  extracted MXF data streams, not a standalone format. CLI continues to accept
  *.bin file extension for backward compatibility
* Refactored MXF class to simplify key extraction logic and remove unnecessary
  restripe condition
* Code quality improvements addressing GitHub security scanning alerts
* Unified CLI command set and memory benchmark tooling to complete async parsing
  implementation (`b383475`)
* Migrated from Git LFS to GitHub releases for sample file distribution
* Reduced repository size by ~99% (359MB → 1.5MB) through Git LFS removal
* Updated test infrastructure to download samples on-demand from releases

### Fixed

* **Row filtering now applies to all output formats** - Fixed critical bug where magazine
  and row filtering (including `-c`/`--caps` flag) only applied to T42 and RCWT formats,
  causing row 0 (header rows) to leak through when converting to STL and other formats.
  Updated filtering logic in 8 parser methods across T42.cs, VBI.cs, and MXF.cs
* **STL header row text extraction** - Fixed STL text extraction to properly handle row 0
  structure (skipping first 10 bytes instead of 2) to prevent control codes and metadata
  from appearing as garbage text in subtitle output
* **Corrected `-c`/`--caps` flag description** - Updated help text to reflect actual
  default rows (0-31) instead of incorrectly stated (0-24)
* RCWT format conversion issues for T42 input (`b414ab5`)
* Broken async MXF restriping methods functionality (`b1487a7`)

### Removed

* Git LFS support and tracked sample files from repository history

### Documentation

* Introduced CHANGELOG.md adopting Keep a Changelog format (retroactive entries
  added) (`a976440`)
* Corrected RCWT format name from "Raw Caption With Timing" to "Raw Captions
  With Time" throughout codebase and documentation

## [1.4.0] - 2025-09-03

### Added

* Async parsing methods for BIN, VBI, T42, MXF (`99bd172`)
* AsyncHelpers utility class (`99bd172`)
* Enhanced CLI with Ctrl+C cancellation support and progress reporting (`99bd172`)

### Changed

* README refactor with updated documentation and examples (`621aa54`)

### Performance

* ArrayPool buffer management reducing memory usage by ~30–45% (`99bd172`)
* Optimized async parsing pipelines for improved throughput (`99bd172`)

## [1.3.0] - 2025-08-17

### Changed

* Enhanced T42.GetText parsing to handle header rows; resolved header row edge
  cases; improved SMPTE XML documentation reducing warnings (`30d99ae`)

## [1.2.0] - 2025-08-16

### Changed

* Refactored CLI commands; standardized default magazine behavior; improved CLI
  usability (`4a49f69`)

## [1.1.0] - 2025-08-13

### Added

* Comprehensive XML documentation across the entire codebase
* Filter examples in README with new sample files
* Enhanced sample test files and updated .gitignore

### Changed

* Input/output parameters now use arguments instead of options
* Updated README with comprehensive usage examples
* Removed Seek functionality from VBI/T42 classes

### Fixed

* Piping to stdout with convert command functionality
* Command-line argument handling improvements

### Documentation

* Complete XML documentation coverage
* Updated README.md with detailed examples
* Added filter command examples

## [1.0.0] - 2025-08-03

### Added

* **Initial major release** of libopx library
* Unified CLI application (`opx`) consolidating multiple tools:
  * `filter` - Filter teletext data by magazine and rows
  * `extract` - Extract/demux streams from MXF files
  * `restripe` - Restripe MXF files with new start timecode
  * `convert` - Convert between different teletext data formats
* Complete format support:
  * **MXF (Material Exchange Format)** parsing and extraction
  * **BIN** file processing with streaming support
  * **VBI (Vertical Blanking Interval)** parsing with T42 conversion
  * **T42 (Teletext packet stream)** processing with filtering
* **Format conversion system** with automatic detection and conversion between:
  * BIN ↔ VBI ↔ T42 ↔ MXF formats
  * Automatic VBI-to-T42 conversion during parsing
* **SMPTE timecode support**:
  * Comprehensive timecode calculations with various frame rates
  * Drop-frame mode handling
  * Timecode byte generation for SMPTE integration
  * Sequential timecode validation
* **Teletext processing**:
  * Magazine and row filtering capabilities
  * Unicode character mapping via TeletextCharset
  * Caption row support (rows 1-24) and standard rows (0-24)
* **MXF processing features**:
  * Key-based filtering using KeyType enum (Data, Video, System, etc.)
  * Extract mode for stream extraction to separate files
  * Demux mode for extracting all found keys as individual files
  * KLV mode for including key/length bytes in output
  * Progress tracking and in-place modification for restriping
* **Streaming architecture**:
  * Memory-efficient IEnumerable processing with yield return
  * Stream-based parsing for large files
  * Standard input/output support for Unix-style piping
* **SMPTE metadata system**:
  * Complete SMPTE essence element definitions
  * XML-based metadata loading
  * Comprehensive essence element, group, type, and label support
* **CLI features**:
  * Modern command-line interface using System.CommandLine
  * Verbose output options across all commands
  * Flexible input/output handling with format auto-detection
  * Progress reporting for long-running operations

### Added - Testing & Development

* Comprehensive xUnit test suite with coverlet code coverage
* Sample test files with Git LFS integration
* Performance benchmarking and validation tests
* Memory usage optimization testing
* Timecode validation across various scenarios

### Added - CI/CD & Automation

* GitHub Actions workflows for:
  * Automated testing and code coverage
  * Claude Code Review integration
  * Pull request assistance automation
* PowerShell and shell scripts for test file creation
* FFmpeg integration for test file generation with various frame rates

### Added - Documentation

* Complete project documentation in CLAUDE.md
* Detailed usage examples and command reference
* Architecture documentation and development guidelines
* Format conversion examples and best practices

### Technical Details

* **Target Framework**: .NET 9 with modern C# features
* **Architecture**: Multi-project solution with library and CLI separation
* **Performance**: Optimized with stream-based processing and memory management
* **Publishing**: Single-file deployment with ReadyToRun optimization
* **Dependencies**: Minimal external dependencies with System.CommandLine for CLI

[unreleased]: https://github.com/nathanpbutler/libopx/compare/v2.1.1...HEAD
[2.1.1]: https://github.com/nathanpbutler/libopx/compare/v2.1.0...v2.1.1
[2.1.0]: https://github.com/nathanpbutler/libopx/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/nathanpbutler/libopx/compare/v1.4.0...v2.0.0
[1.4.0]: https://github.com/nathanpbutler/libopx/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/nathanpbutler/libopx/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/nathanpbutler/libopx/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/nathanpbutler/libopx/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/nathanpbutler/libopx/releases/tag/v1.0.0
