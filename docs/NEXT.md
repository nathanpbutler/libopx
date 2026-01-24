# libopx v3.0 Architecture Redesign

**Status:** v2.4.0 RELEASED ✅ - Phase 4 (v3.0.0 breaking changes) next
**Target Release:** v3.0.0
**Last Updated:** 2026-01-24 (v2.4.0 released with FormatIO API, FormatConverter, and deprecation warnings)

## Unified FormatIO API

**FormatIO** is the fluent API for **all teletext operations**:

- Parsing files to extract teletext data
- Filtering by magazine/rows/page number with content filtering
- Converting between formats (T42 ↔ VBI ↔ STL ↔ RCWT)
- **Extract**: Demuxing essence streams from MXF to separate files
- **Restripe**: In-place modification of MXF timecodes

Extract and Restripe are **terminal operations** that consume the FormatIO instance:

1. After calling `ExtractTo()` or `Restripe()`, further operations throw `InvalidOperationException`
2. FormatIO properly manages stream lifecycle (closes its stream before MXF opens the file)
3. Uses existing ParseOptions for MXF-specific configuration

**MXF Start Timecode**: When opening MXF files, FormatIO automatically reads the start timecode from the TimecodeComponent in the file. This ensures that after restripe operations, subsequent filter/convert operations use the correct start timecode.

```csharp
// Use FormatIO for data-flow operations
using var io = FormatIO.Open("input.mxf");
foreach (var line in io.ParseLines(magazine: 8))
{
    Console.WriteLine(line.Text);
}

// Use FormatIO for Extract operations (fluent API)
using var io = FormatIO.Open("input.mxf")
    .WithDemuxMode()
    .WithKeyNames()
    .WithVerbose();
var result = io.ExtractTo("output_base");

// Use FormatIO for Restripe operations
using var io = FormatIO.Open("input.mxf")
    .WithProgress();
io.Restripe("10:00:00:00");

// Extract specific keys
using var io = FormatIO.Open("input.mxf")
    .WithKeys(KeyType.Data, KeyType.Video);
var result = io.ExtractTo();

// Async variants
var result = await io.ExtractToAsync("output", cancellationToken);
await io.RestripeAsync("01:00:00:00", cancellationToken);
```

**Note:** MXF constructors (`new MXF(string)`, `new MXF(FileInfo)`) remain available for advanced use cases but FormatIO is now the recommended API.

## Executive Summary

This document outlines a major architecture redesign for libopx v3.0 that will:

- **Eliminate ~80% code duplication** across format classes (VBI, T42, TS, MXF, ANC)
- **Unify commands** - Single `convert` command replacing filter/extract/convert
- **Introduce abstraction layer** - IFormatHandler interface for consistent format handling
- **Centralize I/O logic** - FormatIO class managing streams, files, stdin/stdout
- **Enable composition** - Operations can now be chained (filter + convert in one pass)

**Breaking Changes:** Yes (v3.0.0)
**Migration Path:** Phased rollout from v2.2 to v3.0 with deprecation warnings

---

## 1. Current Architecture Analysis

### 1.1 Existing Structure

```plaintext
┌─────────────────────────────────────────┐
│         CLI (Program.cs)                │
│  Entry point, command setup             │
└───────────────────┬─────────────────────┘
                    │
                    ▼
┌───────────────────────────────────────────┐
│          Commands.cs                      │
│  Command definitions (System.CommandLine) │
│  - CreateFilterCommand()                  │
│  - CreateExtractCommand()                 │
│  - CreateRestripeCommand()                │
│  - CreateConvertCommand()                 │
│  Each handler calls Functions.*Async()    │
└───────────────────┬───────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│          Functions.cs                   │
│  Routing logic with switch statements:  │
│  - FilterAsync()   -> switch format     │
│  - ExtractAsync()  -> switch format     │
│  - ConvertAsync()  -> switch format     │
│  - RestripeAsync() -> switch format     │
└──────────────────────┬──────────────────┘
                       │
     ┌─────────────────┼──────┬───────────┐
     ▼           ▼            ▼           ▼
┌─────────┐ ┌─────────┐ ┌──────────┐ ┌─────────┐
│ VBI.cs  │ │ T42.cs  │ │  MXF.cs  │ │  TS.cs  │
├─────────┤ ├─────────┤ ├──────────┤ ├─────────┤
│ Parse() │ │ Parse() │ │ Parse()  │ │ Parse() │
│ ToT42() │ │ ToVBI() │ │ Extract  │ │         │
│         │ │         │ │ Restripe │ │         │
│         │ │         │ │          │ │         │
│         │ │         │ │          │ │         │
└─────────┘ └─────────┘ └──────────┘ └─────────┘

┌─────────┐
│ ANC.cs  │  (extracted from MXF, formerly MXFData)
├─────────┤
│ Parse() │
│         │
└─────────┘
```

### 1.2 Pain Points

**Code Duplication (~80%):**

- Each format class has identical: `InputFile`, `OutputFile`, `Input`, `Output`, `OutputFormat`, `Function`
- Identical constructor patterns: from file, from stdin, from stream
- Identical disposal patterns
- Identical output routing logic
- Note: `InputFormat` exists in VBI/T42/TS only; `LineCount` exists in VBI/T42 only (TS uses `FrameRate` for PTS-based timecode generation)
- **MXFData renamed to ANC:** The nested MXF.MXFData class is being extracted to a top-level ANC (Ancillary Data) class for clarity

**Scattered Conversion Logic:**

- `VBI.ToT42()` static method
- `T42.ToVBI()` static method
- Conversion logic duplicated in Parse() methods
- No central place to add new conversions

**No Abstraction:**

- No `IFormatHandler` interface
- No base class for common functionality
- Cannot treat formats polymorphically
- Difficult to add new formats

**Mixed Concerns:**

- Format classes do: I/O + parsing + filtering + conversion + output
- Violates Single Responsibility Principle
- Hard to test components independently

**Switch Statement Hell:**

- `Functions.cs` has large switch statements on Format enum
- Adding new format requires touching multiple switch statements
- Error-prone and hard to maintain

**Cannot Compose Operations:**

- Want to filter + convert? Must pipe: `opx filter ... | opx convert ...`
- Want to extract + filter MXF? Not possible
- Each command is a separate execution

### 1.3 Code Metrics

**Duplication Analysis:**

```plaintext
Common Properties Across All Format Classes (7 total):
- InputFile: FileInfo? = null
- OutputFile: FileInfo? = null
- Input: Stream (required)
- Output: Stream (lazy)
- OutputFormat: Format?
- Function: Function
- _outputStream: Stream? (private field)

Format-Specific Properties (NOT common):
- InputFormat: Format (VBI, T42, TS only - not in MXF/ANC)
- LineCount: int = 2 (VBI, T42 only - TS uses FrameRate for PTS-based timecodes)
- FrameRate: int = 25 (TS only - for PTS-to-timecode conversion)

Common Constructors (3 per class x 5 classes = 15 total):
- Constructor(string filePath)
- Constructor() [stdin]
- Constructor(Stream stream)

Common Methods:
- SetOutput(string)
- SetOutput(Stream)
- Parse() / ParseAsync()
- Dispose()

Total Lines of Duplicated Code: ~600-800 lines
Maintenance Burden: 5x (change in one place requires 5 updates)
```

### 1.3.2 TS Format Architecture Changes (v2.1.2)

**Packet-Based Architecture:**

In v2.1.2, the TS parser was significantly refactored to return `IEnumerable<Packet>` instead of `IEnumerable<Line>`, introducing PTS-based timecode generation:

**Changes:**

- **Removed:** `LineCount` property (line-based timecode incrementation)
- **Added:** `FrameRate` property for PTS-to-timecode conversion (default 25 fps)
- **Added:** Automatic packet size detection (188 vs 192-byte packets)
- **Added:** Automatic frame rate detection via PTS delta analysis
- **Added:** Video stream tracking for frame rate detection

**Why This Matters for v2.2.0:**

The TS format now has fundamentally different timecode handling:

- **VBI/T42:** Use `LineCount` to increment timecodes every N lines
- **TS:** Extracts PTS (Presentation Time Stamp) from PES packet headers and converts to SMPTE timecodes at detected/configured frame rate

This means `LineCount` cannot be in `FormatIOBase` - it's format-specific to VBI/T42.

**Impact on Phase 1:**

- FormatIOBase will have 7 common properties (not 8)
- Format-specific properties remain in child classes
- TS will keep `FrameRate`, VBI/T42 will keep `LineCount`

### 1.3.3 Current MXF Limitations

**No Direct Video VBI Extraction:**

Currently, libopx can extract VBI data from MXF ancillary data (ANC) streams but cannot extract VBI lines embedded within the video stream itself. This requires a manual preprocessing step:

```bash
# Current workaround (2-stage process)
ffmpeg -v error -i input.mxf -vf crop=720:2:0:28 -f rawvideo -pix_fmt gray - | opx filter -c
```

**Issues with Current Approach:**

- Requires FFmpeg as external dependency
- Two-process overhead (IPC, pipe buffering)
- No integration with magazine/row filtering
- Cannot combine with format conversion in single pass
- Users must understand FFmpeg video filter syntax
- Crop parameters vary by video format (PAL/NTSC)

