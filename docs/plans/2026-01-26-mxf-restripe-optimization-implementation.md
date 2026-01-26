# MXF Restripe Optimization Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Optimize MXF restriping to eliminate unnecessary I/O operations and enable index-table-based fast seeking.

**Architecture:** Phase 1 removes redundant read/seek operations from `RestripeSystemPacket`. Phase 2 adds partition pack and index table parsing to enable direct seeking to System packets, skipping the sequential body scan entirely.

**Tech Stack:** .NET 9, xUnit, existing libopx MXF infrastructure

---

## Task 1: Add Restripe Optimization Test

**Files:**
- Modify: `tests/Handlers/MXFHandlerTests.cs`

**Step 1: Write the failing test**

Add this test after the existing `Parse_RestripeMode_ValidatesTimecodeModification` test (around line 309):

```csharp
[Fact]
public void Parse_RestripeMode_ModifiesMultipleFrameTimecodes()
{
    // Arrange - Create temp file copy for restriping
    var tempFile = Path.GetTempFileName();
    _tempFilesToDelete.Add(tempFile);
    File.WriteAllBytes(tempFile, _sampleData!);

    var newTimecode = new Timecode(1, 0, 0, 0); // 01:00:00:00
    var handler = new MXFHandler(
        newTimecode,
        inputFilePath: tempFile,
        function: Function.Restripe
    );

    // Act - Restripe the file
    using (var stream = File.Open(tempFile, FileMode.Open, FileAccess.ReadWrite))
    {
        var options = new ParseOptions { OutputFormat = Format.T42 };
        foreach (var _ in handler.Parse(stream, options)) { }
    }

    // Assert - Verify SMPTE timecodes are sequential from new start
    using var mxf = new MXF(tempFile);
    Assert.Equal(newTimecode.ToString(), mxf.StartTimecode.ToString());

    // Parse to get SMPTE timecodes
    var verifyHandler = new MXFHandler(mxf.StartTimecode, function: Function.Filter);
    using var verifyStream = File.OpenRead(tempFile);
    foreach (var _ in verifyHandler.Parse(verifyStream, new ParseOptions { OutputFormat = Format.T42 })) { }

    var timecodes = verifyHandler.SMPTETimecodes;
    Assert.NotEmpty(timecodes);

    // First SMPTE timecode should match new start
    Assert.Equal(newTimecode.ToString(), timecodes[0].ToString());

    // Timecodes should be sequential
    for (int i = 1; i < timecodes.Count; i++)
    {
        var expected = timecodes[i - 1].AddFrames(1);
        Assert.Equal(expected.ToString(), timecodes[i].ToString());
    }
}
```

**Step 2: Run test to verify it passes (baseline)**

Run: `dotnet test --filter "Parse_RestripeMode_ModifiesMultipleFrameTimecodes" -v n`

Expected: PASS (this establishes baseline behavior before optimization)

**Step 3: Commit**

```bash
git add tests/Handlers/MXFHandlerTests.cs
git commit -m "$(cat <<'EOF'
test: add restripe multi-frame verification test

Establishes baseline behavior before direct-write optimization.
Verifies SMPTE timecodes are sequential after restriping.
EOF
)"
```

---

## Task 2: Optimize RestripeSystemPacket (Sync Version)

**Files:**
- Modify: `lib/Handlers/MXFHandler.cs:1196-1248`

**Step 1: Read the current implementation**

Review the current `RestripeSystemPacket` method at lines 1196-1248.

**Step 2: Modify to remove unnecessary read/seek**

Replace the method with this optimized version:

```csharp
private void RestripeSystemPacket(Stream input, int length, Timecode newTimecode)
{
    var offset = GetSystemMetadataOffset(length);
    if (offset < 0)
    {
        SkipPacket(input, length);
        return;
    }

    // Skip directly to SMPTE timecode position
    SkipPacket(input, offset);

    if (_verbose)
    {
        // Calculate expected current timecode based on frame count
        // rather than reading from file
        var expectedCurrent = StartTimecode.AddFrames(_frameCount);
        Console.WriteLine($"Restriping System timecode at offset {offset}: {expectedCurrent} -> {newTimecode}");
    }

    // Write new timecode directly (no read, no seek-back needed)
    var newTimecodeBytes = newTimecode.ToBytes();
    input.Write(newTimecodeBytes, 0, Constants.SMPTE_TIMECODE_SIZE);

    var remainingBytes = length - offset - Constants.SMPTE_TIMECODE_SIZE;
    if (remainingBytes > 0)
    {
        SkipPacket(input, remainingBytes);
    }
}
```

