# MXF Restripe Optimization & Index Table Support

**Date:** 2026-01-26
**Status:** Draft
**Target:** v3.0.0

## Overview

Optimize MXF restriping performance for large files (10-100GB) through two complementary improvements:

1. **Direct-write optimization** - Eliminate unnecessary read operations during restripe
2. **Index table support** - Skip sequential body partition scanning by using MXF index tables

## Problem Statement

Current restriping process scans the entire MXF file sequentially, reading every KLV packet to find System packets. For a 100GB file, this means reading ~100GB of data just to write ~360KB of timecode updates (90,000 frames × 4 bytes).

### Current Flow

```
for each KLV packet in file:
    read 16-byte key
    read BER length
    if System packet:
        validate timebase/dropframe
        skip to SMPTE offset
        read 4 bytes (current timecode)
        seek back
        write 4 bytes (new timecode)
    else:
        skip packet bytes (including GB of video/audio)
```

## Phase 1: Direct-Write Optimization

### Changes to `RestripeSystemPacket`

**Remove per-packet validation:**

The Myriadbits MXFInspect validation code (checking timebase/drop-frame from rate byte) runs for every System packet. This validation should happen once at the start, not per-packet.

**Remove unnecessary read:**

The current code reads the existing SMPTE timecode before writing the new one. This read serves two purposes:
- Verify 4 bytes can be read (position validation)
- Display old value in verbose mode

Neither requires actually reading the data:
- Position is already validated by successful KLV parsing
- Verbose logging can calculate expected old value from frame count

**Before:**
```csharp
SkipPacket(input, offset);
var timecodePosition = input.Position;
var smpteRead = input.Read(_smpteBuffer, 0, Constants.SMPTE_TIMECODE_SIZE);
if (smpteRead == Constants.SMPTE_TIMECODE_SIZE)
{
    // verbose logging...
    input.Seek(timecodePosition, SeekOrigin.Begin);
    input.Write(newTimecodeBytes, 0, Constants.SMPTE_TIMECODE_SIZE);
}
```

**After:**
```csharp
SkipPacket(input, offset);
// verbose logging can calculate expected old value if needed
input.Write(newTimecodeBytes, 0, Constants.SMPTE_TIMECODE_SIZE);
```

### Impact

- ~50% fewer I/O operations per System packet
- Removes 1 read + 1 seek per frame
- For 90,000 frames: eliminates 180,000 I/O operations

## Phase 2: Index Table Support

### MXF Partition Structure

MXF OP-1a files have three partitions:

```
┌─────────────────────────────────────────────────────────────┐
│ HEADER PARTITION                                             │
│ ├─ Partition Pack (contains FooterPartition byte offset)    │
│ ├─ Header Metadata (MaterialPackage, TimecodeComponent, etc)│
│ └─ Optional: Index Table (rare in OP-1a)                    │
├─────────────────────────────────────────────────────────────┤
│ BODY PARTITION (bulk of the file: 10-100GB)                 │
│ ├─ Interleaved essence:                                     │
│ │   System → Video → Audio → Data → System → Video → ...    │
│ └─ One "edit unit" per frame                                │
├─────────────────────────────────────────────────────────────┤
│ FOOTER PARTITION                                             │
│ ├─ Partition Pack                                            │
│ ├─ Repeated Header Metadata (for fast reverse lookup)       │
│ └─ INDEX TABLE                                               │
└─────────────────────────────────────────────────────────────┘
```

### Index Table Segments

Index Table Segments contain:

| Field | Purpose |
|-------|---------|
| `IndexEditRate` | Frame rate (e.g., 25/1) |
| `IndexStartPosition` | First edit unit number in this segment |
| `IndexDuration` | Number of edit units covered |
| `EditUnitByteCount` | Fixed byte size per edit unit (if constant) |
| `IndexEntryArray` | Array of per-frame entries |

Each Index Entry contains:

| Field | Type | Purpose |
|-------|------|---------|
| `TemporalOffset` | int8 | B-frame reordering |
| `KeyFrameOffset` | int8 | Distance to previous keyframe |
| `Flags` | uint8 | Random access, sequence header |
| `StreamOffset` | uint64 | Byte offset from partition start |

**Index table types:**

- **CBE (Constant Bytes per Element)**: `EditUnitByteCount` is set, calculate offset mathematically
- **VBE (Variable Bytes per Element)**: Requires `IndexEntryArray` for each frame

### New Types