**Target Solution (v2.4):**

Direct VBI extraction from MXF video frames using FFmpeg.AutoGen:

```bash
# Single command, integrated workflow
opx convert input.mxf --extract-vbi -m 8 -r 20,22 -o output.t42
```

---

## 2. Proposed Architecture

### 2.1 Core Components Overview

```plaintext
┌──────────────────────────────────────────┐
│           CLI (opx)                      │
│  Commands: convert, restripe             │
└────────────────────┬─────────────────────┘
                     │
                     ▼
┌──────────────────────────────────────────┐
│       FormatIO (Public API)              │
│  + Open(path)                            │
│  + Parse(magazine?, rows?)               │
│  + ConvertTo(format)                     │
│  + Filter(magazine, rows)                │
│  + Extract(keys)                         │
│  + SaveTo(path)                          │
└────────────────────┬─────────────────────┘
                     │
         ┌───────────┼───────────┐
         ▼           ▼           ▼
    ┌────────┐  ┌──────────┐ ┌──────────┐
    │ Stream │  │ Format   │ │Converter │
    │ Manager│  │ Registry │ │ Pipeline │
    └────────┘  └──────────┘ └──────────┘
                     │
                     ▼
┌──────────────────────────────────────────┐
│      IFormatHandler Interface            │
│  VBIHandler | T42Handler | TSHandler     │
│  MXFHandler | ANCHandler                 │
└──────────────────────────────────────────┘
```

### 2.2 IFormatHandler Interface

```csharp
namespace nathanbutlerDEV.libopx.Core;

/// <summary>
/// Defines the contract for format-specific parsing and conversion handlers.
/// Each format (VBI, T42, TS, MXF, etc.) implements this interface.
/// </summary>
public interface IFormatHandler
{
    /// <summary>
    /// The format this handler processes.
    /// </summary>
    Format Format { get; }

    /// <summary>
    /// Array of formats this handler can convert to.
    /// </summary>
    Format[] ValidOutputFormats { get; }

    /// <summary>
    /// Parses the input stream and returns an enumerable of lines.
    /// </summary>
    /// <param name="input">Input stream to parse</param>
    /// <param name="options">Parsing options (magazine, rows, PIDs, etc.)</param>
    /// <returns>Enumerable of parsed lines</returns>
    IEnumerable<Line> Parse(Stream input, ParseOptions options);

    /// <summary>
    /// Asynchronously parses the input stream.
    /// </summary>
    IAsyncEnumerable<Line> ParseAsync(
        Stream input,
        ParseOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if this handler can convert to the specified format.
    /// </summary>
    bool CanConvertTo(Format target);
}
```

### 2.3 ParseOptions Class

```csharp
namespace nathanbutlerDEV.libopx.Core;

/// <summary>
/// Unified options for parsing across all formats.
/// Not all options apply to all formats.
/// </summary>
public class ParseOptions
{
    // Filtering
    public int? Magazine { get; set; }
    public int[]? Rows { get; set; }

    // Timecode
    public int LineCount { get; set; } = 2;
    public Timecode? StartTimecode { get; set; }

    // Format-specific
    public int[]? PIDs { get; set; }  // TS only
    public KeyType[]? Keys { get; set; }  // MXF only
    public bool AutoDetectPIDs { get; set; } = true;  // TS

    // Output
    public Format? OutputFormat { get; set; }
    public bool KeepBlanks { get; set; }

    // Debug
    public bool Verbose { get; set; }
}
```

### 2.4 FormatIO Public API

```csharp
namespace nathanbutlerDEV.libopx;

/// <summary>
/// Unified entry point for all format I/O operations.
/// Auto-detects format and delegates to appropriate handler.
/// </summary>
public sealed class FormatIO : IDisposable
{
    private readonly Stream _input;
    private readonly IFormatHandler _handler;
    private ParseOptions _options;

    private FormatIO(Stream input, IFormatHandler handler)
    {
        _input = input;
        _handler = handler;
        _options = new ParseOptions();
    }

    /// <summary>
    /// Opens a file and auto-detects its format.
    /// </summary>
    public static FormatIO Open(string path)
    {
        var format = DetectFormat(path);
        var handler = FormatRegistry.GetHandler(format);
        var stream = File.OpenRead(path);
        return new FormatIO(stream, handler);
    }

    /// <summary>
    /// Opens stdin with specified format.
    /// </summary>
    public static FormatIO OpenStdin(Format format)
    {
        var handler = FormatRegistry.GetHandler(format);
        return new FormatIO(Console.OpenStandardInput(), handler);
    }

    /// <summary>
    /// Opens a custom stream with specified format.
    /// </summary>
    public static FormatIO Open(Stream stream, Format format)
    {
        var handler = FormatRegistry.GetHandler(format);
        return new FormatIO(stream, handler);
    }

    /// <summary>
    /// Configures parsing options.
    /// </summary>
    public FormatIO WithOptions(Action<ParseOptions> configure)
    {
        configure(_options);
        return this;
    }

    /// <summary>
    /// Parses the input stream with current options.
    /// </summary>
    public IEnumerable<Line> Parse(int? magazine = null, int[]? rows = null)
    {
        if (magazine.HasValue) _options.Magazine = magazine;
        if (rows != null) _options.Rows = rows;

        return _handler.Parse(_input, _options);
    }

    /// <summary>
    /// Async version of Parse.
    /// </summary>
    public IAsyncEnumerable<Line> ParseAsync(
        int? magazine = null,
        int[]? rows = null,
        CancellationToken cancellationToken = default)
    {
        if (magazine.HasValue) _options.Magazine = magazine;
        if (rows != null) _options.Rows = rows;

        return _handler.ParseAsync(_input, _options, cancellationToken);
    }

    /// <summary>
    /// Converts to specified format (fluent API).
    /// </summary>
    public FormatIO ConvertTo(Format target)
    {
        if (!_handler.CanConvertTo(target))
            throw new NotSupportedException($"Cannot convert {_handler.Format} to {target}");

        _options.OutputFormat = target;
        return this;
    }

    /// <summary>
    /// Applies magazine/row filtering (fluent API).
    /// </summary>
    public FormatIO Filter(int? magazine, int[]? rows = null)
    {
        _options.Magazine = magazine;
        _options.Rows = rows;
        return this;
    }

    /// <summary>
    /// Saves parsed output to file.
    /// </summary>
    public void SaveTo(string path)
    {
        using var output = File.Create(path);
        SaveTo(output);
    }

    /// <summary>
    /// Saves parsed output to stream.
    /// </summary>
    public void SaveTo(Stream output)
    {
        foreach (var line in Parse())
        {
            // Write line data based on output format
            WriteLineToStream(line, output);
        }
    }

    /// <summary>
    /// Async version of SaveTo.
    /// </summary>
    public async Task SaveToAsync(string path, CancellationToken ct = default)
    {
        using var output = File.Create(path);
        await SaveToAsync(output, ct);
    }

    public async Task SaveToAsync(Stream output, CancellationToken ct = default)
    {
        await foreach (var line in ParseAsync(cancellationToken: ct))
        {
            await WriteLineToStreamAsync(line, output, ct);
        }
    }

    public void Dispose()
    {
        _input?.Dispose();
    }

    private static Format DetectFormat(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".vbi" => Format.VBI,
            ".vbid" => Format.VBI_DOUBLE,
            ".t42" => Format.T42,
            ".ts" => Format.TS,
            ".mxf" => Format.MXF,
            ".bin" => Format.ANC,  // Formerly Format.MXFData
            _ => throw new NotSupportedException($"Unknown format: {ext}")
        };
    }

    private void WriteLineToStream(Line line, Stream output)
    {
        // Implementation based on output format
        // Delegates to FormatConverter
    }

    private async Task WriteLineToStreamAsync(Line line, Stream output, CancellationToken ct)
    {
        // Async implementation
    }
}
```

### 2.5 FormatRegistry

```csharp
namespace nathanbutlerDEV.libopx.Core;

/// <summary>
/// Central registry mapping formats to their handlers.
/// Supports extensibility for custom format handlers.
/// </summary>
public static class FormatRegistry
{
    private static readonly Dictionary<Format, Func<IFormatHandler>> _handlers = new();

    static FormatRegistry()
    {
        // Register built-in handlers
        Register(Format.VBI, () => new VBIHandler());
        Register(Format.VBI_DOUBLE, () => new VBIHandler());
        Register(Format.T42, () => new T42Handler());
        Register(Format.TS, () => new TSHandler());
        Register(Format.MXF, () => new MXFHandler());
        Register(Format.ANC, () => new ANCHandler());  // Formerly MXFDataHandler
    }

    public static void Register(Format format, Func<IFormatHandler> factory)
    {
        _handlers[format] = factory;
    }

    public static IFormatHandler GetHandler(Format format)
    {
        if (!_handlers.TryGetValue(format, out var factory))
            throw new NotSupportedException($"No handler registered for format: {format}");

        return factory();
    }

    public static bool IsRegistered(Format format) => _handlers.ContainsKey(format);
}
```

### 2.6 FormatConverter

