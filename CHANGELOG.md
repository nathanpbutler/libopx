# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

* **FormatIO fluent API** - New primary user-facing API for parsing and converting teletext data with method chaining:
  * **Factory methods**:
    * `FormatIO.Open(string path)` - Auto-detects format from file extension (.vbi, .t42, .mxf, .ts, .bin, etc.)
    * `FormatIO.OpenStdin(Format format)` - Read from standard input with explicit format
    * `FormatIO.Open(Stream stream, Format format)` - Read from custom stream
  * **Fluent configuration**:
    * `WithOptions(Action<ParseOptions>)` - Configure ParseOptions via lambda
    * `ConvertTo(Format targetFormat)` - Set output format with validation
    * `Filter(int? magazine, params int[] rows)` - Set magazine/row filters
    * `WithLineCount(int lineCount)` - Set timecode increment (625 or 525)
    * `WithStartTimecode(Timecode tc)` - Set starting timecode for packets
    * `WithPIDs(params int[] pids)` - Set TS PID filters
  * **MXF-specific fluent configuration**:
    * `WithDemuxMode(bool demux = true)` - Enable demux mode for MXF extraction
    * `WithKeyNames(bool useNames = true)` - Use key names instead of hex identifiers
    * `WithKlvMode(bool klv = true)` - Include KLV headers in output
    * `WithKeys(params KeyType[] keys)` - Specify keys to extract (Data, Video, Audio, etc.)
    * `WithProgress(bool progress = true)` - Enable progress reporting
    * `WithVerbose(bool verbose = true)` - Enable verbose output
  * **MXF terminal operations** (consume the FormatIO instance):
    * `ExtractTo(string? outputBasePath)` - Extract MXF essence streams to files
    * `ExtractToAsync(string? outputBasePath, CancellationToken)` - Async extraction
    * `Restripe(string newStartTimecode)` - Restripe MXF file with new start timecode
    * `RestripeAsync(string newStartTimecode, CancellationToken)` - Async restripe
  * **Dual parsing modes**:
    * `ParseLines()` / `ParseLinesAsync()` - Returns `IEnumerable<Line>` for line-by-line processing
    * `ParsePackets()` / `ParsePacketsAsync()` - Returns `IEnumerable<Packet>` preserving packet grouping for VBI vertical offset (720x32 or 1440x32 output)
  * **SaveTo methods**:
    * `SaveTo(string path, bool useSTLMerging = false)` - Write to file with optional STL intelligent merging
    * `SaveToStdout(bool useSTLMerging = false)` - Write to standard output
    * Async variants: `SaveToAsync()` and `SaveToStdoutAsync()` with cancellation support
  * **Automatic header writing** - RCWT (11 bytes) and STL GSI (1024 bytes) headers written automatically
  * **STL merging opt-in** - Explicit `useSTLMerging` parameter for intelligent subtitle merging (changed from automatic)
  * **Format auto-detection** - Detects format from file extensions (case-insensitive)
  * **MXF start timecode reading** - When opening MXF files, FormatIO automatically reads the start timecode from the TimecodeComponent, ensuring correct timecodes after restripe operations
  * **Custom exception** - `FormatDetectionException` for unknown file extensions
* 84 comprehensive unit tests in `tests/FormatIOTests.cs` covering all FormatIO workflows:
  * Factory method tests (12 tests) - File, stdin, stream patterns
  * Format detection tests (11 tests) - Extension mapping and case handling
  * Fluent API tests (11 tests) - Method chaining and configuration
  * Parsing tests (10 tests) - Lines, packets, sync, async, filtering
  * SaveTo tests (10 tests) - All formats, headers, STL merging
  * Conversion support tests (16 tests) - Supported/unsupported conversion validation
  * Disposal tests (5 tests) - Stream ownership and cleanup
  * Error handling tests (5 tests) - Disposal checks, null validation
  * Integration tests (4 tests) - End-to-end workflows
