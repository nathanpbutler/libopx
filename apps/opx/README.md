# opx CLI Tool

[![GitHub Release](https://img.shields.io/github/v/release/nathanpbutler/libopx?style=flat-square)](https://github.com/nathanpbutler/libopx/releases)
[![.NET](https://img.shields.io/badge/.NET-9-blue?style=flat-square)](https://dotnet.microsoft.com/download/dotnet/9.0)

A unified command-line interface for processing MXF, BIN, VBI, and T42 teletext files using the libopx library.

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

- `-m, --magazine <int>`: Filter by magazine number (default: all magazines)
- `-r, --rows <string>`: Filter by rows (comma-separated or hyphen ranges, e.g., `1,2,5-8,15`)
- `-f, --format <bin|vbi|vbid|t42>`: Input format override (auto-detected from file extension)
- `-c, --caps`: Use caption rows (1-24) instead of default rows (0-24)
- `-l, --line-count <int>`: Number of lines per frame for timecode incrementation (default: 2)
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
cat input.bin | opx filter -f bin -c -

# Pipe FFmpeg output to opx
ffmpeg -v error -i input.mxf -vf crop=720:2:0:28 -f rawvideo -pix_fmt gray - | opx filter -c -
```

### convert - Convert between different teletext data formats

Convert files between VBI, T42, and other supported formats.

```bash
opx convert [options] <input-file?>
```

**Options:**

- `-i, --input-format <bin|vbi|vbid|t42|mxf>`: Input format (auto-detected if not specified)
- `-o, --output-format <vbi|vbid|t42>`: Output format [required]
- `-f, --output-file <file>`: Output file path (writes to stdout if not specified)
- `-m, --magazine <int>`: Filter by magazine number (default: all magazines)
- `-r, --rows <string>`: Filter by rows (comma-separated or hyphen ranges)
- `-c, --caps`: Use caption rows (1-24) instead of default rows (0-24)
- `-k, --keep`: Write blank bytes if rows or magazine doesn't match
- `-l, --line-count <int>`: Number of lines per frame for timecode incrementation (default: 2)
- `-V, --verbose`: Enable verbose output

**Examples:**

```bash
# Convert VBI to T42 format (auto-detect input)
opx convert -o t42 input.vbi

# Convert MXF data stream to T42 with file output
opx convert -i mxf -o t42 -f output.t42 input.mxf

# Convert T42 to VBI with magazine/row filtering
opx convert -i t42 -o vbi -m 8 -r 20-22 input.t42

# Convert VBI to T42 with caption rows only
opx convert -o t42 -c input.vbi

# Convert MXF to VBI preserving structure with blank bytes
opx convert -i mxf -o vbi -k input.mxf

# Pipe FFmpeg output and convert to T42
ffmpeg -v error -i input.mxf -vf crop=720:2:0:28 -f rawvideo -pix_fmt gray - | opx convert -o t42 -f output.t42 -
```

### extract - Extract/demux streams from MXF files

Extract specific data streams from MXF files for further processing.

```bash
opx extract [options] <input.mxf>
```

**Options:**

- `-o, --output <string>`: Output base path - files will be created as `<base>_d.raw`, `<base>_v.raw`, etc.
- `-k, --key <d|v|s|t|a>`: Extract specific keys (data, video, system, timecode, audio)
- `-d, --demux`: Extract all keys found, output as `<base>_<hexkey>.raw`
- `-n`: Use Key/Essence names instead of hex keys (use with `-d`)
- `--klv`: Include key and length bytes in output files, use `.klv` extension
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

- `-t, --timecode <string>`: New start timecode (HH:MM:SS:FF) [required]
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

- **BIN**: MXF caption data stream
- **VBI**: Vertical Blanking Interval data
- **VBID**: VBI Double (2-line format)
- **T42**: Teletext packet stream
- **MXF**: Material Exchange Format files

### Output Formats

- **VBI**: Standard VBI format
- **VBID**: VBI Double format (2 lines per packet)
- **T42**: Teletext packet format

### Auto-detection

The tool automatically detects input format based on file extensions:

- `.bin` → BIN format
- `.vbi` → VBI format
- `.vbid` → VBI Double format
- `.t42` → T42 format
- `.mxf` → MXF format

Override auto-detection using the `-i` or `-f` options.

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
ffmpeg -i input.mxv -vf crop=720:2:0:28 -f rawvideo -pix_fmt gray - | \
  opx filter -c -

# Convert video teletext to T42 format
ffmpeg -i input.mxv -vf crop=720:2:0:28 -f rawvideo -pix_fmt gray - | \
  opx convert -o t42 -f output.t42 -
```

### MPV Visualization

Combine opx output with video playback:

```bash
opx convert -o vbi input.mxf | \
  mpv --window-scale=2 \
      --demuxer=rawvideo \
      --demuxer-rawvideo-mp-format=gray \
      --demuxer-rawvideo-w=720 \
      --demuxer-rawvideo-h=2 \
      --lavfi-complex="[vid1]pad=720:32:0:28,format=yuv422p[v1];movie='input.mxf',scale=720:576:flags=lanczos[v2];[v1][v2]vstack,setsar=608:405,setdar=1.7777[vo];amovie='input.mxf'[ao]" \
      -
```

## Error Handling

The tool provides clear error messages for common issues:

- **File not found**: Check file path and permissions
- **Invalid format**: Verify file format and use `-f` to override detection
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