```csharp
namespace nathanbutlerDEV.libopx.Core;

/// <summary>
/// Centralized format conversion logic.
/// Replaces scattered ToT42/ToVBI methods.
/// </summary>
public static class FormatConverter
{
    /// <summary>
    /// Converts VBI line data to T42 format.
    /// </summary>
    public static byte[] VBIToT42(byte[] vbiData)
    {
        // Logic moved from VBI.ToT42()
        if (vbiData.Length != Constants.VBI_LINE_SIZE &&
            vbiData.Length != Constants.VBI_DOUBLE_LINE_SIZE)
        {
            throw new ArgumentException($"Line data must be {Constants.VBI_LINE_SIZE} or {Constants.VBI_DOUBLE_LINE_SIZE} bytes long.");
        }

        var newLine = vbiData.Length == Constants.VBI_DOUBLE_LINE_SIZE
            ? vbiData
            : Functions.Double(vbiData);
        var normalised = Functions.Normalise(newLine);
        var bits = Functions.GetBits(normalised);
        var offset = Functions.GetOffset(bits);

        if (offset is <= -1 or >= Constants.VBI_MAX_OFFSET_RANGE)
            return new byte[Constants.T42_LINE_SIZE];

        return T42.Get(bits, offset);
    }

    /// <summary>
    /// Converts T42 line data to VBI format.
    /// </summary>
    public static byte[] T42ToVBI(byte[] t42Data, Format outputFormat = Format.VBI)
    {
        // Logic moved from T42.ToVBI()
        if (t42Data.Length != Constants.T42_LINE_SIZE)
            return new byte[outputFormat == Format.VBI_DOUBLE
                ? Constants.VBI_DOUBLE_LINE_SIZE
                : Constants.VBI_LINE_SIZE];

        byte[] lineData = [0x55, 0x55, 0x27, .. t42Data];
        BitArray bits = new(lineData);
        var bytes = new byte[Constants.VBI_BITS_SIZE];

        for (var b = 0; b < Constants.VBI_BITS_SIZE; b++)
            bytes[b] = bits[b] ? Constants.VBI_HIGH_VALUE : Constants.VBI_LOW_VALUE;

        var resized = Constants.VBI_RESIZE_BYTES;
        for (var i = 0; i < Constants.VBI_RESIZE_SIZE; i++)
        {
            var originalPosition = i * Constants.VBI_SCALE;
            var leftPixel = (int)originalPosition;
            var rightPixel = Math.Min(leftPixel + 1, Constants.VBI_BITS_SIZE - 1);
            var rightWeight = originalPosition - leftPixel;
            var leftWeight = 1f - rightWeight;
            resized[i] = (byte)(bytes[leftPixel] * leftWeight + bytes[rightPixel] * rightWeight);
        }

        resized = [.. Constants.VBI_PADDING_BYTES.Take(Constants.VBI_PAD_START)
            .Concat(resized)
            .Concat(Constants.VBI_PADDING_BYTES)
            .Take(Constants.VBI_LINE_SIZE)];

        if (outputFormat == Format.VBI_DOUBLE)
            resized = Functions.Double(resized);

        return resized;
    }

    /// <summary>
    /// Converts T42 to RCWT format.
    /// </summary>
    public static byte[] T42ToRCWT(Line line)
    {
        // Implementation from Line.ToRCWT()
        throw new NotImplementedException();
    }

    /// <summary>
    /// Converts T42 to EBU STL format.
    /// </summary>
    public static byte[] T42ToSTL(Line line)
    {
        // Implementation from Line.ToSTL()
        throw new NotImplementedException();
    }
}
```

### 2.7 FFmpeg.AutoGen Integration (CLI-only)

**Architecture Decision:** FFmpeg.AutoGen will be integrated into the `opx` CLI tool only, NOT the `libopx` library, to avoid adding native dependencies to the library.

```plaintext
┌──────────────────────────────────────────┐
│           opx CLI                        │
│  - Commands (System.CommandLine)         │
│  - FFmpeg.AutoGen wrapper (video decode) │
└───────────────────┬──────────────────────┘
                    │
         ┌──────────┴──────────┐
         ▼                     ▼
┌─────────────────┐   ┌──────────────────┐
│  libopx         │   │ FFmpeg.AutoGen   │
│  (Pure .NET)    │   │ (Native bindings)│
│  - Format I/O   │   │ - Video decode   │
│  - Parsing      │   │ - Frame access   │
│  - Conversion   │   │ - VBI extraction │
└─────────────────┘   └──────────────────┘
```

**VBI Extraction Pipeline:**

```plaintext
MXF → FFmpeg Decode → Video Frame → Extract Lines (default: 28:2) →
  Convert to VBI → Filter (mag/rows) → Convert Format → Output
```

**Key Design Points:**

1. **CLI-only dependency** - Library remains dependency-free
2. **PAL format focus** - Default extracts line 28, count 2 (matching FFmpeg workflow)
3. **Configurable extraction** - `--vbi-lines offset:count` for custom ranges
4. **Integrated filtering** - Apply magazine/row filters during extraction
5. **Format conversion** - Output as T42, VBI, or other formats
6. **Performance** - Stream processing, no full frame buffering
7. **Default behavior** - Without `--extract-vbi`, MXF extracts from ANC data stream

---

## 3. Command Unification

### 3.1 Rationale

Current commands (`filter`, `extract`, `convert`) are conceptually similar:

- **filter**: Input → Filter → Output (stdout, same format)
- **extract**: MXF → Demux streams → Output files
- **convert**: Input → Transform → Output (different format)

All three are **non-destructive transformations** with the same flow: Input → Process → Output.

**Inspiration from vhs-teletext:**
The vhs-teletext `filter` command already performs conversion with the `-o` flag, demonstrating that filtering and conversion are naturally unified operations.

**Benefits of Unification:**

1. Simpler mental model - one transformation concept
2. Operations compose naturally (filter + convert in single pass)
3. More powerful - can extract MXF and filter in one command
4. Consistent CLI - learn once, apply everywhere

### 3.2 Unified `convert` Command

**New Signature:**

```bash
opx convert [input] [options]

Options:
  -o, --output <path>        Output file path (default: stdout)
  -of, --output-format <fmt> Output format (auto-detect from extension)
  -m, --magazine <num>       Filter by magazine (1-8)
  -r, --rows <list>          Filter by rows (comma/range: 1,2,5-8)
  -l, --line-count <num>     Lines per frame (default: 2)
  -c, --caps                 Use caption rows (1-24) instead of (0-31)
  --keep                     Keep blank bytes when filtering
  -V, --verbose              Enable verbose output

  # TS-specific
  --pid <list>               MPEG-TS PIDs (comma-separated)

  # MXF-specific
  --extract <keys>           Extract keys: data,video,audio,system,timecode
  -d, --demux                Extract all keys found
  -n, --names                Use key names instead of hex
  --klv                      Include KLV headers in output

  # MXF Video VBI-specific
  --extract-vbi              Extract VBI from video frames (default: ANC data stream)
  --vbi-lines <offset:count> VBI line offset and count (default: 28:2)
```

### 3.3 Command Examples

**Filtering (replaces `opx filter`):**

```bash
# To stdout with ANSI colors
opx convert input.vbi -m 8 -r 20,22

# To file (preserves format)
opx convert input.vbi -m 8 -r 20,22 -o filtered.vbi

# TS with PID filtering
opx convert input.ts --pid 70 -m 8
```

**Format Conversion (replaces `opx convert`):**

```bash
# Auto-detect output format from extension
opx convert input.vbi -o output.t42

# Explicit output format
opx convert input.vbi -of t42 -o output.raw

# Stdin to stdout
cat input.vbi | opx convert -of t42 > output.t42
```

**Combined Filtering + Conversion:**

```bash
# Filter and convert in one pass (previously required piping)
opx convert input.vbi -m 8 -r 20,22 -o output.t42

# MXF to subtitle with filtering
opx convert input.mxf -m 8 -r 20-24 -o output.stl
```

**MXF Extraction (replaces `opx extract`):**

```bash
# Extract specific streams
opx convert input.mxf --extract data,video -o output_base

# Demux all streams
opx convert input.mxf --demux -o output_base

# Extract with key names and KLV
opx convert input.mxf --demux --names --klv -o output_base

# Extract and filter
opx convert input.mxf --extract data -m 8 -o filtered_d.raw
```

**MXF Video VBI Extraction:**

```bash
# Extract VBI from video frames to T42 (default: line 28, count 2)
opx convert input.mxf --extract-vbi -o output.t42

# Extract specific lines (e.g., line 7 with count 16 for teletext)
opx convert input.mxf --extract-vbi --vbi-lines 7:16 -o output.t42

# Extract with magazine filtering (integrated)
opx convert input.mxf --extract-vbi -m 8 -r 20,22 -o filtered.t42

# Extract to VBI format
opx convert input.mxf --extract-vbi -of vbi -o output.vbi

# Note: Without --extract-vbi, MXF defaults to ANC data stream extraction
opx convert input.mxf --extract data -o anc_data.t42

# Compare with old workaround (deprecated)
# Old: ffmpeg -i input.mxf -vf crop=720:2:0:28 -f rawvideo -pix_fmt gray - | opx filter -c
# New: opx convert input.mxf --extract-vbi
```

### 3.4 Backward Compatibility Strategy

**Phase 1 (v2.5):** Add `convert` command alongside existing commands