* **FormatConverter static class** - Centralized format conversion logic consolidating all conversion methods previously scattered across VBI.cs, T42.cs, Line.cs, and STLExporter.cs:
  * `FormatConverter.VBIToT42()` - Convert VBI/VBI_DOUBLE (720/1440 bytes) to T42 (42 bytes)
  * `FormatConverter.T42ToVBI()` - Convert T42 to VBI or VBI_DOUBLE with format selection
  * `FormatConverter.VBIToVBIDouble()` - Convert VBI to VBI_DOUBLE (720 → 1440 bytes)
  * `FormatConverter.VBIDoubleToVBI()` - Convert VBI_DOUBLE to VBI (1440 → 720 bytes)
  * `FormatConverter.T42ToRCWT()` - Convert T42 data to RCWT packet format (53-byte packets with header + payload)
  * `FormatConverter.T42ToSTL()` - Convert T42 data to STL TTI block format (128-byte blocks)
  * `FormatConverter.EncodeTimecodeToSTL()` - Unified BCD timecode encoding for STL format
  * `FormatConverter.ExtractSTLTextData()` - Unified text extraction with T42-to-STL control code remapping
* 28 comprehensive unit tests in `tests/Core/FormatConverterTests.cs` covering all conversion scenarios
* **FormatRegistry automatic handler registration** - Static constructor automatically registers all built-in format handlers (VBI, T42, ANC, TS, MXF) on first access, eliminating need for manual registration in application code

### Changed

* **CLI `--raw-stl` renamed to `--stl-merge`** - Flipped STL export default behavior:
  * **Default**: Raw STL output (one subtitle per caption line, frame-accurate timing)
  * **`--stl-merge`**: Opt-in intelligent merging (combines word-by-word caption buildup into single subtitles)
  * This change aligns with future STL options like `--stl-merge-rows` for cross-row merging

* **Functions.Filter() and Functions.FilterAsync() migrated to FormatIO**:
  * Filter() reduced from 120 lines to 60 lines (50% reduction)
  * FilterAsync() reduced from 140 lines to 75 lines (46% reduction)
  * Removed format-specific switch statements in favor of FormatIO's unified API
  * All formats including MXF now use consistent FormatIO pattern
  * Zero deprecation warnings in Filter functions

* **Deprecated format class constructors with migration path to v3.0.0**:
  * `new VBI(string)` → Use `FormatIO.Open(path)` instead (removed in v3.0.0)
  * `new VBI()` → Use `FormatIO.OpenStdin(Format.VBI)` instead (removed in v3.0.0)
  * `new VBI(Stream)` → Use `FormatIO.Open(stream, Format.VBI)` instead (removed in v3.0.0)
  * `new T42(string)` → Use `FormatIO.Open(path)` instead (removed in v3.0.0)
  * `new T42()` → Use `FormatIO.OpenStdin(Format.T42)` instead (removed in v3.0.0)
  * `new T42(Stream)` → Use `FormatIO.Open(stream, Format.T42)` instead (removed in v3.0.0)
  * `new ANC(string)` → Use `FormatIO.Open(path)` instead (removed in v3.0.0)
  * `new ANC()` → Use `FormatIO.OpenStdin(Format.ANC)` instead (removed in v3.0.0)
  * `new ANC(Stream)` → Use `FormatIO.Open(stream, Format.ANC)` instead (removed in v3.0.0)
  * `new TS(string)` → Use `FormatIO.Open(path)` instead (removed in v3.0.0)
  * `new TS()` → Use `FormatIO.OpenStdin(Format.TS)` instead (removed in v3.0.0)
  * `new TS(Stream)` → Use `FormatIO.Open(stream, Format.TS)` instead (removed in v3.0.0)
  * `MXF.MXFData` → Use `ANC` class directly (removed in v3.0.0)