**Step 3: Add frame counter field if not present**

Check if `_frameCount` field exists. If not, add it near other private fields:

```csharp
private int _frameCount;
```

And increment it in the restripe parse loop after each System packet.

**Step 4: Run tests to verify optimization doesn't break behavior**

Run: `dotnet test --filter "MXFHandlerTests" -v n`

Expected: All tests PASS

**Step 5: Commit**

```bash
git add lib/Handlers/MXFHandler.cs
git commit -m "$(cat <<'EOF'
perf: optimize RestripeSystemPacket to eliminate read/seek

Remove unnecessary read of current timecode value and seek-back
before writing. Reduces I/O operations by ~50% per System packet.
EOF
)"
```

---

## Task 3: Optimize RestripeSystemPacketAsync

**Files:**
- Modify: `lib/Handlers/MXFHandler.cs:1324-1381`

**Step 1: Modify async version to match sync optimization**

Replace the method with this optimized version:

```csharp
private async Task RestripeSystemPacketAsync(Stream input, int length, Timecode newTimecode, CancellationToken cancellationToken)
{
    var offset = GetSystemMetadataOffset(length);
    if (offset < 0)
    {
        await SkipPacketAsync(input, length, cancellationToken);
        return;
    }

    // Skip directly to SMPTE timecode position
    await SkipPacketAsync(input, offset, cancellationToken);

    if (_verbose)
    {
        // Calculate expected current timecode based on frame count
        var expectedCurrent = StartTimecode.AddFrames(_frameCount);
        Console.WriteLine($"Restriping System timecode at offset {offset}: {expectedCurrent} -> {newTimecode}");
    }

    // Write new timecode directly (no read, no seek-back needed)
    var newTimecodeBytes = newTimecode.ToBytes();
    await input.WriteAsync(newTimecodeBytes.AsMemory(0, Constants.SMPTE_TIMECODE_SIZE), cancellationToken);

    var remainingBytes = length - offset - Constants.SMPTE_TIMECODE_SIZE;
    if (remainingBytes > 0)
    {
        await SkipPacketAsync(input, remainingBytes, cancellationToken);
    }
}
```

**Step 2: Run tests**

Run: `dotnet test --filter "MXFHandlerTests" -v n`

Expected: All tests PASS

**Step 3: Commit**

```bash
git add lib/Handlers/MXFHandler.cs
git commit -m "$(cat <<'EOF'
perf: optimize RestripeSystemPacketAsync to match sync version

Apply same direct-write optimization to async restripe path.
EOF
)"
```

---

## Task 4: Remove Per-Packet Myriadbits Validation

**Files:**
- Modify: `lib/Handlers/MXFHandler.cs`

**Step 1: Move validation to once-per-file**

The Myriadbits validation (lines 1205-1223 in sync, 1333-1355 in async) should run once when restripe starts, not per-packet.

Add a new method and field:

```csharp
private bool _timebaseValidated;

/// <summary>
/// Validates that the first System packet's timebase matches the target timecode.
/// Called once at the start of restripe operation.
/// </summary>
private void ValidateTimebaseOnce(Stream input, int length, Timecode newTimecode)
{
    if (_timebaseValidated) return;

    var offset = GetSystemMetadataOffset(length);
    if (offset < 0) return;

    input.Seek(1, SeekOrigin.Current); // Skip first byte
    int timebase = StartTimecode.Timebase;
    bool dropFrame = StartTimecode.DropFrame;
    var rate = input.ReadByte();
    int rateIndex = (rate & 0x1E) >> 1;
    int[] rates = [0, 24, 25, 30, 48, 50, 60, 72, 75, 90, 96, 100, 120, 0, 0, 0];
    if (rateIndex < 16)
        timebase = rates[rateIndex];
    if ((rate & 0x01) == 0x01)
        dropFrame = true;
    input.Seek(-2, SeekOrigin.Current);

    if (newTimecode.Timebase != timebase || newTimecode.DropFrame != dropFrame)
    {
        throw new InvalidOperationException(
            $"New timecode {newTimecode} does not match file timebase {timebase} and drop frame {dropFrame}.");
    }

    _timebaseValidated = true;
}
```