```bash
# Old commands still work (with deprecation warning)
opx filter -m 8 input.vbi
# Warning: 'filter' is deprecated, use 'convert' instead

# New command available
opx convert -m 8 input.vbi
```

**Phase 2 (v3.0):** Remove old commands

```bash
# Old commands removed
opx filter ...
# Error: Unknown command 'filter'. Did you mean 'convert'?

# Only new command works
opx convert -m 8 input.vbi
```

### 3.5 Migration Table

| Old Command | New Command |
| ----------- | ----------- |
| `opx filter -m 8 input.vbi` | `opx convert -m 8 input.vbi` |
| `opx filter -m 8 input.vbi > output.txt` | `opx convert -m 8 input.vbi -o output.txt` |
| `opx convert input.vbi output.t42` | `opx convert input.vbi -o output.t42` |
| `opx extract -k d,v input.mxf` | `opx convert input.mxf --extract data,video -o output_base` |
| `opx extract -d -n input.mxf` | `opx convert input.mxf --demux --names -o output_base` |
| `opx filter input.ts --pid 70` | `opx convert input.ts --pid 70` |

---

## 4. Implementation Phases

### Overview: Consolidated Release Strategy

To avoid flooding NuGet with rapid incremental releases, phases are bundled into meaningful milestones:

- **v2.2.0** - Phases 1 + 2: Internal foundation (FormatIOBase + IFormatHandler abstractions)
- **v2.4.0** - Phase 3: New API available with deprecation warnings
- **v3.0.0** - Phase 4: Breaking changes and unified CLI

---

### Phase 1: Extract Common I/O (v2.2.0) ✅ COMPLETE

**Goal:** Internal refactoring without breaking changes.

**Tasks:**

1. ✅ Create `FormatIOBase` abstract class
2. ✅ Extract common properties (InputFile, Input, Output, etc.)
3. ✅ Extract common methods (SetOutput, disposal patterns)
4. ✅ Refactor VBI, T42, TS, MXF to inherit from FormatIOBase
5. ✅ Extract MXFData to top-level ANC class
6. ✅ Remove orphaned AsyncProcessingHelpers class (288 lines removed)
7. ✅ Update all tests to ensure no regressions

**Deliverables:**

- [x] `lib/Core/FormatIOBase.cs` (created with conditional disposal pattern)
- [x] Updated format classes (VBI, T42, TS, MXF inherit from base)
- [x] `lib/Formats/ANC.cs` (extracted from nested MXF.MXFData)
- [x] Updated tests (MemoryBenchmarkTests.cs, +30 new unit tests)
- [x] All existing tests pass (33/33 passing)
- [x] Cleaned up `lib/AsyncHelpers.cs` (kept ProgressReporter, removed unused wrappers)
- [x] No public API changes (maintained backward compatibility)

**Code Example:**

```csharp
// lib/Core/FormatIOBase.cs
public abstract class FormatIOBase : IDisposable
{
    // Common I/O properties (7 total)
    public FileInfo? InputFile { get; set; }
    public FileInfo? OutputFile { get; set; }
    private Stream? _outputStream;
    public required Stream Input { get; set; }
    public Stream Output => _outputStream ??= OutputFile == null
        ? Console.OpenStandardOutput()
        : OutputFile.Open(FileMode.Create, FileAccess.Write, FileShare.Read);

    public Format? OutputFormat { get; set; }
    public Function Function { get; set; } = Function.Filter;

    // Note: InputFormat, LineCount, FrameRate are format-specific
    // and remain in child classes (VBI, T42, TS, etc.)

    public void SetOutput(string outputFile)
    {
        OutputFile = new FileInfo(outputFile);
    }

    public void SetOutput(Stream outputStream)
    {
        OutputFile = null;
        _outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
        if (InputFile != null)
            Input?.Dispose();
        if (OutputFile != null)
            _outputStream?.Dispose();
    }
}

// lib/Formats/VBI.cs (updated)
public class VBI : FormatIOBase
{
    // Format-specific properties (not in base class)
    public Format InputFormat { get; set; } = Format.VBI;
    public int LineCount { get; set; } = 2;
    public int LineLength => InputFormat == Format.VBI_DOUBLE
        ? Constants.VBI_DOUBLE_LINE_SIZE
        : Constants.VBI_LINE_SIZE;
    public static readonly Format[] ValidOutputs = [Format.VBI, Format.VBI_DOUBLE, Format.T42, Format.RCWT, Format.STL];

    [SetsRequiredMembers]
    public VBI(string inputFile, Format? vbiType = Format.VBI)
    {
        InputFile = new FileInfo(inputFile);
        if (!InputFile.Exists)
            throw new FileNotFoundException("The specified VBI file does not exist.", inputFile);

        InputFormat = vbiType ?? (InputFile.Extension.ToLower() switch
        {
            ".vbi" => Format.VBI,
            ".vbid" => Format.VBI_DOUBLE,
            _ => Format.VBI
        });

        Input = InputFile.OpenRead();
    }

    // ... rest of VBI-specific implementation
}

// lib/Formats/TS.cs (updated)
public class TS : FormatIOBase
{
    // Format-specific properties (not in base class)
    public Format InputFormat { get; set; } = Format.TS;
    public int FrameRate { get; set; } = 25;  // For PTS-to-timecode conversion
    public int[]? PIDs { get; set; } = null;
    public bool AutoDetect { get; set; } = true;
    public bool Verbose { get; set; } = false;
    public static readonly Format[] ValidOutputs = [Format.T42, Format.VBI, Format.VBI_DOUBLE, Format.RCWT, Format.STL];

    // Note: TS uses FrameRate instead of LineCount because it extracts PTS
    // (Presentation Time Stamp) from PES packets for accurate timecodes

    // ... rest of TS-specific implementation
}
```

**Success Criteria:**

- [x] ~300-400 lines of code removed (duplicated I/O properties/methods) ✅
  - **Result:** ~600 lines total eliminated
    - ~300 lines from SetOutput(), Dispose(), property declarations
    - ~288 lines from removing orphaned AsyncProcessingHelpers class
- [x] All 5 format classes inherit from FormatIOBase (VBI, T42, TS, MXF, ANC) ✅
- [x] 100% test coverage maintained - 33/33 tests passing (+30 new tests) ✅
- [x] No breaking changes to public API ✅
- [x] Format-specific properties (InputFormat, LineCount, FrameRate) remain in child classes ✅

**Technical Debt Cleanup:**

During Phase 1, the orphaned `AsyncProcessingHelpers` class was identified and removed from `lib/AsyncHelpers.cs`:

- **Removed:** 288 lines of unused async wrapper methods (ProcessVBIAsync, ProcessT42Async, ProcessMXFDataAsync, ProcessMXFAsync, ProcessExtractAsync, ProcessConvertAsync, ProcessLargeVBIExample)
- **Why:** These methods became redundant after `Functions.cs` was enhanced with `FilterAsync()`, `ExtractAsync()`, `RestripeAsync()`, and `ConvertAsync()` methods in v1.5.0
- **Kept:** `ProgressReporter` utility class (80 lines) - still useful for progress reporting in long-running operations
- **Benefit:** Eliminated duplicate functionality, simplified maintenance, and clarified the single path for async operations (via `Functions.cs`)

---

### Phase 2: Define Abstractions (v2.2.0) ✅ COMPLETE

**Goal:** Introduce interfaces while maintaining backward compatibility.

**Note:** Combined with Phase 1 into v2.2.0 to deliver a complete internal foundation in one release.

**Status:** 100% Complete - All 5 format handlers implemented (T42, VBI, ANC, TS, MXF)

**Completed Tasks:**

1. ✅ Define `IFormatHandler` interface for Line-based formats
2. ✅ Define `IPacketFormatHandler` interface for Packet-based formats
3. ✅ Create `FormatRegistry` class
4. ✅ Extend `ParseOptions` with StartTimecode and PIDs
5. ✅ Implement `T42Handler`, `VBIHandler`, `ANCHandler`
6. ✅ Create adapter layer for T42, VBI, ANC classes
7. ✅ Add tests for new infrastructure (33 new tests)
8. ✅ Implement `TSHandler` (refactored ~1070 lines of stateful logic)
9. ✅ Update TS.cs to delegate to TSHandler
10. ✅ Implement `MXFHandler` (refactored ~1600 lines of complex logic)
11. ✅ Update MXF.cs to delegate to MXFHandler
12. ✅ All existing tests passing (66/66)

**Future Work (Handler-Specific Tests):**

- [ ] Add tests for TSHandler (awaiting TS test files from user)
- [ ] Add tests for MXFHandler (awaiting MXF test files from user)

**Deliverables:**

- [x] `lib/Core/IFormatHandler.cs` ✅
- [x] `lib/Core/IPacketFormatHandler.cs` ✅
- [x] `lib/Core/FormatRegistry.cs` ✅
- [x] `lib/Core/ParseOptions.cs` ✅ (extended with StartTimecode, PIDs)
- [x] `lib/Handlers/T42Handler.cs` ✅
- [x] `lib/Handlers/VBIHandler.cs` ✅
- [x] `lib/Handlers/ANCHandler.cs` ✅
- [x] `lib/Handlers/TSHandler.cs` ✅
- [x] `lib/Handlers/MXFHandler.cs` ✅
- [x] Updated `T42.cs`, `VBI.cs`, `ANC.cs`, `TS.cs`, `MXF.cs` to use handlers internally ✅
- [x] Tests for new components (33 tests, 66/66 total passing) ✅