* **Note:** MXF constructors (`new MXF(string)`, `new MXF(FileInfo)`) are NOT deprecated - they remain available for advanced use cases. However, **FormatIO is now the recommended API** for all operations including Extract and Restripe, which are available as first-class fluent methods
* **Deprecated conversion methods with migration path to v3.0.0**:
  * `VBI.ToT42()` → Use `FormatConverter.VBIToT42()` instead (removed in v3.0.0)
  * `T42.ToVBI()` → Use `FormatConverter.T42ToVBI()` instead (removed in v3.0.0)
  * `Line.ToRCWT()` → Use `FormatConverter.T42ToRCWT()` instead (removed in v3.0.0)
  * `Line.ToSTL()` → Use `FormatConverter.T42ToSTL()` instead (removed in v3.0.0)
* All format handlers (T42Handler, VBIHandler) now use FormatConverter internally
* `Line.ConvertFormat()` now delegates to FormatConverter for all conversions
* `Functions.cs` updated to use FormatConverter for VBI-to-T42 conversion (line 1486)

### Fixed

* **STL output now skips row 0 and empty lines** - Two fixes for cleaner STL output:
  * Row 0 (page header) is now automatically skipped - it contains station name/time, never subtitle content
  * Fixed `IsSTLLineEmpty` to strip parity bit (0x7F mask) before checking for spaces and control codes - T42 data includes odd parity on the MSB, so spaces appear as `0xA0` not `0x20`

* **Eliminated duplicate method implementations**:
  * Consolidated two identical `EncodeTimecodeToSTL()` implementations (Line.cs and STLExporter.cs) into single FormatConverter method
  * Consolidated two identical `ExtractSTLTextData()` implementations (Line.cs and STLExporter.cs) into single FormatConverter method
  * Removed duplicate helper methods from Line.cs (GetFTSBytes, GetFieldMarkerByte) - now private in FormatConverter

### Technical Details

* Created `lib/FormatIO.cs` (~1000 lines) - Complete fluent API implementation with factory pattern, dual parsing modes, format auto-detection, MXF start timecode reading, and MXF Extract/Restripe as first-class terminal operations
* Created `tests/FormatIOTests.cs` (~800 lines) - 84 comprehensive unit tests covering all workflows
* Added `[Obsolete]` attributes to 12 format class constructors across VBI.cs, T42.cs, ANC.cs, TS.cs (MXF constructors remain non-deprecated for Extract/Restripe operations)
* Created `lib/Core/FormatConverter.cs` (408 lines) with 8 public and 2 private static methods
* Created `tests/Core/FormatConverterTests.cs` (403 lines) with comprehensive test coverage
* Modified 7 files to use FormatConverter: VBI.cs, T42.cs, Line.cs, VBIHandler.cs, T42Handler.cs, Functions.cs, STLExporter.cs
* Deleted ~150 lines of duplicate code from Line.cs and STLExporter.cs
* Deleted FormatIOExtensions.cs - Extract/Restripe operations are now first-class instance methods on FormatIO with proper stream lifecycle management and consumed state tracking
* `lib/Core/ExtractResult.cs` - Result class for extraction operations containing extracted file paths, success status, and error information
* Removed dead code: ConvertToSTLAsync function (~180 lines) that was superseded by FormatIO's STL support
* All tests passing (238 existing + 84 new FormatIO tests = 322 total)
* Maintains 100% backward compatibility - all deprecated methods and constructors still work with [Obsolete] warnings
* FormatIO provides cleaner API while existing format classes remain available for advanced use cases

## [2.3.0] - 2026-01-09

### Added

* **STL subtitle paragraph merging** - New `STLExporter` class that dramatically reduces subtitle paragraph count when converting from MXF, ANC, TS to STL format:
  * **Content-based tracking** - Tracks subtitle text by content rather than row position, handling teletext's row-shifting behavior
  * **Text growth detection** - Automatically merges captions that build word-by-word (e.g., "Hello" → "Hello world" → "Hello world!")
  * **Delayed clear mechanism** - Handles frame gaps in live captioning data by buffering cleared content for up to 30 frames (~1.2 seconds at 25fps) before emitting
  * **Proper end-time detection** - Uses content disappearance to determine subtitle end times instead of arbitrary next-frame timing
  * **`--raw-stl` CLI option** - Preserves previous row-by-row behavior for users who want a more accurate representation of the original Teletext caption transmission

### Technical Details

