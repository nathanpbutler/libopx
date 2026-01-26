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