**Code Example:**

```csharp
// lib/Handlers/T42Handler.cs
public class T42Handler : IFormatHandler
{
    public Format Format => Format.T42;
    public Format[] ValidOutputFormats => [Format.T42, Format.VBI, Format.VBI_DOUBLE, Format.RCWT, Format.STL];

    public IEnumerable<Line> Parse(Stream input, ParseOptions options)
    {
        var lineNumber = 0;
        var timecode = options.StartTimecode ?? new Timecode(0);
        var t42Buffer = new byte[Constants.T42_LINE_SIZE];

        while (input.Read(t42Buffer, 0, Constants.T42_LINE_SIZE) == Constants.T42_LINE_SIZE)
        {
            if (lineNumber % options.LineCount == 0 && lineNumber != 0)
                timecode = timecode.GetNext();

            var line = new Line
            {
                LineNumber = lineNumber,
                Data = [.. t42Buffer],
                Length = Constants.T42_LINE_SIZE,
                SampleCoding = 0x31,
                SampleCount = Constants.T42_LINE_SIZE,
                LineTimecode = timecode,
            };

            line.SetCachedType(Format.T42);

            if (t42Buffer.Length >= Constants.T42_LINE_SIZE && t42Buffer.Any(b => b != 0))
            {
                line.Magazine = T42.GetMagazine(t42Buffer[0]);
                line.Row = T42.GetRow([.. t42Buffer.Take(2)]);
                line.Text = T42.GetText([.. t42Buffer.Skip(2)], line.Row == 0);
            }
            else
            {
                lineNumber++;
                continue;
            }

            // Apply filtering
            if (options.Magazine.HasValue && line.Magazine != options.Magazine.Value)
            {
                lineNumber++;
                continue;
            }

            if (options.Rows != null && !options.Rows.Contains(line.Row))
            {
                lineNumber++;
                continue;
            }

            // Apply format conversion if needed
            if (options.OutputFormat.HasValue && options.OutputFormat != Format.T42)
            {
                line = ConvertLine(line, options.OutputFormat.Value);
            }

            yield return line;
            lineNumber++;
        }
    }

    public async IAsyncEnumerable<Line> ParseAsync(
        Stream input,
        ParseOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Similar to Parse() but async
        throw new NotImplementedException();
    }

    public bool CanConvertTo(Format target)
    {
        return ValidOutputFormats.Contains(target);
    }

    private Line ConvertLine(Line line, Format targetFormat)
    {
        // Delegate to FormatConverter
        throw new NotImplementedException();
    }
}

// lib/Formats/T42.cs (updated to use handler)
public class T42 : FormatIOBase
{
    private readonly T42Handler _handler = new();

    public IEnumerable<Line> Parse(int? magazine = null, int[]? rows = null)
    {
        var options = new ParseOptions
        {
            Magazine = magazine,
            Rows = rows ?? Constants.DEFAULT_ROWS,
            LineCount = LineCount,
            OutputFormat = OutputFormat
        };

        return _handler.Parse(Input, options);
    }

    // ... rest remains unchanged for now
}
```

**Success Criteria:**

- [x] IFormatHandler interface fully tested ✅
- [x] IPacketFormatHandler interface created ✅
- [x] FormatRegistry can register/retrieve handlers ✅
- [x] All 5 format handlers implemented (T42, VBI, ANC, TS, MXF) ✅
- [x] All 5 format classes delegate to handlers ✅
- [x] All existing tests still pass - 66/66 tests passing ✅
- [x] No breaking changes to public API ✅
- [x] All format handlers complete and consistent ✅
- [x] Phase 2 complete - v2.2.0 ready for release ✅

---

### Phase 3: Centralize Conversions & New API (v2.4.0) ✅ COMPLETE

**Goal:** Move all format conversion logic to FormatConverter + introduce new FormatIO public API.

**Note:** v2.3.0 released with STL export support. Phase 3 combines conversion centralization with new API introduction. FFmpeg.AutoGen integration deferred to v3.0.0.

**Tasks:**

1. ✅ Create `FormatConverter` static class
2. ✅ Move `VBI.ToT42()` → `FormatConverter.VBIToT42()`
3. ✅ Move `T42.ToVBI()` → `FormatConverter.T42ToVBI()`
4. ✅ Move `Line.ToRCWT()` → `FormatConverter.T42ToRCWT()`
5. ✅ Move `Line.ToSTL()` → `FormatConverter.T42ToSTL()`
6. ✅ Update all format handlers to use FormatConverter
7. ✅ Add `[Obsolete]` attributes to old methods
8. ✅ Update documentation
9. ✅ Implement complete `FormatIO` public API class with fluent API
10. ✅ Implement `Open()`, `OpenStdin()`, `Parse()`, `ConvertTo()`, `SaveTo()` methods
11. ✅ Add async variants for all operations
12. ✅ Make old API available alongside new API (both work simultaneously)
13. ✅ Add deprecation warnings to guide users toward new API
14. ✅ Consolidate duplicate `EncodeTimecodeToSTL` (Line.cs:681-692 + STLExporter.cs:517-526) → shared location
15. ✅ Consolidate duplicate `ExtractSTLTextData` (Line.cs:700-776 + STLExporter.cs:532-588) → single implementation
16. Refactor `WriteSTLHeaderAsync` in Functions.cs to use `STLExporter.CreateGSIHeader()` (deferred)
17. ✅ Add FormatRegistry static constructor for automatic handler registration
18. ✅ Migrate `Functions.Filter()` to use FormatIO API
19. ✅ Migrate `Functions.FilterAsync()` to use FormatIO API
20. ✅ Migrate `Functions.Convert()` to use FormatIO API
21. ✅ Migrate `Functions.ConvertAsync()` to use FormatIO API
22. ✅ Migrate `Functions.Extract()` to use FormatIO API
23. ✅ Migrate `Functions.Restripe()` to use FormatIO API
24. ✅ Add Extract/Restripe as first-class FormatIO terminal operations
25. ✅ Add MXF fluent configuration methods (WithDemuxMode, WithKeyNames, etc.)
26. ✅ FormatIO reads MXF start timecode from file
27. ✅ Delete FormatIOExtensions.cs (functionality moved to FormatIO)
28. ✅ Remove dead ConvertToSTLAsync code (~180 lines)

**Deliverables:**

- [x] `lib/Core/FormatConverter.cs` ✅
- [x] `lib/FormatIO.cs` (new public API) ✅
- [x] `lib/Core/FormatRegistry.cs` (auto-registration) ✅
- [x] `tests/FormatIOTests.cs` (84 comprehensive tests) ✅
- [x] Deprecated methods in VBI, T42, Line classes ✅
- [x] Deprecated constructors in VBI, T42, ANC, TS classes (NOT MXF - needed for Extract/Restripe) ✅
- [x] Updated handlers to use FormatConverter ✅
- [x] Migrated `Functions.Filter()`, `FilterAsync()`, `Convert()`, `ConvertAsync()` to FormatIO ✅
- [x] Migration guide in docs (CHANGELOG.md updated) ✅
- [x] All tests updated (317/317 passing) ✅
- [x] Deleted FormatIOExtensions.cs and ExtractResult.cs (Extract/Restripe use MXF directly) ✅
- [ ] `opx/Core/VideoVBIExtractor.cs`
- [ ] Updated Commands.cs with `--extract-vbi` option
- [ ] Integration tests with MXF video files
- [ ] Documentation in docs/NEXT.md
- [ ] Example usage in README.md
- [ ] Side-by-side API comparison examples (old vs new)

**Code Example:**

```csharp
// lib/Formats/VBI.cs (deprecated method)
[Obsolete("Use FormatConverter.VBIToT42() instead. This method will be removed in v3.0.")]
public static byte[] ToT42(byte[] lineData, bool debug = false)
{
    return FormatConverter.VBIToT42(lineData);
}

// lib/Formats/T42.cs (deprecated method)
[Obsolete("Use FormatConverter.T42ToVBI() instead. This method will be removed in v3.0.")]
public static byte[] ToVBI(byte[] t42bytes, Format outputFormat = Format.VBI)
{
    return FormatConverter.T42ToVBI(t42bytes, outputFormat);
}
```

**Deprecation Messages:**

```csharp
// Compile-time warnings
warning CS0618: 'VBI.ToT42(byte[], bool)' is obsolete:
  'Use FormatConverter.VBIToT42() instead. This method will be removed in v3.0.'
```

**Success Criteria:**

- [x] All conversion logic in one place (FormatConverter) ✅
- [x] New FormatIO API fully functional alongside old API ✅
- [x] Old methods and constructors still work but show warnings ✅
- [x] All handlers use FormatConverter ✅
- [x] Documentation updated with migration examples ✅
- [x] All tests passing (317/317) ✅
- [x] MXF constructors NOT deprecated (available for advanced use cases) ✅
- [x] Extract/Restripe migrated to FormatIO as first-class terminal operations ✅
- [x] FormatIO reads MXF start timecode from file ✅
- [x] FormatIOExtensions.cs deleted (functionality in FormatIO) ✅
- [x] Users have clear migration path from old to new API ✅
- [ ] FFmpeg.AutoGen integration (deferred to v3.0.0)