**Step 2: Update RestripeSystemPacket to call validation once**

```csharp
private void RestripeSystemPacket(Stream input, int length, Timecode newTimecode)
{
    var offset = GetSystemMetadataOffset(length);
    if (offset < 0)
    {
        SkipPacket(input, length);
        return;
    }

    // Validate timebase on first System packet only
    if (!_timebaseValidated)
    {
        ValidateTimebaseOnce(input, length, newTimecode);
    }

    // Skip directly to SMPTE timecode position
    SkipPacket(input, offset);

    if (_verbose)
    {
        var expectedCurrent = StartTimecode.AddFrames(_frameCount);
        Console.WriteLine($"Restriping System timecode at offset {offset}: {expectedCurrent} -> {newTimecode}");
    }

    var newTimecodeBytes = newTimecode.ToBytes();
    input.Write(newTimecodeBytes, 0, Constants.SMPTE_TIMECODE_SIZE);

    var remainingBytes = length - offset - Constants.SMPTE_TIMECODE_SIZE;
    if (remainingBytes > 0)
    {
        SkipPacket(input, remainingBytes);
    }
}
```

**Step 3: Add async validation method**

```csharp
private async Task ValidateTimebaseOnceAsync(Stream input, int length, Timecode newTimecode, CancellationToken cancellationToken)
{
    if (_timebaseValidated) return;

    var offset = GetSystemMetadataOffset(length);
    if (offset < 0) return;

    input.Seek(1, SeekOrigin.Current);
    int timebase = StartTimecode.Timebase;
    bool dropFrame = StartTimecode.DropFrame;
    var rateBuffer = new byte[1];
    await input.ReadAsync(rateBuffer.AsMemory(), cancellationToken);
    var rate = rateBuffer[0];
    int rateIndex = (rate & 0x1E) >> 1;
    int[] rates = [0, 24, 25, 30, 48, 50, 60, 72, 75, 90, 96, 100, 120, 0, 0, 0];
    if (rateIndex < 16)
        timebase = rates[rateIndex];
    if ((rate & 0x01) == 0x01)
        dropFrame = true;
    input.Seek(-2, SeekOrigin.Current);

    if (newTimecode.Timebase != timebase || newTimecode.DropFrame != dropFrame)
    {
        throw new InvalidOperationException(
            $"New timecode {newTimecode} does not match file timebase {timebase} and drop frame {dropFrame}.");
    }

    _timebaseValidated = true;
}
```

**Step 4: Update RestripeSystemPacketAsync similarly**

**Step 5: Run tests**

Run: `dotnet test --filter "MXFHandlerTests" -v n`

Expected: All tests PASS

**Step 6: Commit**

```bash
git add lib/Handlers/MXFHandler.cs
git commit -m "$(cat <<'EOF'
perf: move timebase validation to once-per-file

Myriadbits validation now runs only on first System packet instead
of every packet. Further reduces per-frame I/O overhead.
EOF
)"
```

---

## Task 5: Add Partition Pack Key Constants

**Files:**
- Modify: `lib/Keys.cs`

**Step 1: Add partition pack key definitions**

Add after the existing key definitions (around line 50):

```csharp
#region Partition Pack Keys

/// <summary>
/// Header Partition Pack Key (Open Incomplete)
/// </summary>
public static readonly byte[] HeaderPartitionPackOpenIncomplete =
    [0x06, 0x0E, 0x2B, 0x34, 0x02, 0x05, 0x01, 0x01, 0x0D, 0x01, 0x02, 0x01, 0x01, 0x02, 0x01, 0x00];

/// <summary>
/// Header Partition Pack Key (Closed Incomplete)
/// </summary>
public static readonly byte[] HeaderPartitionPackClosedIncomplete =
    [0x06, 0x0E, 0x2B, 0x34, 0x02, 0x05, 0x01, 0x01, 0x0D, 0x01, 0x02, 0x01, 0x01, 0x02, 0x02, 0x00];

/// <summary>
/// Header Partition Pack Key (Open Complete)
/// </summary>
public static readonly byte[] HeaderPartitionPackOpenComplete =
    [0x06, 0x0E, 0x2B, 0x34, 0x02, 0x05, 0x01, 0x01, 0x0D, 0x01, 0x02, 0x01, 0x01, 0x02, 0x03, 0x00];

/// <summary>
/// Header Partition Pack Key (Closed Complete)
/// </summary>
public static readonly byte[] HeaderPartitionPackClosedComplete =
    [0x06, 0x0E, 0x2B, 0x34, 0x02, 0x05, 0x01, 0x01, 0x0D, 0x01, 0x02, 0x01, 0x01, 0x02, 0x04, 0x00];

/// <summary>
/// Footer Partition Pack Key (Closed Complete)
/// </summary>
public static readonly byte[] FooterPartitionPackClosedComplete =
    [0x06, 0x0E, 0x2B, 0x34, 0x02, 0x05, 0x01, 0x01, 0x0D, 0x01, 0x02, 0x01, 0x01, 0x04, 0x04, 0x00];

/// <summary>
/// Index Table Segment Key
/// </summary>
public static readonly byte[] IndexTableSegment =
    [0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0D, 0x01, 0x02, 0x01, 0x01, 0x10, 0x01, 0x00];

#endregion
```

