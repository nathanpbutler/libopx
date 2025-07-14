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