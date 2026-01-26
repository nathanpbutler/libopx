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
    /// Index SID (Stream ID).
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