**Step 2: Add KeyType enum values for partitions**

Modify `lib/Enums/KeyType.cs` to add:

```csharp
HeaderPartition,
FooterPartition,
IndexTableSegment,
```

**Step 3: Update GetKeyType method to recognize partition keys**

In `Keys.cs`, update the key matching logic to detect partition and index keys.

**Step 4: Run tests**

Run: `dotnet test -v n`

Expected: All tests PASS

**Step 5: Commit**

```bash
git add lib/Keys.cs lib/Enums/KeyType.cs
git commit -m "$(cat <<'EOF'
feat: add partition pack and index table key constants

Prepare for index table parsing by defining MXF partition pack keys
and index table segment key.
EOF
)"
```

---

## Task 6: Create PartitionPack Model

**Files:**
- Create: `lib/Core/PartitionPack.cs`

**Step 1: Create the PartitionPack class**

```csharp
namespace nathanbutlerDEV.libopx.Core;

/// <summary>
/// Represents a parsed MXF partition pack containing offsets and sizes.
/// Used to locate footer partition and index tables.
/// </summary>
public class PartitionPack
{
    /// <summary>
    /// Major version of the MXF file.
    /// </summary>
    public ushort MajorVersion { get; init; }

    /// <summary>
    /// Minor version of the MXF file.
    /// </summary>
    public ushort MinorVersion { get; init; }

    /// <summary>
    /// Size of the KAG (KLV Alignment Grid) in bytes.
    /// </summary>
    public uint KagSize { get; init; }

    /// <summary>
    /// Byte offset to this partition from start of file.
    /// </summary>
    public long ThisPartition { get; init; }

    /// <summary>
    /// Byte offset to previous partition from start of file.
    /// </summary>
    public long PreviousPartition { get; init; }

    /// <summary>
    /// Byte offset to footer partition from start of file.
    /// Zero if no footer or not yet written.
    /// </summary>
    public long FooterPartition { get; init; }

    /// <summary>
    /// Size of header metadata in this partition.
    /// </summary>
    public long HeaderByteCount { get; init; }

    /// <summary>
    /// Size of index table in this partition.
    /// </summary>
    public long IndexByteCount { get; init; }

    /// <summary>
    /// Size of index table in bytes.
    /// </summary>
    public uint IndexSid { get; init; }

    /// <summary>
    /// Byte offset to start of essence container.
    /// </summary>
    public long BodyOffset { get; init; }

    /// <summary>
    /// Body SID (Stream ID) for essence.
    /// </summary>
    public uint BodySid { get; init; }

    /// <summary>
    /// Parses a partition pack from raw bytes.
    /// </summary>
    /// <param name="data">The partition pack data (after KLV key and length).</param>
    /// <returns>A parsed PartitionPack.</returns>
    public static PartitionPack Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 64)
            throw new ArgumentException("Partition pack data too short", nameof(data));

        return new PartitionPack
        {
            MajorVersion = ReadUInt16BE(data, 0),
            MinorVersion = ReadUInt16BE(data, 2),
            KagSize = ReadUInt32BE(data, 4),
            ThisPartition = ReadInt64BE(data, 8),
            PreviousPartition = ReadInt64BE(data, 16),
            FooterPartition = ReadInt64BE(data, 24),
            HeaderByteCount = ReadInt64BE(data, 32),
            IndexByteCount = ReadInt64BE(data, 40),
            IndexSid = ReadUInt32BE(data, 48),
            BodyOffset = ReadInt64BE(data, 52),
            BodySid = ReadUInt32BE(data, 60),
        };
    }

    private static ushort ReadUInt16BE(ReadOnlySpan<byte> data, int offset)
    {
        return (ushort)((data[offset] << 8) | data[offset + 1]);
    }

    private static uint ReadUInt32BE(ReadOnlySpan<byte> data, int offset)
    {
        return (uint)((data[offset] << 24) | (data[offset + 1] << 16) |
                      (data[offset + 2] << 8) | data[offset + 3]);
    }

    private static long ReadInt64BE(ReadOnlySpan<byte> data, int offset)
    {
        return ((long)data[offset] << 56) | ((long)data[offset + 1] << 48) |
               ((long)data[offset + 2] << 40) | ((long)data[offset + 3] << 32) |
               ((long)data[offset + 4] << 24) | ((long)data[offset + 5] << 16) |
               ((long)data[offset + 6] << 8) | data[offset + 7];
    }
}
```