```csharp
/// <summary>
/// Represents a parsed MXF partition pack.
/// </summary>
public class PartitionPack
{
    public long FooterPartitionOffset { get; init; }
    public long HeaderByteCount { get; init; }
    public long IndexByteCount { get; init; }
    public long BodyOffset { get; init; }

    public static PartitionPack Parse(ReadOnlySpan<byte> data);
}

/// <summary>
/// Represents a parsed MXF index table.
/// </summary>
public class MxfIndex
{
    public Ratio EditRate { get; init; }
    public long BodyPartitionOffset { get; init; }
    public int EditUnitCount { get; init; }
    public bool IsConstantByteSize { get; init; }
    public int? ConstantEditUnitByteCount { get; init; }

    /// <summary>
    /// Gets the absolute file offset for an edit unit (frame).
    /// </summary>
    public long GetEditUnitOffset(int editUnit);

    /// <summary>
    /// Gets the offset to the System packet for an edit unit.
    /// System packets are at the start of each edit unit in OP-1a.
    /// </summary>
    public long GetSystemPacketOffset(int editUnit);
}
```

### Integration into MXF Class

```csharp
public class MXF
{
    /// <summary>
    /// The parsed index table, or null if not available.
    /// </summary>
    public MxfIndex? Index { get; private set; }

    /// <summary>
    /// Whether this MXF file has a usable index table.
    /// </summary>
    public bool HasIndex => Index != null;

    /// <summary>
    /// Attempts to load the index table from the footer partition.
    /// </summary>
    private MxfIndex? LoadIndexTable()
    {
        // 1. Parse header partition pack to get FooterPartition offset
        // 2. Seek to footer partition
        // 3. Parse index table segments
        // 4. Build and return MxfIndex
    }
}
```

### Optimized Restripe Flow

```csharp
public async Task RestripeAsync(...)
{
    if (mxf.HasIndex)
    {
        await RestripeWithIndexAsync(...);  // Fast path
    }
    else
    {
        await RestripeSequentialAsync(...); // Current behavior
    }
}

private async Task RestripeWithIndexAsync(...)
{
    // 1. Restripe TimecodeComponent in header (existing logic)

    // 2. For each frame, seek directly and write
    for (int frame = 0; frame < index.EditUnitCount; frame++)
    {
        var offset = index.GetSystemPacketOffset(frame);
        stream.Seek(offset, SeekOrigin.Begin);

        var timecode = startTimecode.AddFrames(frame);
        stream.Write(timecode.ToBytes());

        ReportProgress(frame, index.EditUnitCount);
    }
}
```

### Performance Comparison

For 1-hour 25fps file (90,000 frames) in a 100GB container:

| Metric | Sequential Scan | With Index Table |
|--------|-----------------|------------------|
| Bytes read | ~100 GB | ~5 MB |
| Seek operations | 180,000+ | 90,002 |
| Write operations | 90,001 | 90,001 |
| Read operations | 90,000+ | ~1,000 |
| Expected time | Minutes | Seconds |

## Error Handling & Edge Cases

### Files Without Index Tables

Graceful fallback to sequential scanning:

```csharp
if (!mxf.HasIndex)
{
    // Optionally log: "No index table found - using sequential scan"
    await RestripeSequentialAsync(...);
}
```

### Validation Checks

| Check | Action |
|-------|--------|
| Index frame count ≠ expected | Warn user, proceed with caution |
| Timebase mismatch | Error, abort restripe |
| Discontinuous timecodes | Warn: "File has gaps, will be made continuous" |
| Footer partition missing | Fall back to sequential |
| Index entries out of bounds | Corrupt index, fall back to sequential |

### Future: Sequential Timecode Validation

```csharp
/// <summary>
/// Validates that SMPTE timecodes in the file are sequential.
/// Non-sequential timecodes indicate record start/stop or spliced content.
/// </summary>
public bool ValidateSequentialTimecodes()
{
    // Check SMPTETimecodes list for frame-by-frame continuity
    // Return false if gaps detected
}
```

This could power a pre-restripe warning in simpleRestriper:
> "File contains non-sequential timecodes (possible record start/stop). Restriping will make them continuous. Continue?"

## Files Affected

| File | Changes |
|------|---------|
| `lib/Handlers/MXFHandler.cs` | Direct-write optimization, index-aware restripe |
| `lib/Formats/MXF.cs` | Index table loading, `HasIndex`/`Index` properties |
| `lib/Models/PartitionPack.cs` | New file |
| `lib/Models/MxfIndex.cs` | New file |
| `lib/Keys.cs` | Add partition pack and index table keys |

## Broader Benefits

Index table parsing benefits the entire library, not just restriping:

| Use Case | Benefit |
|----------|---------|
| Extraction | Jump to specific frame ranges |
| Filtering | Seek directly to frames with desired content |
| Timecode lookup | Find frame by timecode without linear search |
| Demuxing | Efficiently interleave reads from multiple tracks |
| Progress reporting | Know total frame count upfront |

## Implementation Order

1. **Phase 1**: Direct-write optimization (quick win, low risk)
2. **Phase 2**: Index table parsing infrastructure
3. **Phase 3**: Index-aware restripe path
4. **Phase 4**: Extend to extraction/filtering operations
5. **Future**: Sequential timecode validation