---

### Phase 4: Breaking Changes & Unified CLI (v3.0.0) ⚠️ BREAKING

**Goal:** Remove deprecated code, unify CLI commands, add FFmpeg integration.

**Note:** v2.5.0 skipped - going directly from v2.4.0 to v3.0.0 after sufficient migration period (2-3 months).

**Tasks:**

1. FFmpeg.AutoGen integration for MXF video VBI extraction (CLI-only)
2. Remove old `filter` command from CLI
3. Remove old `extract` command from CLI
4. Remove old separate `convert` command (replace with unified version)
5. Create new unified `convert` command in CLI
6. Remove deprecated methods from format classes (VBI.ToT42(), T42.ToVBI(), etc.)
7. Remove backward compatibility wrappers
8. Update all documentation to reflect breaking changes
9. Finalize migration guide with examples
10. Update README with v3.0 examples
11. Write blog post / changelog announcement
12. Reduce sync/async code duplication in Functions.cs (~800 lines) - extract common logic into helper methods
13. Create `HandleException()` helper in Functions.cs to consolidate repeated try/catch patterns (~100 lines)
14. Remove commented-out code blocks in Functions.cs (lines 1069-1078, 1095-1104, 1119-1128, 1169-1178)

**Deliverables:**

- [ ] FFmpeg.AutoGen integration for MXF video VBI extraction
- [ ] New unified `convert` command in CLI (replaces filter/extract/convert)
- [ ] Removed old commands (`filter`, `extract`, old `convert`)
- [ ] Removed deprecated code (VBI.ToT42(), T42.ToVBI(), etc.)
- [ ] Updated README.md with v3.0 examples only
- [ ] Complete `docs/MIGRATION.md` guide
- [ ] `CHANGELOG.md` v3.0 entry with breaking changes list
- [ ] Blog post / announcement draft

**Breaking Changes:**

```csharp
// Old API (removed in v3.0)
var vbi = new VBI("input.vbi");
foreach (var line in vbi.Parse(magazine: 8)) { ... }

// New API (v3.0)
using var io = FormatIO.Open("input.vbi");
foreach (var line in io.Parse(magazine: 8)) { ... }
```

**CLI Breaking Changes:**

```bash
# Old commands (removed in v3.0)
opx filter -m 8 input.vbi
opx extract -k d,v input.mxf
opx convert input.vbi output.t42

# New unified command (v3.0)
opx convert -m 8 input.vbi
opx convert input.mxf --extract data,video -o output_base
opx convert input.vbi -o output.t42
```

**Success Criteria:**

- [ ] FormatIO API fully functional
- [ ] All format handlers complete
- [ ] New `convert` command works for all use cases
- [ ] Old commands removed
- [ ] Deprecated code removed
- [ ] 100% test coverage
- [ ] Documentation complete
- [ ] Performance benchmarks show <5% regression

---

## 5. API Examples

### 5.1 Library API (v3.0)

**Example 1: Simple Filtering:**

```csharp
using nathanbutlerDEV.libopx;

// Open file (auto-detect format)
using var io = FormatIO.Open("input.vbi");

// Parse with filtering
foreach (var line in io.Parse(magazine: 8, rows: [20, 22]))
{
    Console.WriteLine(line.Text);
}
```

**Example 2: Format Conversion:**

```csharp
// Open VBI file
using var io = FormatIO.Open("input.vbi");

// Convert to T42 and save
io.ConvertTo(Format.T42).SaveTo("output.t42");
```

**Example 3: Filter + Convert:**

```csharp
// Open and configure
using var io = FormatIO.Open("input.vbi");

// Filter and convert in one pass
await io
    .Filter(magazine: 8, rows: [20, 22])
    .ConvertTo(Format.T42)
    .SaveToAsync("output.t42");
```

**Example 4: TS with PID Filtering:**

```csharp
using var io = FormatIO.Open("input.ts");

// Configure TS-specific options
io.WithOptions(opts => {
    opts.PIDs = [70, 71];
    opts.AutoDetectPIDs = false;
    opts.Verbose = true;
});

// Parse and process
foreach (var line in io.Parse(magazine: 8))
{
    Console.WriteLine(line.Text);
}
```

**Example 5: MXF Extraction:**

```csharp
using var io = FormatIO.Open("input.mxf");

// Extract specific streams
io.WithOptions(opts => {
    opts.Keys = [KeyType.Data, KeyType.Video];
    opts.OutputFormat = Format.T42;
});

await io.SaveToAsync("output_d.raw");
```

**Example 6: Stdin/Stdout:**

```csharp
// Read from stdin, write to stdout
using var io = FormatIO.OpenStdin(Format.VBI);

foreach (var line in io.Parse(magazine: 8))
{
    Console.WriteLine(line);
}
```

**Example 7: Custom Stream:**

```csharp
using var inputStream = new MemoryStream(vbiData);
using var outputStream = new MemoryStream();

using var io = FormatIO.Open(inputStream, Format.VBI);
io.ConvertTo(Format.T42).SaveTo(outputStream);

var t42Data = outputStream.ToArray();
```

### 5.2 CLI Examples (v3.0)

**Filtering:**

```bash
# To stdout with ANSI colors (current: opx filter)
opx convert input.vbi -m 8 -r 20,22

# To file
opx convert input.vbi -m 8 -r 20,22 -o filtered.vbi

# TS with PID filtering
opx convert input.ts --pid 70 -m 8 -V
```

**Format Conversion:**

```bash
# Auto-detect output format from extension (current: opx convert)
opx convert input.vbi -o output.t42

# Explicit output format
opx convert input.vbi -of t42 -o output.raw

# VBI to EBU STL subtitle
opx convert input.vbi -of stl -o output.stl
```

**Combined Operations:**

```bash
# Filter + convert in one pass (previously required piping)
opx convert input.vbi -m 8 -r 20,22 -o output.t42

# MXF to subtitle with filtering
opx convert input.mxf -m 8 -r 20-24 -of stl -o output.stl

# TS to VBI with PID and magazine filtering
opx convert input.ts --pid 70 -m 8 -of vbi -o output.vbi
```

**MXF Extraction:**

```bash
# Extract specific streams (current: opx extract)
opx convert input.mxf --extract data,video -o output_base

# Demux all keys
opx convert input.mxf --demux -o output_base

# Extract with key names and KLV headers
opx convert input.mxf --demux --names --klv -o output_base

# Extract and filter
opx convert input.mxf --extract data -m 8 -r 20,22 -o filtered_d.t42
```

**MXF Video VBI Extraction:**

```bash
# Extract VBI from video frames to T42 (default: line 28, count 2)
opx convert archive.mxf --extract-vbi -o teletext.t42

# Extract specific magazine and rows
opx convert archive.mxf --extract-vbi -m 8 -r 20-24 -o subtitles.t42

# Extract to EBU STL subtitle format
opx convert archive.mxf --extract-vbi -m 8 -of stl -o subtitles.stl

# Extract specific VBI line range (e.g., line 7 with 16 lines for full teletext)
opx convert archive.mxf --extract-vbi --vbi-lines 7:16 -o teletext_full.t42

# Compare ANC vs video frame extraction
opx convert archive.mxf --extract data -o anc_vbi.t42
opx convert archive.mxf --extract-vbi -o video_vbi.t42
diff anc_vbi.t42 video_vbi.t42

# Automated batch processing
for file in archive/*.mxf; do
  opx convert "$file" --extract-vbi -m 8 -of stl -o "subtitles/$(basename "$file" .mxf).stl"
done
```

**Piping:**

```bash
# Still works as before
cat input.vbi | opx convert -m 8 -of t42 > output.t42

# Chain with other tools
opx convert input.mxf --extract data -m 8 | mpv -

# Multiple stages
opx convert input.ts --pid 70 | opx convert -of vbi | opx restripe -t 10:00:00:00
```

---

## 6. Migration Guide

### 6.1 Library Migration

**Scenario 1: Basic Parsing:**

```csharp
// Old (v2.x)
var vbi = new VBI("input.vbi");
foreach (var line in vbi.Parse(magazine: 8, rows: new[] { 20, 22 }))
{
    Console.WriteLine(line.Text);
}

// New (v3.0)
using var io = FormatIO.Open("input.vbi");
foreach (var line in io.Parse(magazine: 8, rows: [20, 22]))
{
    Console.WriteLine(line.Text);
}
```

**Scenario 2: Format Conversion:**

```csharp
// Old (v2.x)
var vbi = new VBI("input.vbi");
vbi.OutputFormat = Format.T42;
vbi.SetOutput("output.t42");
// Manual writing loop...

// New (v3.0)
using var io = FormatIO.Open("input.vbi");
io.ConvertTo(Format.T42).SaveTo("output.t42");
```

**Scenario 3: Async Parsing:**

```csharp
// Old (v2.x)
var vbi = new VBI("input.vbi");
await foreach (var line in vbi.ParseAsync(magazine: 8, cancellationToken: ct))
{
    await ProcessLineAsync(line);
}

// New (v3.0)
using var io = FormatIO.Open("input.vbi");
await foreach (var line in io.ParseAsync(magazine: 8, cancellationToken: ct))
{
    await ProcessLineAsync(line);
}
```