**Step 2: Run build to verify compilation**

Run: `dotnet build`

Expected: Build succeeds

**Step 3: Commit**

```bash
git add lib/Core/PartitionPack.cs
git commit -m "$(cat <<'EOF'
feat: add PartitionPack model for MXF partition parsing

Parses partition pack data to extract footer offset and index info.
EOF
)"
```

---

## Task 7: Add PartitionPack Unit Tests

**Files:**
- Create: `tests/Core/PartitionPackTests.cs`

**Step 1: Write the tests**

```csharp
using nathanbutlerDEV.libopx.Core;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Core;

public class PartitionPackTests
{
    [Fact]
    public void Parse_ValidData_ExtractsFields()
    {
        // Arrange - Minimal 64-byte partition pack
        var data = new byte[64];

        // Major version = 1 (bytes 0-1)
        data[0] = 0x00; data[1] = 0x01;
        // Minor version = 3 (bytes 2-3)
        data[2] = 0x00; data[3] = 0x03;
        // KAG size = 512 (bytes 4-7)
        data[4] = 0x00; data[5] = 0x00; data[6] = 0x02; data[7] = 0x00;
        // This partition = 0 (bytes 8-15)
        // Previous partition = 0 (bytes 16-23)
        // Footer partition = 0x1000000 (bytes 24-31)
        data[24] = 0x00; data[25] = 0x00; data[26] = 0x00; data[27] = 0x00;
        data[28] = 0x01; data[29] = 0x00; data[30] = 0x00; data[31] = 0x00;

        // Act
        var pack = PartitionPack.Parse(data);

        // Assert
        Assert.Equal((ushort)1, pack.MajorVersion);
        Assert.Equal((ushort)3, pack.MinorVersion);
        Assert.Equal(512u, pack.KagSize);
        Assert.Equal(0x1000000L, pack.FooterPartition);
    }

    [Fact]
    public void Parse_TooShortData_ThrowsArgumentException()
    {
        // Arrange
        var data = new byte[32]; // Too short

        // Act & Assert
        Assert.Throws<ArgumentException>(() => PartitionPack.Parse(data));
    }

    [Fact]
    public void Parse_ZeroFooterPartition_IndicatesNoFooter()
    {
        // Arrange
        var data = new byte[64]; // All zeros

        // Act
        var pack = PartitionPack.Parse(data);

        // Assert
        Assert.Equal(0L, pack.FooterPartition);
    }
}
```

**Step 2: Run tests**

Run: `dotnet test --filter "PartitionPackTests" -v n`

Expected: All tests PASS

**Step 3: Commit**

```bash
git add tests/Core/PartitionPackTests.cs
git commit -m "$(cat <<'EOF'
test: add PartitionPack unit tests
EOF
)"
```

---

## Task 8: Create MxfIndex Model

**Files:**
- Create: `lib/Core/MxfIndex.cs`

**Step 1: Create the MxfIndex class**