* Created `lib/Exporters/STLExporter.cs` (~600 lines) with content-based subtitle tracking
* Added 30 unit tests in `tests/Exporters/STLExporterTests.cs` covering all merging scenarios
* Modified `lib/Functions.cs` to integrate STLExporter for STL output (unless `--raw-stl` specified)
* Modified `apps/opx/Commands.cs` to add `--raw-stl` option to convert command

## [2.2.0] - 2025-11-07

### Added

* **Handler-based architecture** - Major architectural refactoring introducing a handler pattern that separates parsing logic from I/O management across all formats. This redesign establishes a consistent, extensible foundation while maintaining 100% backward compatibility.
* **Format handler interfaces** - New interface hierarchy defining contracts for format handlers:
  * `IFormatHandlerBase` - Base interface with common properties (`InputFormat`, `ValidOutputs`)
  * `ILineFormatHandler` - Interface for line-based formats (T42, VBI) with `LineLength` property
  * `IPacketFormatHandler` - Interface for packet-based formats (TS, MXF, ANC) returning `Packet` objects
* **Format handler registry** - Thread-safe `FormatRegistry` class using `ConcurrentDictionary` for centralized handler management with registration, retrieval, and validation methods
* **Unified configuration** - `ParseOptions` class providing consistent configuration across all format handlers with properties for magazine filtering, row filtering, output format, line count, start timecode, and PID filtering
* **Five format handler implementations**:
  * `T42Handler` (250 lines) - Encapsulates T42 teletext parsing with timecode tracking and format conversion
  * `VBIHandler` (280 lines) - Handles VBI and VBI_DOUBLE parsing with automatic format detection
  * `ANCHandler` (183 lines) - Parses MXF ancillary data packets with minimal state management
  * `TSHandler` (939 lines) - Most complex packet handler managing MPEG-TS parsing with PES assembly, continuity tracking, PAT/PMT parsing, and PTS normalization
  * `MXFHandler` (1,374 lines) - Largest handler supporting KLV parsing, multi-stream management, and three operation modes (filter, extract, restripe)
* **Comprehensive test coverage** - 33 new tests added across 3 test files:
  * `ParseOptionsTests.cs` (13 tests) - Configuration object validation
  * `FormatRegistryTests.cs` (14 tests) - Registry behavior and thread-safety
  * `T42HandlerTests.cs` (15 tests) - T42 parsing logic with real sample files

### Changed

* **Format class refactoring** - All format classes now inherit from `FormatIOBase` and delegate parsing to dedicated handlers, reducing code duplication and improving maintainability:
  * `T42.cs` - Reduced by 231 lines (900 → 669) by delegating to T42Handler
  * `VBI.cs` - Reduced by 258 lines (450 → 192) with format auto-detection in handler
  * `TS.cs` - Reduced by 835 lines / 83% (1,000 → 165) moving complex state management to TSHandler
  * `MXF.cs` - Reduced by 1,029 lines / 67% (1,526 → 497) with KLV parsing moved to MXFHandler
  * `ANC.cs` - Promoted from nested `MXF.MXFData` class to top-level format class (308 lines) delegating to ANCHandler
* **State encapsulation** - Parsing state (PES buffers, continuity counters, PID tracking, timecode tracking) moved from format classes into handler instances, enabling cleaner separation of concerns
* **PTS normalization in TS parser** - TSHandler now tracks first PTS encountered and normalizes all subsequent PTS values relative to it, ensuring timecodes start at 00:00:00:00 regardless of original stream offset

### Technical Details

* Created 5 new Core infrastructure files (251 lines total):
  * `lib/Core/IFormatHandlerBase.cs` (20 lines) - Base handler interface
  * `lib/Core/ILineFormatHandler.cs` (38 lines) - Line-based format contract
  * `lib/Core/IPacketFormatHandler.cs` (32 lines) - Packet-based format contract
  * `lib/Core/FormatRegistry.cs` (108 lines) - Thread-safe handler registry
  * `lib/Core/ParseOptions.cs` (85 lines) - Unified configuration with `Clone()` method
