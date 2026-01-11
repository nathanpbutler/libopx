# opx CLI Tool

[![GitHub Release](https://img.shields.io/github/v/release/nathanpbutler/libopx?style=flat-square)](https://github.com/nathanpbutler/libopx/releases)
[![.NET](https://img.shields.io/badge/.NET-9-blue?style=flat-square)](https://dotnet.microsoft.com/download/dotnet/9.0)

<!-- markdownlint-disable MD013 -->

A unified command-line interface for processing MXF files, ancillary data streams, VBI, T42, and MPEG-TS teletext files using the libopx library.

## Installation

### Pre-built Binaries

Download the latest release from [GitHub Releases](https://github.com/nathanpbutler/libopx/releases).

### Build from Source

```bash
# Clone repository
git clone https://github.com/nathanpbutler/libopx
cd libopx

# Publish as single-file executable
dotnet publish apps/opx -c Release -r win-x64 --self-contained
dotnet publish apps/opx -c Release -r linux-x64 --self-contained
dotnet publish apps/opx -c Release -r osx-x64 --self-contained

# Or run directly with .NET
dotnet run --project apps/opx -- [command] [options]
```

## Commands

### filter - Filter teletext data by magazine and rows

Extract and filter teletext data from various formats.

```bash
opx filter [options] <input-file?>
```

**Options:**

- `-if, --input-format <bin|mxf|t42|ts|vbi|vbid>`: Input format
- `-m, --magazine`: Filter by magazine number (default: all magazines)
- `-r, --rows`: Filter by number of rows (comma-separated or hyphen ranges, e.g., 1,2,5-8,15)
- `-l, --line-count`: Number of lines per frame for timecode incrementation [default: 2]
- `-c, --caps`: Use caption rows (1-24) instead of default rows (0-31)
- `--pid`: Specify MPEG-TS PID(s) to extract (comma-separated, e.g., 70 or 70,71). Only applies to TS format.
- `-V, --verbose`: Enable verbose output

**Examples:**

```bash
# Filter VBI data for magazine 8, rows 20 and 22
opx filter -m 8 -r 20,22 input.vbi

# Filter T42 data for all magazines, caption rows only
opx filter -c input.t42

# Filter MXF data for magazine 8, rows 5-8 and 15
opx filter -m 8 -r 5-8,15 input.mxf

# Filter from stdin with format override
cat input.bin | opx filter -if bin -c -

# Pipe FFmpeg output to opx
ffmpeg -v error -i input.mxf \
  -vf crop=720:2:0:28 -f rawvideo -pix_fmt gray - | \
  opx filter -c -

# Filter TS file with auto-detected teletext PIDs
opx filter -c input.ts

# Filter TS file with manual PID specification
opx filter --pid 70 -m 8 input.ts

# Filter TS with multiple PIDs
opx filter --pid 70,71 -c input.ts
```

### convert - Convert between different teletext data formats

Convert files between VBI, T42, and other supported formats.

```bash
opx convert [options] <input-file?>
```

**Options:**

- `-if, --input-format <bin|mxf|t42|ts|vbi|vbid>`: Input format (auto-detected from file extension if not specified)
- `-of, --output-format <rcwt|stl|t42|vbi|vbid>`: Output format (auto-detected from output file extension if not specified)
- `-m, --magazine`: Filter by magazine number (default: all magazines)
- `-r, --rows`: Filter by number of rows (comma-separated or hyphen ranges, e.g., 1,2,5-8,15)
- `-l, --line-count`: Number of lines per frame for timecode incrementation [default: 2]
- `-c, --caps`: Use caption rows (1-24) instead of default rows (0-31)
- `--pid`: Specify MPEG-TS PID(s) to extract (comma-separated, e.g., 70 or 70,71). Only applies to TS format.
- `--keep`: Write blank bytes if rows or magazine doesn't match
- `--stl-merge`: Enable intelligent STL merging (combines word-by-word caption buildup into single subtitles with proper timing). By default, STL output preserves raw timing with one subtitle per caption line.
- `-V, --verbose`: Enable verbose output

**Examples:**

```bash
# Convert VBI to T42 format (auto-detect from extension)
opx convert input.vbi output.t42

# Convert MXF data stream to T42 with file output
opx convert input.mxf output.t42

# Convert T42 to VBI with magazine/row filtering
opx convert -m 8 -r 20-22 input.t42 output.vbi

# Convert VBI to T42 with caption rows only
opx convert -c input.vbi output.t42

# Convert MXF to VBI preserving structure with blank bytes
opx convert --keep input.mxf output.vbi

# Pipe FFmpeg output and convert to T42
ffmpeg -v error -i input.mxf \
  -vf crop=720:2:0:28 -f rawvideo -pix_fmt gray - | \
  opx convert -of t42 - > output.t42

# Convert to RCWT (Raw Captions With Time) format
opx convert -c input.mxf output.rcwt

# Convert to EBU STL (EBU-Tech 3264) subtitle format
opx convert -c input.t42 output.stl

# Convert MXF to STL with magazine and row filtering
opx convert -m 8 -r 20-24 input.mxf output.stl

# Convert TS to T42 with auto-detected PIDs
opx convert -c input.ts output.t42

# Convert TS to T42 with manual PID specification
opx convert --pid 70 input.ts output.t42

# Convert TS to VBI with multiple PIDs
opx convert --pid 70,71 -c input.ts output.vbi

# Convert TS to STL subtitle format
opx convert --pid 70 -m 8 -c input.ts output.stl

# Convert MXF/TS to STL with raw output (default, one subtitle per caption line)
# Preserves frame-accurate timing without merging
opx convert -c input.mxf output.stl
opx convert -c input.ts output.stl

# Convert to STL with intelligent merging
# Use when captions build word-by-word and should be merged into single subtitles
opx convert -c --stl-merge input.mxf output.stl
opx convert -c --stl-merge input.ts output.stl
```

### extract - Extract/demux streams from MXF files

Extract specific data streams from MXF files for further processing.

```bash
opx extract [options] <input.mxf>
```

**Options:**

- `-o, --output`: Output base path - files will be created as `<base>_d.raw`, `<base>_v.raw`, etc
- `-k, --key <a|d|s|t|v>`: Specify keys to extract
- `-d, --demux`: Extract all keys found, output as `<base>_<hexkey>.raw`
- `-n`: Use Key/Essence names instead of hex keys (use with -d)
- `--klv`: Include key and length bytes in output files, use .klv extension
- `-V, --verbose`: Enable verbose output

**Examples:**

```bash
# Extract all streams from MXF file
opx extract input.mxf

# Extract only data and video streams
opx extract -k d,v input.mxf

# Extract all keys with custom output path
opx extract -d -o /path/to/output input.mxf

# Extract with key/length bytes included
opx extract --klv -k d input.mxf

# Extract all keys using essence names
opx extract -d -n input.mxf
```

### restripe - Restripe MXF file with new start timecode

Modify the start timecode of an MXF file while preserving all other data.

```bash
opx restripe [options] <input-file>
```

**Options:**

- `-t, --timecode` (required): New start timecode (HH:MM:SS:FF)
- `-V, --verbose`: Enable verbose output
- `-pp, --print-progress`: Print progress during parsing

**Examples:**

```bash
# Restripe MXF with new start timecode
opx restripe -t 10:00:00:00 input.mxf

# Restripe with verbose output and progress
opx restripe -t 01:30:15:10 -V -pp input.mxf
```

## Format Support

### Input Formats

- **MXF**: Material Exchange Format files
- **ANC**: MXF ancillary data streams
- **VBI**: Vertical Blanking Interval data
- **VBID**: VBI Double (2-line format)
- **T42**: Teletext packet stream
- **TS**: MPEG Transport Stream (188-byte packets)

### Output Formats

- **VBI**: Standard VBI format
- **VBID**: VBI Double format (2 lines per packet)
- **T42**: Teletext packet format

### Auto-detection

The tool automatically detects input format based on file extensions:

- `.mxf`  → MXF format
- `.bin`/`.anc`  → ANC data stream
- `.vbi`  → VBI format
- `.vbid` → VBI double-width format
- `.t42`  → T42 format
- `.ts`   → MPEG Transport Stream format

Override auto-detection using the `-if` option.

## Row Filtering

### Row Specification Formats

- **Individual rows**: `1,2,5,8`
- **Ranges**: `5-8` (equivalent to `5,6,7,8`)
- **Mixed**: `1,2,5-8,15`

### Predefined Row Sets

- **Default rows**: 0-24 (all teletext rows)
- **Caption rows**: 1-24 (excluding header row 0)

Use the `-c` flag to automatically select caption rows.

## Pipeline Examples

### FFmpeg Integration

Extract VBI data from video and process with opx:

```bash
# Extract and filter teletext from video
ffmpeg -i input.mxf \
  -vf crop=720:2:0:28 -f rawvideo -pix_fmt gray - | \
  opx filter -c -

# Convert video teletext to T42 format
ffmpeg -i input.mxf \
  -vf crop=720:2:0:28 \
  -f rawvideo -pix_fmt gray - | \
  opx convert -of t42 -f output.t42 -
```

### MPV Visualization

Combine opx output with video playback:

```bash
opx convert -of vbi input.mxf | \
  mpv --window-scale=2 \
      --demuxer=rawvideo \
      --demuxer-rawvideo-mp-format=gray \
      --demuxer-rawvideo-w=720 \
      --demuxer-rawvideo-h=2 \
      --lavfi-complex="[vid1]pad=720:32:0:28,format=yuv422p[v1]" \
      -
```

## Error Handling

The tool provides clear error messages for common issues:

- **File not found**: Check file path and permissions
- **Invalid format**: Verify file format and use `-if` to override
- **Invalid timecode**: Ensure timecode format is `HH:MM:SS:FF`
- **Invalid rows**: Check row specification syntax

Use the `-V` flag to enable verbose output for debugging.

## Performance

- **Streaming Processing**: Memory usage remains constant regardless of file size
- **Optimized I/O**: Buffered reading and writing for large files
- **Single-file Deployment**: No external dependencies required

## Requirements

- No dependencies for pre-built binaries
- .NET 9 runtime required when running from source
- Supported platforms: Windows, Linux, macOS (x64)
