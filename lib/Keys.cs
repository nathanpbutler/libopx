using System;
using nathanbutlerDEV.libopx.SMPTE;

namespace nathanbutlerDEV.libopx;

/// <summary>
/// Contains MXF key definitions and provides key type identification for stream processing.
/// Maps various MXF essence elements and metadata keys to their corresponding types.
/// </summary>
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
    private static readonly byte[] SystemMetadataPackGC = [0x06, 0x0E, 0x2B, 0x34, 0x02, 0x05, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x04, 0x01, 0x01, 0x00];
    /// <summary>
    /// System Stream GC Key
    /// </summary>
    private static readonly byte[] SystemMetadataSetGC = [0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x14, 0x02, 0x01, 0x00];
    /// <summary>
    /// Timecode Component Key
    /// </summary>
    public static readonly byte[] TimecodeComponent = [0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0D, 0x01, 0x01, 0x01, 0x01, 0x01, 0x14, 0x00];
    /// <summary>
    /// Data Stream Key
    /// </summary>
    public static readonly byte[] DataStream = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x17, 0x01];
    /// <summary>
    /// D-10 Video Element Key - TODO: Remove this
    /// </summary>
    private static readonly byte[] D10 = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x05, 0x01, 0x01];
    /// <summary>
    /// Frame-wrapped MPEG Picture Element Key - TODO: Remove this
    /// </summary>
    private static readonly byte[] Video = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x01, 0x05];
    /// <summary>
    /// AES3 Frame-wrapped Sound Element Key
    /// </summary>
    private static readonly byte[] AES3FrameWrappedSoundElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x16, 0x02, 0x03];
    /// <summary>
    /// AES38-Ch Element Key - TODO: Remove this
    /// </summary>
    private static readonly byte[] AES38ChElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x06, 0x01, 0x10];

    #endregion

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

    private static readonly Dictionary<ReadOnlyMemory<byte>, KeyType> KeyPatterns = new()
    {
        { SystemMetadataPackGC.AsMemory(), KeyType.System },
        { SystemMetadataSetGC.AsMemory(), KeyType.System },
        { DataStream.AsMemory(), KeyType.Data },
        { TimecodeComponent.AsMemory(), KeyType.TimecodeComponent },
        { D10.AsMemory(), KeyType.Video },
        { Video.AsMemory(), KeyType.Video },
        { AES3FrameWrappedSoundElement.AsMemory(), KeyType.Audio },
        { AES38ChElement.AsMemory(), KeyType.Audio },
        
        // Audio Essence Elements
        { Essence._8ChAES3Element.AsMemory(), KeyType.Audio },
        { Essence.AES3ClipWrappedSoundElement.AsMemory(), KeyType.Audio },
        { Essence.AES3CustomWrappedSoundElement.AsMemory(), KeyType.Audio },
        { Essence.AES3FrameWrappedSoundElement.AsMemory(), KeyType.Audio },
        { Essence.BWFElement.AsMemory(), KeyType.Audio },
        { Essence.ClipWrappedAlawSoundElement.AsMemory(), KeyType.Audio },
        { Essence.ClipWrappedMGASoundElement.AsMemory(), KeyType.Audio },
        { Essence.ClipWrappedMPEGSoundElement.AsMemory(), KeyType.Audio },
        { Essence.CustomWrappedAlawSoundElement.AsMemory(), KeyType.Audio },
        { Essence.CustomWrappedMPEGSoundElement.AsMemory(), KeyType.Audio },
        { Essence.FrameWrappedAlawSoundElement.AsMemory(), KeyType.Audio },
        { Essence.FrameWrappedMGASoundElement.AsMemory(), KeyType.Audio },
        { Essence.FrameWrappedMPEGSoundElement.AsMemory(), KeyType.Audio },
        { Essence.IMF_IABEssenceClipWrappedElement.AsMemory(), KeyType.Audio },
        { Essence.WaveClipWrappedSoundElement.AsMemory(), KeyType.Audio },
        { Essence.WaveCustomWrappedSoundElement.AsMemory(), KeyType.Audio },
        { Essence.WaveFrameWrappedSoundElement.AsMemory(), KeyType.Audio },
        { Essence.CPSoundItem.AsMemory(), KeyType.Audio },
        { Essence.GCSoundItem.AsMemory(), KeyType.Audio },
        
        // Video Essence Elements
        { Essence.ARRIRAWPictureElement.AsMemory(), KeyType.Video },
        { Essence.ClipWrappedACESPictureElement.AsMemory(), KeyType.Video },
        { Essence.ClipWrappedDNxPackedPictureElement.AsMemory(), KeyType.Video },
        { Essence.ClipWrappedFFV1PictureElement.AsMemory(), KeyType.Video },
        { Essence.ClipWrappedJPEG2000PictureElement.AsMemory(), KeyType.Video },
        { Essence.ClipWrappedJPEGXSPictureElement.AsMemory(), KeyType.Video },
        { Essence.ClipWrappedMPEGPictureElement.AsMemory(), KeyType.Video },
        { Essence.ClipWrappedPictureElement.AsMemory(), KeyType.Video },
        { Essence.ClipWrappedTIFF_EPPictureElement.AsMemory(), KeyType.Video },
        { Essence.ClipWrappedVC1PictureElement.AsMemory(), KeyType.Video },
        { Essence.ClipWrappedVC2PictureElement.AsMemory(), KeyType.Video },
        { Essence.ClipWrappedVC3PictureElement.AsMemory(), KeyType.Video },
        { Essence.ClipWrappedVC5PictureElement.AsMemory(), KeyType.Video },
        { Essence.CustomWrappedMPEGPictureElement.AsMemory(), KeyType.Video },
        { Essence.CustomWrappedVC5PictureElement.AsMemory(), KeyType.Video },
        { Essence.DVDIFClipWrapped.AsMemory(), KeyType.Video },
        { Essence.DVDIFFrameWrapped.AsMemory(), KeyType.Video },
        { Essence.FrameWrappedACESPictureElement.AsMemory(), KeyType.Video },
        { Essence.FrameWrappedDNxPackedPictureElement.AsMemory(), KeyType.Video },
        { Essence.FrameWrappedFFV1PictureElement.AsMemory(), KeyType.Video },
        { Essence.FrameWrappedJPEG2000PictureElement.AsMemory(), KeyType.Video },
        { Essence.FrameWrappedJPEGXSPictureElement.AsMemory(), KeyType.Video },
        { Essence.FrameWrappedMPEGPictureElement.AsMemory(), KeyType.Video },
        { Essence.FrameWrappedPictureElement.AsMemory(), KeyType.Video },
        { Essence.FrameWrappedProResPictureElement.AsMemory(), KeyType.Video },
        { Essence.FrameWrappedTIFF_EPPictureElement.AsMemory(), KeyType.Video },
        { Essence.FrameWrappedVC1PictureElement.AsMemory(), KeyType.Video },
        { Essence.FrameWrappedVC2PictureElement.AsMemory(), KeyType.Video },
        { Essence.FrameWrappedVC3PictureElement.AsMemory(), KeyType.Video },
        { Essence.FrameWrappedVC5PictureElement.AsMemory(), KeyType.Video },
        { Essence.LineWrappedPictureElement.AsMemory(), KeyType.Video },
        { Essence.TypeD10Element.AsMemory(), KeyType.Video },
        { Essence.TypeD11ElementFrameWrapped.AsMemory(), KeyType.Video },
        { Essence.CPPictureItem.AsMemory(), KeyType.Video },
        { Essence.GCPictureItem.AsMemory(), KeyType.Video },
        
        // Data Essence Elements
        { Essence.ANCPacketElement.AsMemory(), KeyType.Data },
        { Essence.AuxDataEssence.AsMemory(), KeyType.Data },
        { Essence.ClipWrappedMPEGDataElement.AsMemory(), KeyType.Data },
        { Essence.ControlElement.AsMemory(), KeyType.Data },
        { Essence.CustomWrappedMPEGDataElement.AsMemory(), KeyType.Data },
        { Essence.FrameWrappedANCDataElement.AsMemory(), KeyType.Data },
        { Essence.FrameWrappedDMCVTElement.AsMemory(), KeyType.Data },
        { Essence.FrameWrappedISXDData.AsMemory(), KeyType.Data },
        { Essence.FrameWrappedMPEGDataElement.AsMemory(), KeyType.Data },
        { Essence.FrameWrappedVBIDataElement.AsMemory(), KeyType.Data },
        { Essence.GeneralDataElement.AsMemory(), KeyType.Data },
        { Essence.JFIFElement.AsMemory(), KeyType.Data },
        { Essence.LineWrappedPictureDataElement.AsMemory(), KeyType.Data },
        { Essence.LineWrappedPictureHANCDataElement.AsMemory(), KeyType.Data },
        { Essence.LineWrappedPictureVANCDataElement.AsMemory(), KeyType.Data },
        { Essence.PHDRImageMetadataItem.AsMemory(), KeyType.Data },
        { Essence.SupplementalDataElement.AsMemory(), KeyType.Data },
        { Essence.TIFFElement.AsMemory(), KeyType.Data },
        { Essence.TimedTextDataElement.AsMemory(), KeyType.Data },
        { Essence.VBILineElement.AsMemory(), KeyType.Data },
        { Essence.CPDataItem.AsMemory(), KeyType.Data },
        { Essence.GCDataItem.AsMemory(), KeyType.Data },
        { Essence.GCCompoundItem.AsMemory(), KeyType.Data },
        
        // System Essence Elements
        { Essence.AAFAssociation.AsMemory(), KeyType.System },
        { Essence.ESSENCEDICTIONARY.AsMemory(), KeyType.System },
        { Essence.GenericContainerApplication.AsMemory(), KeyType.System },
        { Essence.GenericContainerVersion.AsMemory(), KeyType.System },
        { Essence.OrganizationallyRegisteredforPublicUse.AsMemory(), KeyType.System },

        // Partition Pack Keys
        { HeaderPartitionPackOpenIncomplete.AsMemory(), KeyType.HeaderPartition },
        { HeaderPartitionPackClosedIncomplete.AsMemory(), KeyType.HeaderPartition },
        { HeaderPartitionPackOpenComplete.AsMemory(), KeyType.HeaderPartition },
        { HeaderPartitionPackClosedComplete.AsMemory(), KeyType.HeaderPartition },
        { FooterPartitionPackClosedComplete.AsMemory(), KeyType.FooterPartition },

        // Index Table Keys
        { IndexTableSegment.AsMemory(), KeyType.IndexTableSegment },
    };

    /// <summary>
    /// Determines the key type from an MXF key byte sequence.
    /// </summary>
    /// <param name="key">The MXF key bytes to analyze</param>
    /// <returns>The identified key type, or Unknown if not recognized</returns>
    /// <exception cref="IOException">Thrown when the key doesn't start with valid MXF FourCC</exception>
    public static KeyType GetKeyType(ReadOnlySpan<byte> key)
    {
        // Initial check to see if our time is being wasted on a non-MXF file :D
        if (!key[..FourCc.Length].SequenceEqual(FourCc))
        {
            throw new IOException("The file is not an MXF file.");
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

/// <summary>
/// Enumeration of MXF key types for categorizing essence elements and metadata.
/// </summary>
 public enum KeyType
{
    /// <summary>
    /// Data essence elements including VBI, ANC, and auxiliary data.
    /// </summary>
    Data,
    /// <summary>
    /// Video essence elements including various codecs and picture formats.
    /// </summary>
    Video,
    /// <summary>
    /// System metadata elements and organizational keys.
    /// </summary>
    System,
    /// <summary>
    /// Timecode component metadata for temporal synchronization.
    /// </summary>
    TimecodeComponent,
    /// <summary>
    /// Audio essence elements including various audio formats and codecs.
    /// </summary>
    Audio,
    /// <summary>
    /// Header partition pack key.
    /// </summary>
    HeaderPartition,
    /// <summary>
    /// Footer partition pack key.
    /// </summary>
    FooterPartition,
    /// <summary>
    /// Index table segment key.
    /// </summary>
    IndexTableSegment,
    /// <summary>
    /// Unknown or unrecognized key type.
    /// </summary>
    Unknown
}