* Created 5 new Handler classes (3,064 lines total) in `lib/Handlers/` directory
* Created 3 new test files (610 lines total) achieving comprehensive handler coverage
* Net code impact: +3,925 lines created, -2,353 lines removed from format classes
* All 66/66 tests passing (33 existing + 33 new) - zero regressions
* Architecture supports Strategy, Registry, and Options patterns
* Documentation added: `docs/PHASE2_SUMMARY.md` (152 lines) with complete architectural analysis

### Migration Notes

* **No breaking changes** - All existing public APIs remain unchanged and functional
* Format classes maintain all existing constructors, properties, and methods
* Parsing calls automatically delegate to new handler infrastructure
* Existing code continues to work without modification
* New handler-based APIs available for advanced usage scenarios

## [2.1.2] - 2025-11-04

### Changed

* **MPEG-TS parser now returns Packets instead of Lines** - The TS parser has been refactored to return `IEnumerable<Packet>` instead of `IEnumerable<Line>`, matching the MXF parser's packet-based architecture. Each packet represents a PES (Packetized Elementary Stream) frame containing multiple teletext lines. This provides better consistency across container formats and enables frame-level PTS timing.
* **PTS-based timecode generation** - TS parser now extracts PTS (Presentation Time Stamp) values from PES packet headers and converts them to accurate timecodes. This provides proper audio/video synchronization for transport stream formats where data, audio, and video streams may not be perfectly aligned.

### Added

* **Automatic frame rate detection for MPEG-TS** - Implemented PTS delta analysis to automatically detect video frame rates (24, 25, 30, 48, 50, 60 fps) from transport streams. The parser analyzes PTS intervals between video frames to determine the correct timebase, similar to MediaInfo's frame rate detection.
* **Video stream tracking in TS parser** - Added PMT (Program Map Table) parsing to identify and track video stream PIDs (MPEG-1, MPEG-2, H.264, H.265) for frame rate detection.
* **PTS extraction and conversion utilities** - New methods for extracting 33-bit PTS values from PES headers and converting them to SMPTE timecodes at the correct timebase.

### Removed

* **TS.LineCount property** - Removed as the packet-based architecture makes this property obsolete. Use `packet.LineCount` to get the number of lines in a specific packet.

### Technical Details

* Added `DetectFrameRateFromVideo()` method (TS.cs) for automatic frame rate detection via PTS delta analysis
* Added `TryExtractPTS()` method (TS.cs) for extracting PTS from PES packet headers
* Added `ConvertPTSToTimecode()` method (TS.cs) for PTS-to-timecode conversion
* Modified `ProcessPMT()` (TS.cs) to track video stream PIDs
* Modified `Parse()` and `ParseAsync()` (TS.cs) to return `IEnumerable<Packet>`
* Modified `ProcessPESPacketToPacket()` (TS.cs) to group teletext data by PES packet
* Updated `Functions.cs` to iterate over `packet.Lines` for TS format
* Added video stream type constants to `Constants.cs`
* Added PTS/DTS parsing constants to `Constants.cs`

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

[unreleased]: https://github.com/nathanpbutler/libopx/compare/v2.3.0...HEAD
[2.4.0]: https://github.com/nathanpbutler/libopx/compare/v2.3.0...v2.4.0
[2.3.0]: https://github.com/nathanpbutler/libopx/compare/v2.2.0...v2.3.0
[2.2.0]: https://github.com/nathanpbutler/libopx/compare/v2.1.2...v2.2.0
[2.1.2]: https://github.com/nathanpbutler/libopx/compare/v2.1.1...v2.1.2
[2.1.1]: https://github.com/nathanpbutler/libopx/compare/v2.1.0...v2.1.1
[2.1.0]: https://github.com/nathanpbutler/libopx/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/nathanpbutler/libopx/compare/v1.4.0...v2.0.0
[1.4.0]: https://github.com/nathanpbutler/libopx/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/nathanpbutler/libopx/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/nathanpbutler/libopx/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/nathanpbutler/libopx/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/nathanpbutler/libopx/releases/tag/v1.0.0
