# libopx Development Notes

## Project

- .NET 9 (C#, nullable enabled, implicit usings)
- Root namespace: `nathanbutlerDEV.libopx`
- SDK pinned via `global.json` (rollForward: latestMajor)

## Architecture

### Directory Structure

- `lib/` — core library (NuGet package: `libopx`)
  - `Formats/` — parsers: MXF, MXFData, VBI, T42, TS
  - `Handlers/` — internal format handlers
  - `Exporters/` — output format writers
  - `Timecode.cs`, `TimecodeComponent.cs` — SMPTE timecode
  - `Packet.cs`, `Line.cs` — data structures
  - `Functions.cs`, `Constants.cs`, `Keys.cs`, `Enums/`
  - `SMPTE/` — XML metadata definitions
- `apps/opx/` — CLI tool (System.CommandLine)
- `apps/simpleRestriper/` — Avalonia desktop GUI for MXF restriping (to be replaced by opxBlazor)
- `apps/opxBlazor/` — Blazor Server GUI with MudBlazor
- `tests/` — xUnit tests + memory benchmarks
- `samples/`, `scripts/` — sample files and helper tooling

### Core Patterns

- **Handler bypass pattern**: T42Handler and VBIHandler set Line properties (Magazine, Row, Text) directly — they do NOT call `Line.ParseLine()` or `Line.ExtractMetadata()`. When adding new properties to Line that derive from T42 data, you must also update these handlers. ANCHandler, MXFHandler, and TSHandler DO call `ParseLine()`/`ParseLineAsync()`.
- **FormatIO** is the public fluent API; handlers are internal. `FormatIO.Open()` auto-detects format from file extension.
- **Line-based formats**: VBI, VBI_DOUBLE, T42 → `ParseLines()`/`ParseLinesAsync()`
- **Packet-based formats**: ANC, TS, MXF → `ParsePackets()`/`ParsePacketsAsync()`
- **Sync/async parity**: all format parsers expose `Parse()` and `ParseAsync()`; async variants yield ~90–95% lower allocations via `ArrayPool`.

### Streaming Parsers

- `MXF.Parse()` / `ParseAsync()` → `Packet` enumeration with key filtering
- `MXFData.Parse()` / `ParseAsync()` → `IEnumerable` / `IAsyncEnumerable<Packet>`
- `VBI.Parse()` / `ParseAsync()` → `IEnumerable` / `IAsyncEnumerable<Line>` (auto VBI→T42)
- `T42.Parse()` / `ParseAsync()` → `Line` enumeration w/ filtering & conversions
- `TS.Parse()` / `ParseAsync()` → `Line` enumeration with PAT/PMT parsing, PES accumulation, teletext extraction (LSB-first bit reversal)

### Format Conversion

- VBI ⇄ T42 via `VBI.ToT42()` and `T42.ToVBI()`
- RCWT wrapping via `Line.ToRCWT()` with FTS/field state management
- STL conversion via `Line.ToSTL()` with BCD timecode encoding and automatic empty line filtering
- Output format chosen by `Line.ParseLine()` logic with universal row/magazine filtering

### MXF Processing

- Key filtering (`AddRequiredKey()` / enum KeyType)
- Demux & extract modes with optional KLV preservation
- Sequential timecode validation
- Restripe modifies timecodes in-place by seeking back and overwriting

## Build & Test

```bash
dotnet build libopx.sln          # build everything
dotnet build lib/libopx.csproj   # build library only
dotnet test tests/libopx.Tests.csproj  # run all tests
dotnet test --filter "MemoryBenchmarkTests"  # memory benchmarks
dotnet run --project apps/opx    # run CLI tool
dotnet run --project apps/opxBlazor  # run Blazor GUI
```

Publish CLI:

```bash
dotnet publish apps/opx -c Release -r win-x64 --self-contained
dotnet publish apps/opx -c Release -r linux-x64 --self-contained
dotnet publish apps/opx -c Release -r osx-x64 --self-contained
```

- Pre-existing CS0618 warnings in MXF.cs and tests are expected (obsolete API usage).

## CLI Commands (opx)

### filter — Filter teletext data by magazine and rows

- `INPUT_FILE` (optional): input file; stdin if omitted
- `-if, --input-format <bin|mxf|t42|vbi|vbid>`: input format
- `-m, --magazine`: filter by magazine number
- `-r, --rows`: filter rows (comma-separated or ranges, e.g. `1,2,5-8,15`)
- `-l, --line-count`: lines per frame for timecode incrementation [default: 2]
- `-c, --caps`: use caption rows (1-24) instead of default (0-31)
- `--pid`: TS PID(s) to extract (comma-separated, e.g. `70` or `70,71`)
- `-V, --verbose`: verbose output

### extract — Demux streams from MXF

- `INPUT_FILE` (required): MXF file path
- `-o, --output`: output base path (files created as `<base>_d.raw`, etc.)
- `-k, --key <a|d|s|t|v>`: keys to extract
- `-d, --demux`: extract all keys, output as `<base>_<hexkey>.raw`
- `-n`: use key/essence names instead of hex keys (with `-d`)
- `--klv`: include key and length bytes, use `.klv` extension
- `-V, --verbose`: verbose output

### restripe — Apply new start timecode to MXF

- `INPUT_FILE` (required): MXF file path
- `-t, --timecode` (required): new start timecode (HH:MM:SS:FF)
- `-V, --verbose`: verbose output
- `-pp, --print-progress`: print progress during parsing

### convert — Convert between formats

- `INPUT_FILE` (optional): input file; stdin if omitted
- `OUTPUT_FILE` (optional): output file; stdout if omitted
- `-if, --input-format <bin|mxf|t42|vbi|vbid>`: input format (auto-detected from extension)
- `-of, --output-format <rcwt|stl|t42|vbi|vbid>`: output format (auto-detected from extension)
- `--pid`: TS PID(s) to extract (comma-separated, e.g. `70` or `70,71`)
- `-m, --magazine`, `-r, --rows`, `-l, --line-count`, `-c, --caps`, `--keep`, `-V, --verbose`

### Usage Examples

```bash
cat input.vbi | dotnet run --project apps/opx -- filter -c
dotnet run --project apps/opx -- filter -m 1 -r 0,23 input.vbi
dotnet run --project apps/opx -- extract -k d,v input.mxf
dotnet run --project apps/opx -- extract -d -n -o output_base input.mxf
dotnet run --project apps/opx -- restripe -t 10:00:00:00 input.mxf
dotnet run --project apps/opx -- convert -of t42 input.vbi > output.t42
dotnet run --project apps/opx -- convert input.mxf output.t42
dotnet run --project apps/opx -- convert -m 8 -r 20-22 -V input.t42 output.vbi
dotnet run --project apps/opx -- convert input.vbi output.rcwt
dotnet run --project apps/opx -- convert -c input.mxf output.stl
dotnet run --project apps/opx -- convert --pid 70 input.ts output.t42
```

## Library API Examples

```csharp
// Sync VBI parse with caption row filtering
using var vbi = new VBI("input.vbi");
foreach (var line in vbi.Parse(magazine: null, rows: Constants.CAPTION_ROWS))
    Console.WriteLine(line);

// Async parsing (reduced allocations)
using var vbiAsync = new VBI("large_file.vbi");
await foreach (var line in vbiAsync.ParseAsync(magazine: 8, rows: [20, 22]))
    Console.WriteLine(line);

// MXF parsing with key filtering
using var mxf = new MXF("input.mxf");
mxf.AddRequiredKey("Data");
foreach (var packet in mxf.Parse(magazine: null, rows: null))
    Console.WriteLine(packet);

// TS parsing with auto-detection of teletext PIDs
using var ts = new TS("input.ts");
await foreach (var line in ts.ParseAsync(magazine: 8, rows: Constants.CAPTION_ROWS))
    Console.WriteLine(line);
```

## Development Guidelines

- Preserve public APIs and wire formats unless a breaking change is explicitly requested.
- Prefer streaming, low-allocation patterns already used in the parsers.
- Maintain sync/async parity — if you add a sync method, add the async counterpart (and vice versa).
- Use `Span<byte>`/`ReadOnlySpan<byte>` where possible over array copies. Reuse `ArrayPool` where applicable.
- Keep conversions single-pass; avoid unnecessary buffering.
- Favor incremental patches; avoid repo-wide reformatting or stylistic churn.
- Prefer zero-dependency solutions; justify new dependencies by necessity (performance, standards compliance, security).
- Keep exception messages concise; avoid leaking file system paths beyond necessity.
- PascalCase public, `_camelCase` private fields. Follow existing style.
- Dispose streams promptly; prefer `using` / `await using`.

## Testing

- Reference sample files by filename only: `"input.vbi"`, `"input.mxf"`, etc. (copied automatically to test output).
- Use `SampleFiles.EnsureAsync()` to download test samples from GitHub releases. Set `OPX_SAMPLES_VERSION` env var for a specific version (defaults to v1.0.0).
- Validate both sync and async paths.
- Run memory benchmarks after performance changes.
- Add tests when altering timing, parsing boundaries, or buffer sizing.