```csharp
namespace nathanbutlerDEV.libopx.Core;

/// <summary>
/// Represents a parsed MXF index table for efficient frame-level seeking.
/// </summary>
public class MxfIndex
{
    /// <summary>
    /// Edit rate as numerator/denominator (e.g., 25/1 for 25fps).
    /// </summary>
    public (int Numerator, int Denominator) EditRate { get; init; }

    /// <summary>
    /// Byte offset to start of body partition.
    /// </summary>
    public long BodyPartitionOffset { get; init; }

    /// <summary>
    /// Total number of edit units (frames) in the index.
    /// </summary>
    public int EditUnitCount { get; init; }

    /// <summary>
    /// Whether all edit units have constant byte size (CBE).
    /// </summary>
    public bool IsConstantByteSize { get; init; }

    /// <summary>
    /// Constant byte count per edit unit (only valid if IsConstantByteSize is true).
    /// </summary>
    public int? ConstantEditUnitByteCount { get; init; }

    /// <summary>
    /// Per-frame stream offsets for VBE (variable byte) index tables.
    /// Null for CBE index tables.
    /// </summary>
    public long[]? StreamOffsets { get; init; }

    /// <summary>
    /// Gets the absolute file offset for an edit unit (frame).
    /// </summary>
    /// <param name="editUnit">Zero-based edit unit number.</param>
    /// <returns>Absolute byte offset in the file.</returns>
    public long GetEditUnitOffset(int editUnit)
    {
        if (editUnit < 0 || editUnit >= EditUnitCount)
            throw new ArgumentOutOfRangeException(nameof(editUnit));

        if (IsConstantByteSize && ConstantEditUnitByteCount.HasValue)
        {
            return BodyPartitionOffset + (long)editUnit * ConstantEditUnitByteCount.Value;
        }

        if (StreamOffsets != null && editUnit < StreamOffsets.Length)
        {
            return BodyPartitionOffset + StreamOffsets[editUnit];
        }

        throw new InvalidOperationException("Index table does not contain offset for edit unit");
    }

    /// <summary>
    /// Gets the offset to the System packet for an edit unit.
    /// In OP-1a, System packets are at the start of each edit unit.
    /// </summary>
    /// <param name="editUnit">Zero-based edit unit number.</param>
    /// <returns>Absolute byte offset to the System packet.</returns>
    public long GetSystemPacketOffset(int editUnit)
    {
        // In OP-1a interleaved essence, System packet is first in edit unit
        // Add KLV key size (16 bytes) to get to the value
        return GetEditUnitOffset(editUnit) + Constants.KLV_KEY_SIZE;
    }
}
```

**Step 2: Run build**

Run: `dotnet build`

Expected: Build succeeds

**Step 3: Commit**

```bash
git add lib/Core/MxfIndex.cs
git commit -m "$(cat <<'EOF'
feat: add MxfIndex model for frame-level seeking

Supports both CBE (constant byte) and VBE (variable byte) index tables.
Provides GetEditUnitOffset and GetSystemPacketOffset for direct seeking.
EOF
)"
```

---

## Task 9: Add MxfIndex Unit Tests

**Files:**
- Create: `tests/Core/MxfIndexTests.cs`

**Step 1: Write the tests**

```csharp
using nathanbutlerDEV.libopx.Core;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Core;

public class MxfIndexTests
{
    [Fact]
    public void GetEditUnitOffset_ConstantByteSize_CalculatesCorrectly()
    {
        // Arrange
        var index = new MxfIndex
        {
            EditRate = (25, 1),
            BodyPartitionOffset = 1000,
            EditUnitCount = 100,
            IsConstantByteSize = true,
            ConstantEditUnitByteCount = 500,
        };

        // Act & Assert
        Assert.Equal(1000L, index.GetEditUnitOffset(0));
        Assert.Equal(1500L, index.GetEditUnitOffset(1));
        Assert.Equal(2000L, index.GetEditUnitOffset(2));
        Assert.Equal(50500L, index.GetEditUnitOffset(99));
    }

    [Fact]
    public void GetEditUnitOffset_VariableByteSize_UsesStreamOffsets()
    {
        // Arrange
        var index = new MxfIndex
        {
            EditRate = (25, 1),
            BodyPartitionOffset = 1000,
            EditUnitCount = 3,
            IsConstantByteSize = false,
            StreamOffsets = [0, 512, 1536],
        };

        // Act & Assert
        Assert.Equal(1000L, index.GetEditUnitOffset(0));
        Assert.Equal(1512L, index.GetEditUnitOffset(1));
        Assert.Equal(2536L, index.GetEditUnitOffset(2));
    }

    [Fact]
    public void GetEditUnitOffset_OutOfRange_ThrowsException()
    {
        // Arrange
        var index = new MxfIndex
        {
            EditRate = (25, 1),
            BodyPartitionOffset = 1000,
            EditUnitCount = 10,
            IsConstantByteSize = true,
            ConstantEditUnitByteCount = 500,
        };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => index.GetEditUnitOffset(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => index.GetEditUnitOffset(10));
    }

    [Fact]
    public void GetSystemPacketOffset_AddsKlvKeySize()
    {
        // Arrange
        var index = new MxfIndex
        {
            EditRate = (25, 1),
            BodyPartitionOffset = 1000,
            EditUnitCount = 10,
            IsConstantByteSize = true,
            ConstantEditUnitByteCount = 500,
        };

        // Act
        var offset = index.GetSystemPacketOffset(0);

        // Assert - Should add 16 bytes for KLV key
        Assert.Equal(1016L, offset);
    }
}
```