**Scenario 4: Stdin/Stdout:**

```csharp
// Old (v2.x)
var vbi = new VBI(); // stdin
foreach (var line in vbi.Parse(magazine: 8))
{
    Console.WriteLine(line);
}

// New (v3.0)
using var io = FormatIO.OpenStdin(Format.VBI);
foreach (var line in io.Parse(magazine: 8))
{
    Console.WriteLine(line);
}
```

**Scenario 5: MXF Extraction:**

```csharp
// Old (v2.x)
var mxf = new MXF("input.mxf");
mxf.AddRequiredKey(KeyType.Data);
mxf.AddRequiredKey(KeyType.Video);
mxf.DemuxMode = true;
mxf.UseKeyNames = true;
mxf.ExtractEssence("output_base");

// New (v3.0)
using var io = FormatIO.Open("input.mxf");
io.WithOptions(opts => {
    opts.Keys = [KeyType.Data, KeyType.Video];
})
.SaveTo("output_base_d.raw");
```

**Scenario 6: TS with PIDs:**

```csharp
// Old (v2.x)
var ts = new TS("input.ts");
ts.PIDs = new[] { 70, 71 };
ts.Verbose = true;
foreach (var line in ts.Parse(magazine: 8))
{
    Console.WriteLine(line);
}

// New (v3.0)
using var io = FormatIO.Open("input.ts");
io.WithOptions(opts => {
    opts.PIDs = [70, 71];
    opts.Verbose = true;
});
foreach (var line in io.Parse(magazine: 8))
{
    Console.WriteLine(line);
}
```

### 6.2 CLI Migration

```bash
# Filtering
# Old: opx filter -m 8 -r 20,22 input.vbi
# New: opx convert input.vbi -m 8 -r 20,22

# Conversion
# Old: opx convert input.vbi output.t42
# New: opx convert input.vbi -o output.t42

# Extraction
# Old: opx extract -k d,v input.mxf
# New: opx convert input.mxf --extract data,video -o output_base

# Demux with names
# Old: opx extract -d -n input.mxf
# New: opx convert input.mxf --demux --names -o output_base

# TS filtering
# Old: opx filter --pid 70 input.ts
# New: opx convert input.ts --pid 70

# Piping (still works)
# Both: cat input.vbi | opx convert -m 8 -of t42 > output.t42
```

### 6.3 Deprecation Timeline (Consolidated)

| Version | Status | Available Commands | Library API |
| ------- | ------ | ----------------- | ----------- |
| v2.1 | Stable | filter, extract, convert, restripe | VBI, T42, TS, MXF classes |
| v2.2 | Complete ✅ | filter, extract, convert, restripe | Same + IFormatHandler, FormatRegistry |
| v2.3 (current) | STL export | filter, extract, convert, restripe | Same + STLExporter |
| v2.4 | New API + deprecation warnings | filter⚠️, extract⚠️, convert⚠️, restripe | Old API⚠️ + **FormatIO (new)** |
| **v3.0** | **Breaking** | **convert (unified)**, restripe | **FormatIO only** |

**Key:**

- ⚠️ = Shows deprecation warnings
- **Bold** = Recommended/new approach
- v2.5.0 skipped to avoid rapid release churn

---

## 7. Testing Strategy

### 7.1 Test Coverage Goals

- **Unit Tests:** >85% coverage
- **Integration Tests:** All format combinations
- **Performance Tests:** <5% regression tolerance
- **CLI Tests:** All command variations

### 7.2 Test Categories

**Phase 1-3 (v2.2-2.4):**

- [ ] All existing tests must pass unchanged
- [ ] Add tests for new components (FormatIOBase, IFormatHandler, FormatConverter)
- [ ] Integration tests for old API + new API working together
- [ ] Performance benchmarks (establish baseline)

**Phase 4 (v3.0):**

- [ ] Update all tests to new API
- [ ] Remove tests for deprecated code
- [ ] Integration tests for unified `convert` command
- [ ] End-to-end tests for all format pairs
- [ ] Performance validation (compare to baseline)

### 7.3 Sample Tests

**Unit Test Example:**

```csharp
[Fact]
public void FormatIO_Open_AutoDetectsVBI()
{
    // Arrange
    var testFile = "samples/sample.vbi";

    // Act
    using var io = FormatIO.Open(testFile);

    // Assert
    Assert.Equal(Format.VBI, io.InputFormat);
}

[Fact]
public void FormatRegistry_GetHandler_ReturnsVBIHandler()
{
    // Act
    var handler = FormatRegistry.GetHandler(Format.VBI);

    // Assert
    Assert.NotNull(handler);
    Assert.Equal(Format.VBI, handler.Format);
    Assert.IsAssignableFrom<VBIHandler>(handler);
}
```

**Integration Test Example:**

```csharp
[Fact]
public async Task FormatIO_ConvertAsync_VBIToT42()
{
    // Arrange
    var inputPath = "samples/sample.vbi";
    var outputPath = Path.GetTempFileName();

    try
    {
        // Act
        using var io = FormatIO.Open(inputPath);
        await io.ConvertTo(Format.T42).SaveToAsync(outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));

        using var t42 = FormatIO.Open(outputPath);
        var lines = await t42.ParseAsync().ToListAsync();
        Assert.True(lines.Count > 0);
        Assert.All(lines, line => Assert.Equal(Constants.T42_LINE_SIZE, line.Length));
    }
    finally
    {
        File.Delete(outputPath);
    }
}
```

**CLI Test Example:**

```csharp
[Fact]
public async Task CLI_Convert_FiltersMagazine()
{
    // Arrange
    var inputPath = "samples/sample.vbi";
    var args = new[] { "convert", inputPath, "-m", "8" };

    // Act
    var output = new StringWriter();
    Console.SetOut(output);
    var exitCode = await Program.Main(args);

    // Assert
    Assert.Equal(0, exitCode);
    var result = output.ToString();
    Assert.Contains("Magazine 8", result);
}
```

**Performance Benchmark:**

```csharp
[Benchmark]
public void VBI_Parse_Performance()
{
    // v2.1 baseline
    using var vbi = new VBI("samples/large.vbi");
    foreach (var line in vbi.Parse())
    {
        // Process line
    }
}

[Benchmark]
public void FormatIO_Parse_Performance()
{
    // v3.0 new implementation
    using var io = FormatIO.Open("samples/large.vbi");
    foreach (var line in io.Parse())
    {
        // Process line
    }
}

// Goal: New implementation should be within 5% of old
```

### 7.4 Test Execution Plan

**Continuous Integration:**

- Run all tests on every commit
- Fail build if any test fails
- Fail build if coverage drops below 85%

**Pre-Release:**

- Run full test suite
- Run performance benchmarks
- Manual testing of CLI commands
- Test migration path (v2.x to v3.0)

---

## 8. Performance Considerations

### 8.1 Optimization Goals

1. **Zero-allocation hot paths** - Use `Span<T>`, `Memory<T>`, `ArrayPool<T>`
2. **Async streaming** - Large files processed without loading into memory
3. **Lazy evaluation** - Only process what's needed
4. **No unnecessary copies** - Pass by reference where possible

### 8.2 Memory Management

**Current Issues (v2.x):**

- Format classes allocate arrays for every line
- No buffer pooling
- Excessive string allocations in text parsing

**Improvements (v3.0):**

```csharp
// Use ArrayPool for buffers
var arrayPool = ArrayPool<byte>.Shared;
var buffer = arrayPool.Rent(Constants.VBI_LINE_SIZE);
try
{
    // Use buffer
}
finally
{
    arrayPool.Return(buffer);
}

// Use Span<T> to avoid allocations
public void ProcessLine(Span<byte> lineData)
{
    // Process in-place, no allocation
}

// Lazy enumeration
public IEnumerable<Line> Parse()
{
    // Yield return - only process when consumed
    yield return line;
}
```

### 8.3 Benchmarking Plan

**Baseline (v2.1):**

- Parse 10MB VBI file
- Convert VBI to T42
- Filter MXF by magazine
- Extract MXF streams

**Target (v3.0):**

- Same operations d 5% slower
- Memory usage d 10% increase
- No memory leaks

**Tools:**

- BenchmarkDotNet for microbenchmarks
- dotMemory for memory profiling
- PerfView for allocation tracking

---

## 9. Open Questions & Decisions

### 9.1 Questions to Resolve

- [ ] **Q1:** Should FormatIO implement IDisposable or return disposable handles?
  - **Decision:** FormatIO implements IDisposable (manages stream lifetime)

- [ ] **Q2:** How to handle format detection failures?
  - **Option A:** Throw exception (fail fast)
  - **Option B:** Return error code
  - **Option C:** Try multiple formats
  - **Decision:** TBD

- [ ] **Q3:** Support for custom IFormatHandler plugins?
  - **Decision:** Yes via FormatRegistry.Register()

- [ ] **Q4:** Threading model for async operations?
  - **Decision:** Use async/await, no explicit threads

- [ ] **Q5:** Should we support format chains? (e.g., VBI to T42 to RCWT)
  - **Decision:** Yes, via fluent API

