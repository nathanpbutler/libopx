using System;

namespace nathanbutlerDEV.libopx;

public class Keys
{
    #region MXF Keys
    /// <summary>
    /// FourCC Key
    /// </summary>
    public static readonly byte[] FourCc = [0x06, 0x0E, 0x2B, 0x34];
    /// <summary>
    /// System Stream Key
    /// </summary>
    private static readonly byte[] SystemStream = [0x06, 0x0E, 0x2B, 0x34, 0x02, 0x05, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x04, 0x01, 0x01, 0x00];
    /// <summary>
    /// Timecode Component Key
    /// </summary>
    public static readonly byte[] TimecodeComponent = [0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0D, 0x01, 0x01, 0x01, 0x01, 0x01, 0x14, 0x00];
    /// <summary>
    /// Data Stream Key
    /// </summary>
    public static readonly byte[] DataStream = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x17, 0x01];
    /// <summary>
    /// D-10 Video Element Key
    /// </summary>
    private static readonly byte[] D10 = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x05, 0x01, 0x01];
    /// <summary>
    /// Frame-wrapped MPEG Picture Element Key
    /// </summary>
    private static readonly byte[] Video = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x01, 0x05];
    /// <summary>
    /// AES3 Frame-wrapped Sound Element Key
    /// </summary>
    private static readonly byte[] AES3FrameWrappedSoundElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x16, 0x02, 0x03];
    /// <summary>
    /// AES38-Ch Element Key
    /// </summary>
    private static readonly byte[] AES38ChElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x06, 0x01, 0x10];

    #endregion

    private static readonly Dictionary<ReadOnlyMemory<byte>, KeyType> KeyPatterns = new()
    {
        { SystemStream.AsMemory(), KeyType.System },
        { DataStream.AsMemory(), KeyType.Data },
        { TimecodeComponent.AsMemory(), KeyType.TimecodeComponent },
        { D10.AsMemory(), KeyType.Video },
        { Video.AsMemory(), KeyType.Video },
        { AES3FrameWrappedSoundElement.AsMemory(), KeyType.Audio },
        { AES38ChElement.AsMemory(), KeyType.Audio }
    };

    public static KeyType GetKeyType(ReadOnlySpan<byte> key)
    {
        // Initial check to see if our time is being wasted on a non-MXF file :D
        if (!key[..FourCc.Length].SequenceEqual(FourCc))
        {
            throw new IOException("The file is not an MXF file.");
        }

        // Fast path for most common keys - check specific bytes directly to avoid dictionary lookup
        
        // Check for System Stream first (most common during SMPTE parsing)
        // 06 0E 2B 34 02 05 01 01 0D 01 03 01 04 01 01 00
        if (key.Length >= 16 &&
            key[4] == 0x02 && key[5] == 0x05 && key[6] == 0x01 && key[7] == 0x01 &&
            key[8] == 0x0D && key[9] == 0x01 && key[10] == 0x03 && key[11] == 0x01 &&
            key[12] == 0x04 && key[13] == 0x01 && key[14] == 0x01 && key[15] == 0x00)
        {
            return KeyType.System;
        }
        
        // Check for Timecode Component
        // 06 0E 2B 34 02 53 01 01 0D 01 01 01 01 01 14 00
        if (key.Length >= 16 &&
            key[4] == 0x02 && key[5] == 0x53 && key[6] == 0x01 && key[7] == 0x01 &&
            key[8] == 0x0D && key[9] == 0x01 && key[10] == 0x01 && key[11] == 0x01 &&
            key[12] == 0x01 && key[13] == 0x01 && key[14] == 0x14 && key[15] == 0x00)
        {
            return KeyType.TimecodeComponent;
        }
        
        // Check for Data Stream (14 bytes)
        // 06 0E 2B 34 01 02 01 01 0D 01 03 01 17 01
        if (key.Length >= 14 &&
            key[4] == 0x01 && key[5] == 0x02 && key[6] == 0x01 && key[7] == 0x01 &&
            key[8] == 0x0D && key[9] == 0x01 && key[10] == 0x03 && key[11] == 0x01 &&
            key[12] == 0x17 && key[13] == 0x01)
        {
            return KeyType.Data;
        }
        
        // Fallback to dictionary lookup for less common keys
        foreach (var pattern in KeyPatterns)
        {
            if (key[..pattern.Key.Length].SequenceEqual(pattern.Key.Span))
            {
                return pattern.Value;
            }
        }
        
        return KeyType.Unknown;
    }
}

public enum KeyType
{
    Data,
    Video,
    System,
    TimecodeComponent, // TODO: Change this to metadata?
    Audio,
    Unknown
}