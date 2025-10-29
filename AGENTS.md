# AGENTS.md

<!-- markdownlint-disable MD013 MD022 MD031 MD032 -->

Guidance for any automated coding agent (AI pair programmer, refactoring assistant, benchmark bot, etc.) working inside this repository. This document is LLM‑agnostic and supersedes any model‑specific instructions while coexisting with `CLAUDE.md`.

## Agent Operating Principles

When proposing or making changes:
- Preserve public APIs and wire formats unless a breaking change is explicitly requested.
- Prefer streaming, low-allocation patterns already used in the parsers.
- Maintain sync/async feature parity. If you add a sync method, add the async counterpart (and vice versa) with identical semantics.
- Validate performance claims with the existing `MemoryBenchmarkTests` suite before asserting improvements.
- Use sample files by filename only (they're copied automatically to test output). Do **not** hardcode absolute paths.
- Keep conversions single-pass and avoid unnecessary buffering; reuse `ArrayPool` where applicable.
- Update or add minimal tests when modifying parsing, timing, or format conversion logic.
- Use the `SampleFiles.EnsureAsync()` helper for downloading test samples from GitHub releases automatically.
- Favor incremental patches; avoid repo-wide reformatting or stylistic churn.

## Quick Reference: Key Capabilities

- **Async Support**: All format parsers expose sync `Parse()` and async `ParseAsync()` methods; async variants yield ~90–95% lower allocations via `ArrayPool`.
- **RCWT Support**: Full Raw Captions With Time conversion with automatic T42 payload handling, field alternation, and FTS synchronization.
- **STL Support**: Complete EBU STL (EBU-Tech 3264) subtitle format support with GSI header generation, TTI block creation, BCD timecode encoding, automatic empty line filtering, and proper header/caption row text extraction.
- **Teletext Filtering**: Magazine and row filtering across VBI, T42, MXF-derived streams; filters apply universally to all output formats.
- **Memory Benchmarking**: `MemoryBenchmarkTests` verifies allocation reductions—run before/after performance-related edits.

## Common Commands

### Build

```bash
# Build entire solution
dotnet build

# Build specific projects
dotnet build lib/libopx.csproj
```

#### filter – Filter teletext data by magazine and rows
```bash
- `input` (required): MXF file path.
```
Arguments:
- `INPUT_FILE` (optional): Input file; if omitted, reads from stdin.

Options:
- `-if, --input-format <bin|mxf|t42|vbi|vbid>`: Input format
- `-m, --magazine`: Filter by magazine number (default: all magazines)
- `-r, --rows`: Filter by number of rows (comma-separated or hyphen ranges, e.g., 1,2,5-8,15)
- `-l, --line-count`: Number of lines per frame for timecode incrementation [default: 2]
- `-c, --caps`: Use caption rows (1-24) instead of default rows (0-31)
- `-V, --verbose`: Enable verbose output

#### extract – Demux streams from MXF
```bash

```
Arguments:
- `INPUT_FILE` (required): MXF file path.

Options:
- `-o, --output`: Output base path - files will be created as `<base>_d.raw`, `<base>_v.raw`, etc
- `-k, --key <a|d|s|t|v>`: Specify keys to extract
- `-d, --demux`: Extract all keys found, output as `<base>_<hexkey>.raw`
- `-n`: Use Key/Essence names instead of hex keys (use with -d)
- `--klv`: Include key and length bytes in output files, use .klv extension
- `-V, --verbose`: Enable verbose output

#### restripe – Apply new start timecode to MXF
## Common Commands

### Build

```bash
# Build entire solution
- Thread-safe FTS & field alternation (0 → 1 → 0 ...)

# Build specific projects
- FTS = `FrameNumber * 40ms` (25fps default) honoring parser `LineCount`
- State reset via `Functions.ResetRCWTHeader()`


# Release build
Status:
```

### Test

```bash
# Run all tests
- ✅ T42 → RCWT fully implemented

# Specific test method
- 🚧 VBI / MXFData / MXF → RCWT: sequential pipeline (convert → T42 → RCWT)

# Coverage


# Specific test class
Recent internal improvements:

# Memory benchmark suite
- Eliminated double conversions by centralizing RCWT header emission

# Specific memory benchmark (detailed)
- FTS synchronized with timecode, not per-line increments
```

### Publish CLI

```bash
dotnet publish apps/opx -c Release -r win-x64 --self-contained
dotnet publish apps/opx -c Release -r linux-x64 --self-contained
dotnet publish apps/opx -c Release -r osx-x64 --self-contained
```

### General Invocation

```bash
- Proper T42 detection with `Line.SetCachedType()`
```

#### filter – Filter teletext data
```bash

```
Arguments:
- `INPUT_FILE` (optional): Input file (stdin if omitted).

Options:
- `-if, --input-format <bin|mxf|t42|vbi|vbid>`: Input format
- `-m, --magazine`: Filter by magazine number (default: all magazines)
- `-r, --rows`: Filter by number of rows (comma-separated or hyphen ranges, e.g., 1,2,5-8,15)
- `-l, --line-count`: Number of lines per frame for timecode incrementation [default: 2]
- `-c, --caps`: Use caption rows (1-24) instead of default rows (0-31)
- `-V, --verbose`: Enable verbose output

#### extract – Demux MXF streams
```bash
## Parsing Examples
```
Arguments:
- `INPUT_FILE` (required): MXF file path.

Options:
- `-o, --output`: Output base path - files will be created as `<base>_d.raw`, `<base>_v.raw`, etc
- `-k, --key <a|d|s|t|v>`: Specify keys to extract
- `-d, --demux`: Extract all keys found, output as `<base>_<hexkey>.raw`
- `-n`: Use Key/Essence names instead of hex keys (use with -d)
- `--klv`: Include key and length bytes in output files, use .klv extension
- `-V, --verbose`: Enable verbose output

#### restripe – New start timecode for MXF
```bash

```
Arguments:
- `INPUT_FILE` (required): MXF file path.

Options:
- `-t, --timecode` (required): New start timecode (HH:MM:SS:FF)
- `-V, --verbose`: Enable verbose output
- `-pp, --print-progress`: Print progress during parsing

#### convert – Convert between formats
```bash
```csharp
```
Arguments:
- `INPUT_FILE`  (optional): Input file; stdin if omitted.
- `OUTPUT_FILE` (optional): Output file; stdout if omitted.

Options:
- `-if, --input-format <bin|mxf|t42|vbi|vbid>`: Input format (auto-detected from file extension if not specified)
- `-of, --output-format <rcwt|stl|t42|vbi|vbid>`: Output format (auto-detected from output file extension if not specified)
- `-m, --magazine`: Filter by magazine number (default: all magazines)
- `-r, --rows`: Filter by number of rows (comma-separated or hyphen ranges, e.g., 1,2,5-8,15)
- `-l, --line-count`: Number of lines per frame for timecode incrementation [default: 2]
- `-c, --caps`: Use caption rows (1-24) instead of default rows (0-31)
- `--keep`: Write blank bytes if rows or magazine doesn't match
- `-V, --verbose`: Enable verbose output

### Usage Examples

```bash
# Filter stdin for caption rows only
cat input.vbi | dotnet run --project apps/opx -- filter -c

# Filter magazine 1 rows 0 & 23
dotnet run --project apps/opx -- filter -m 1 -r 0,23 input.vbi

# Extract data & video streams
dotnet run --project apps/opx -- extract -k d,v input.mxf

# Demux all streams with names
dotnet run --project apps/opx -- extract -d -n -o output_base input.mxf

# Restripe MXF start timecode
dotnet run --project apps/opx -- restripe -t 10:00:00:00 input.mxf

# Convert VBI → T42 (stdout)
dotnet run --project apps/opx -- convert -of t42 input.vbi > output.t42

# MXF (data stream) → T42 file
dotnet run --project apps/opx -- convert -if mxf -of t42 input.mxf output.t42

# T42 → VBI with filtering & verbose
dotnet run --project apps/opx -- convert -if t42 -of vbi -m 8 -r 20-22 -V input.t42 output.vbi

# VBI → RCWT file
dotnet run --project apps/opx -- convert -if vbi -of rcwt input.vbi output.rcwt

# MXF → STL (EBU subtitle format) with caption rows only
dotnet run --project apps/opx -- convert -if mxf -of stl -c input.mxf output.stl

# T42 → STL with magazine and row filtering
dotnet run --project apps/opx -- convert -of stl -m 8 -r 20-24 input.t42 output.stl
```

## Parsing Examples

```csharp
// Sync VBI parse with caption row filtering and T42 conversion
using var vbi = new VBI("input.vbi");
foreach (var line in vbi.Parse(magazine: null, rows: Constants.CAPTION_ROWS))
{
    Console.WriteLine(line);
}

// Async parsing for large file (reduced allocations)
using var vbiAsync = new VBI("large_file.vbi");
await foreach (var line in vbiAsync.ParseAsync(magazine: 8, rows: [20, 22]))
{
    Console.WriteLine(line);
}

// MXF parsing with key filtering
using var mxf = new MXF("input.mxf");
mxf.AddRequiredKey("Data");
foreach (var packet in mxf.Parse(magazine: null, rows: null))
{
    Console.WriteLine(packet);
}
```

## Architectural Overview

Solution: Multi-project `.NET 9` with core library + CLI + tests.

- `lib/` Core library (`libopx`) containing:
  - `Formats/` parsers: `MXF`, `MXFData`, `VBI`, `T42`
  - `Timecode*.cs` SMPTE timecode components
  - `Packet`, `Line` data structures
  - `Functions`, `Constants`, `TeletextCharset`, `Keys`, `Enums/`
  - `SMPTE/` XML metadata definitions (elements, groups, types, labels)
- `apps/opx/` Unified CLI (filter, extract, restripe, convert)
- `tests/` xUnit tests + memory benchmarks + `SampleFiles.cs` helper for downloading test assets
- `scripts/` Helper tooling for asset generation & extraction

### Core Patterns

Streaming Parsers:
- `MXF.Parse()` / `ParseAsync()` → `Packet` enumeration with key filtering
- `MXFData.Parse()` / `ParseAsync()` → `IEnumerable` / `IAsyncEnumerable<Packet>`
- `VBI.Parse()` / `ParseAsync()` → `IEnumerable` / `IAsyncEnumerable<Line>` (auto VBI→T42)
- `T42.Parse()` / `ParseAsync()` → `Line` enumeration w/ filtering & conversions

Format Conversion Flow:
- VBI ⇄ T42 via `VBI.ToT42()` and `T42.ToVBI()`
- Optional RCWT wrapping using `Line.ToRCWT()` with FTS/field state management
- Optional STL conversion using `Line.ToSTL()` with BCD timecode encoding and automatic empty line filtering
- Output format chosen by `Line.ParseLine()` logic with universal row/magazine filtering

MXF Processing:
- Key filtering (`AddRequiredKey()` / enum KeyType)
- Demux & extract modes
- Optional KLV preservation
- Sequential timecode validation

Performance:
- `ArrayPool` reuse in async parsers
- ~90–95% allocation reduction vs sync methods (validated in benchmarks)
- ReadyToRun + single-file publishing for CLI

## Development Guidelines for Agents

1. Add tests when altering timing, parsing boundaries, or buffer sizing.
2. Keep exception messages concise; avoid leaking file system paths beyond necessity.
3. For new formats or conversions, mirror the sync/async dual API.
4. Use `Span<byte>`/`ReadOnlySpan<byte>` where possible over array copies.
5. Dispose streams promptly; prefer `using` / `await using`.
6. If adding a dependency, justify necessity (performance, standards compliance, security) and prefer zero-dependency solutions first.
7. Avoid global state except where already centralized (e.g., RCWT header state) and document threading implications.
8. Maintain consistent naming (PascalCase public, `_camelCase` private fields if introduced; follow existing style).

## Testing Guidance

- Reference sample files directly: `"input.vbi"`, `"input.mxf"`, etc.
- Sample files are automatically downloaded from GitHub releases using `SampleFiles.EnsureAsync()`.
- Set `OPX_SAMPLES_VERSION` environment variable to use a specific release version (defaults to v1.0.0).
- Validate both sync and async paths.
- Run memory benchmarks after performance changes: `dotnet test --filter "MemoryBenchmarkTests"`.
- Cover new branches in format detection or timecode arithmetic.

## Safety & Integrity Checks

Before opening a PR (automated or manual):
- Build succeeds in Debug & Release.
- All tests pass.
- No unexplained public API changes (`grep` diff for `public`).
- Benchmarks unchanged or improved (if performance-sensitive change).
- New docs or README adjustments included for new features.

## Extension Opportunities (Optional for Agents)

If proposing enhancements, consider incremental RFC-style issues first for:
- Additional caption/teletext container formats
- Advanced MXF essence stream analytics
- Pluggable timecode frame rate strategies
- SIMD or hardware-accelerated decoding paths

## Reference Files

See also:
- `EXAMPLES.md` – Detailed parser usage patterns
- `ASYNC_FEATURES.md` – Async design and memory optimization notes
- `README.md` – High-level project summary
- `CLAUDE.md` – Legacy model-specific guidance (still present for historical context)

---
This `AGENTS.md` is the canonical, model-neutral source of guidance. Automated agents should prioritize this file over any model-branded instructions unless explicitly directed otherwise.