- [ ] **Q6:** Versioning strategy for NuGet package?
  - **Decision:** SemVer 2.0, v3.0.0 = breaking

### 9.2 Design Decisions Log

| Date | Decision | Rationale |
| ---- | -------- | --------- |
| 2025-01-31 | Unify filter/extract/convert into single `convert` command | Simpler mental model, enables composition |
| 2025-01-31 | Phased rollout (v2.2 to v3.0) | Minimize disruption, allow migration time |
| 2025-01-31 | IFormatHandler interface | Consistent handling, extensibility |
| 2025-01-31 | FormatIO as main entry point | Single API surface, auto-detection |
| 2025-01-31 | Centralize conversions in FormatConverter | Single source of truth, easier to test |

---

## 10. Release Timeline (Consolidated)

### 10.1 v2.2.0 - Internal Foundation (Phases 1 + 2)

**Focus:** Extract common code + introduce abstractions without breaking changes

**Includes:**

- [x] Phase 1: FormatIOBase, common I/O patterns
- [ ] Phase 2: IFormatHandler interface, FormatRegistry, ParseOptions
- [ ] All format handlers implemented
- [ ] 100% test coverage maintained
- [ ] Performance benchmarks established
- [ ] Internal documentation

**Timeline:** Month 1-2

**Release Date:** TBD

---

### 10.2 v2.4.0 - New API + Deprecation (Phase 3) ✅ RELEASED

**Focus:** New public FormatIO API available, deprecation warnings

**Includes:**

- [x] FormatConverter with centralized conversion logic ✅
- [x] Complete FormatIO public API (Open, Parse, ConvertTo, SaveTo) ✅
- [x] Extract/Restripe as first-class FormatIO terminal operations ✅
- [x] MXF start timecode reading from file ✅
- [x] Add `[Obsolete]` attributes to old methods ✅
- [x] Old API works alongside new API (both functional) ✅
- [x] Migration guide published ✅
- [x] FormatIO examples added to README files ✅
- [x] CHANGELOG.md updated ✅
- [x] Package metadata updated ✅

**Release Date:** 2026-01-24

**Migration Period:** 2-3 months before v3.0.0 (breaking changes)

---

### 10.3 v3.0.0 - Breaking Changes (Phase 4)

**Focus:** Clean API, unified CLI, remove deprecated code, FFmpeg integration

**Includes:**

- [ ] FFmpeg.AutoGen integration for MXF video VBI extraction
- [ ] Remove old filter/extract/convert commands
- [ ] New unified `convert` command only
- [ ] Remove deprecated methods (VBI.ToT42(), etc.)
- [ ] Remove backward compatibility wrappers
- [ ] Update all documentation
- [ ] Migration guide complete
- [ ] Performance validation
- [ ] v3.0 announcement blog post
- [ ] Update README with new examples only

**Timeline:** Month 6-7 (after 2-3 month migration period)

**Release Date:** TBD

---

**Note:** v2.3.0 and v2.5.0 skipped to avoid flooding NuGet with intermediate releases. Each release represents a significant, stable milestone.

---

## 11 Advanced Features

### 11.1 MXF Video VBI Extraction

**Overview:**

Starting in v3.0, `opx` will be able to extract VBI (Vertical Blanking Interval) data embedded in the video stream of MXF files, eliminating the need for FFmpeg preprocessing.

**Status:** Planned for v3.0.0

**Background:**

VBI data in broadcast video files can exist in two locations:

1. **Ancillary Data (ANC) packets** - Already supported by libopx
2. **Embedded in video frames** - Planned feature using FFmpeg.AutoGen (v3.0.0)

Traditional approach requires manual FFmpeg preprocessing:

```bash
ffmpeg -v error -i input.mxf -vf crop=720:2:0:28 -f rawvideo -pix_fmt gray - | opx filter -c
```

**New Approach:**

```bash
# Single integrated command
opx convert input.mxf --extract-vbi -m 8 -o output.t42
```

**Technical Details:**

- Uses FFmpeg.AutoGen for video frame decoding
- Default extracts line 28 with count of 2 (matching FFmpeg crop workflow)
- PAL format support (720x576, 25fps)
- Configurable line offset and count via `--vbi-lines`
- Processes frames sequentially (streaming, no full load)
- Integrates with existing filtering and conversion pipeline
- Without `--extract-vbi` flag, MXF defaults to ANC data stream extraction

**Architecture:**

```plaintext
Input MXF → FFmpeg Decode → Extract VBI Lines →
  Parse Teletext → Filter (mag/rows) → Convert Format → Output
```

**Implementation Notes:**

1. **CLI-only feature** - Not part of libopx library (avoids native deps)
2. **FFmpeg.AutoGen wrapper** - Thin abstraction over FFmpeg libraries
3. **Line extraction** - Configurable range via `--vbi-lines`
4. **Format conversion** - Outputs to T42, VBI, RCWT, STL, etc.
5. **Performance** - Comparable to FFmpeg preprocessing but single-process

**Command-Line Interface:**

```bash
opx convert [input.mxf] --extract-vbi [options]

VBI Extraction Options:
  --extract-vbi              Enable VBI extraction from video frames
                             (default: MXF extracts from ANC data stream)
  --vbi-lines <offset:count> Line offset and count (default: 28:2)

Standard Options (apply after extraction):
  -m, --magazine <num>       Filter by magazine
  -r, --rows <list>          Filter by rows
  -of, --output-format       Convert to format (t42, vbi, stl, etc.)
  -o, --output <path>        Output file
```

**Use Cases:**

1. **Archive digitization** - Extract teletext from video frames
2. **Quality comparison** - Compare VBI data vs ANC data
3. **Format migration** - Convert embedded VBI to standalone files
4. **Automated workflows** - Single-command processing pipelines

**Examples:**

```bash
# Basic extraction (default: line 28, count 2)
opx convert archive.mxf --extract-vbi -o teletext.t42

# Extract specific magazine and rows
opx convert archive.mxf --extract-vbi -m 8 -r 20-24 -o subtitles.t42

# Extract to EBU STL subtitle format
opx convert archive.mxf --extract-vbi -m 8 -of stl -o subtitles.stl

# Extract specific VBI line range (line 7, count 16 for full teletext)
opx convert archive.mxf --extract-vbi --vbi-lines 7:16 -o teletext_full.t42

# Compare VBI data sources (ANC vs video frames)
opx convert dual.mxf --extract data -o anc_vbi.t42
opx convert dual.mxf --extract-vbi -o video_vbi.t42
diff anc_vbi.t42 video_vbi.t42

# Default MXF behavior (extracts from ANC data stream, not video)
opx convert archive.mxf -o anc_teletext.t42
```

**Performance Notes:**

- Single-process execution (vs 2-process FFmpeg pipe)
- Streaming frame processing (low memory usage)
- Typical speed: 2-5x realtime on modern hardware
- No intermediate files required

**Limitations:**

- Requires FFmpeg libraries installed on system
- PAL format only in v2.4 (NTSC support planned for future release)
- Video codec support depends on FFmpeg build
- CLI-only feature (not available in libopx library API)

**Future Enhancements:**

- NTSC format support (720x480, 29.97fps)
- GPU-accelerated decoding (FFmpeg hardware acceleration)
- Parallel frame processing for multi-stream MXF
- Custom VBI line extraction patterns
- VBI quality metrics and validation

---

## 12. Success Metrics

### Code Quality

- [ ] 60%+ reduction in duplicated code (~600-800 lines removed)
- [ ] <5% performance regression
- [ ] 85%+ test coverage maintained
- [ ] Zero memory leaks
- [ ] No TODOs left in critical paths

### User Experience

- [ ] Positive community feedback on v3.0
- [ ] Successful migration of example projects
- [ ] <10% increase in GitHub issues (bugs)
- [ ] Documentation clarity score >4/5

### Adoption

- [ ] 50%+ of users migrated to v3.0 within 3 months
- [ ] New tutorial/blog posts from community
- [ ] NuGet download trend remains stable or increases

---

## 13. References

### Standards & Specifications

- [SMPTE MXF Standards](https://www.smpte.org/)
- [DVB Teletext (ETSI EN 300 472)](https://www.etsi.org/deliver/etsi_en/300400_300499/300472/01.03.01_60/en_300472v010301p.pdf)
- [EBU Tech 3264 (STL format)](https://tech.ebu.ch/publications/tech3264)

### Inspiration

- [vhs-teletext](https://github.com/ali1234/vhs-teletext) - Filter command design
- [MXFInspect](https://github.com/Myriadbits/MXFInspect) - MXF parsing insights
- [ffmpeg](https://ffmpeg.org/) - Unified command design

### .NET Best Practices

- [.NET Performance Tips](https://learn.microsoft.com/en-us/dotnet/standard/collections/thread-safe/)
- [Span\<T> and Memory\<T>](https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/)
- [ArrayPool\<T>](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1)

---

## Contributing

This is a living document. If you have suggestions or questions about the v3.0 redesign:

1. Open an issue on GitHub
2. Tag with `v3.0-design` label
3. Reference this document

**Last Updated:** 2026-01-24 (v2.4.0 released)
**Document Version:** 1.8
**Status:** v2.4.0 Released ✅ - Phase 4 (v3.0.0 breaking changes) next