**Step 2: Run tests**

Run: `dotnet test --filter "MxfIndexTests" -v n`

Expected: All tests PASS

**Step 3: Commit**

```bash
git add tests/Core/MxfIndexTests.cs
git commit -m "$(cat <<'EOF'
test: add MxfIndex unit tests

Tests CBE and VBE offset calculations and bounds checking.
EOF
)"
```

---

## Task 10: Add Index Loading to MXF Class

**Files:**
- Modify: `lib/Formats/MXF.cs`

**Step 1: Add Index property and HasIndex**

Add near other properties:

```csharp
/// <summary>
/// The parsed index table, or null if not available.
/// </summary>
public MxfIndex? Index { get; private set; }

/// <summary>
/// Whether this MXF file has a usable index table.
/// </summary>
public bool HasIndex => Index != null;
```

**Step 2: Add LoadIndexTable method**

```csharp
/// <summary>
/// Attempts to load the index table from the footer partition.
/// </summary>
private MxfIndex? LoadIndexTable()
{
    if (Input == null || !Input.CanSeek)
        return null;

    try
    {
        // Save current position
        var originalPosition = Input.Position;

        // Read header partition pack to get footer offset
        Input.Seek(0, SeekOrigin.Begin);

        var keyBuffer = new byte[Constants.KLV_KEY_SIZE];
        if (Input.Read(keyBuffer, 0, Constants.KLV_KEY_SIZE) != Constants.KLV_KEY_SIZE)
            return null;

        // Verify it's a header partition pack
        var keyType = Keys.GetKeyType(keyBuffer);
        if (keyType != KeyType.HeaderPartition)
            return null;

        // Read BER length
        var length = ReadBerLength(Input);
        if (length < 64)
            return null;

        // Read partition pack data
        var packData = new byte[length];
        if (Input.Read(packData, 0, length) != length)
            return null;

        var headerPack = PartitionPack.Parse(packData);
        if (headerPack.FooterPartition == 0)
            return null; // No footer partition

        // Seek to footer partition
        Input.Seek(headerPack.FooterPartition, SeekOrigin.Begin);

        // TODO: Parse footer partition and index table segments
        // This is a placeholder for the full implementation

        // Restore original position
        Input.Seek(originalPosition, SeekOrigin.Begin);
        return null; // Full implementation in next task
    }
    catch
    {
        return null; // Graceful fallback on any error
    }
}
```

**Step 3: Call LoadIndexTable in constructor**

Add after other initialization:

```csharp
Index = LoadIndexTable();
```

**Step 4: Run tests**

Run: `dotnet test --filter "MXFTests" -v n`

Expected: All tests PASS

**Step 5: Commit**

```bash
git add lib/Formats/MXF.cs
git commit -m "$(cat <<'EOF'
feat: add Index property and LoadIndexTable stub to MXF class

Adds HasIndex/Index properties and initial partition pack reading.
Full index table parsing to follow.
EOF
)"
```

---

## Summary

This plan covers:

1. **Tasks 1-4**: Direct-write optimization (Phase 1 complete)
2. **Tasks 5-10**: Index table infrastructure (Phase 2 foundation)

After completing these tasks, the codebase will have:
- Optimized restripe with ~50% fewer I/O operations per frame
- Partition pack and index models ready for use
- Foundation for index-aware restripe (follow-up work)

**Follow-up work (separate plan):**
- Parse index table segments from footer
- Implement `RestripeWithIndexAsync` fast path
- Add index support to extraction/filtering operations
