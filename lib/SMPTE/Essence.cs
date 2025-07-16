namespace nathanbutlerDEV.libopx.SMPTE;
public class Essence
{
    /// <summary>
    /// 8-Ch AES3 Element
    /// </summary>
    /// <remarks>
    /// Identifies a 8 channel AES3 audio data element
    /// </remarks>
    public static readonly byte[] _8ChAES3Element = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x06, 0x01, 0x10, 0x7F];
    /// <summary>
    /// AAF Association
    /// </summary>
    /// <remarks>
    /// Metadata registered by the AAF Association for public use
    /// </remarks>
    public static readonly byte[] AAFAssociation = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    /// <summary>
    /// AES3 Clip-wrapped Sound Element
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped AES3 sound element
    /// </remarks>
    public static readonly byte[] AES3ClipWrappedSoundElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x16, 0x7F, 0x04, 0x7F];
    /// <summary>
    /// AES3 Custom-wrapped Sound Element
    /// </summary>
    /// <remarks>
    /// Identifies a custom-wrapped AES3 coded sound element
    /// </remarks>
    public static readonly byte[] AES3CustomWrappedSoundElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x16, 0x7F, 0x0C, 0x7F];
    /// <summary>
    /// AES3 Frame-wrapped Sound Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped AES3 sound element
    /// </remarks>
    public static readonly byte[] AES3FrameWrappedSoundElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x16, 0x7F, 0x03, 0x7F];
    /// <summary>
    /// ANC Packet Element
    /// </summary>
    /// <remarks>
    /// Identifies a Ancillary Packet data element
    /// </remarks>
    public static readonly byte[] ANCPacketElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x07, 0x7F, 0x21, 0x7F];
    /// <summary>
    /// ARRIRAW Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies an ARRIRAW Picture Element
    /// </remarks>
    public static readonly byte[] ARRIRAWPictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x1C, 0x7F];
    /// <summary>
    /// Aux Data Essence
    /// </summary>
    /// <remarks>
    /// Identifies an Auxiliary Data Essence element
    /// </remarks>
    public static readonly byte[] AuxDataEssence = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x17, 0x7F, 0x0D, 0x7F];
    /// <summary>
    /// BWF Element
    /// </summary>
    /// <remarks>
    /// Identifies a VBI line data element
    /// </remarks>
    public static readonly byte[] BWFElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x07, 0x7F, 0x40, 0x7F];
    /// <summary>
    /// Clip-wrapped ACES Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped ACES Picture Element
    /// </remarks>
    public static readonly byte[] ClipWrappedACESPictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x13, 0x7F];
    /// <summary>
    /// Clip-wrapped A-law Sound Element
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped A-law coded sound element
    /// </remarks>
    public static readonly byte[] ClipWrappedAlawSoundElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x16, 0x7F, 0x09, 0x7F];
    /// <summary>
    /// Clip-wrapped DNxPacked Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies Clip-wrapped DNxPacked Picture Element
    /// </remarks>
    public static readonly byte[] ClipWrappedDNxPackedPictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x19, 0x7F];
    /// <summary>
    /// Clip-wrapped FFV1 Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped FFV1 Picture Element
    /// </remarks>
    public static readonly byte[] ClipWrappedFFV1PictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x1E, 0x7F];
    /// <summary>
    /// Clip-wrapped JPEG 2000 Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped JPEG 2000 Picture Element
    /// </remarks>
    public static readonly byte[] ClipWrappedJPEG2000PictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x09, 0x7F];
    /// <summary>
    /// Clip-wrapped JPEG XS Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped JPEG XS Picture Element
    /// </remarks>
    public static readonly byte[] ClipWrappedJPEGXSPictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x1B, 0x7F];
    /// <summary>
    /// Clip-wrapped MGA Sound Element
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped MGA Sound Element
    /// </remarks>
    public static readonly byte[] ClipWrappedMGASoundElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x16, 0x7F, 0x0F, 0x7F];
    /// <summary>
    /// Clip-wrapped MPEG Data Element
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped MPEG data element
    /// </remarks>
    public static readonly byte[] ClipWrappedMPEGDataElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x17, 0x7F, 0x06, 0x7F];
    /// <summary>
    /// Clip-wrapped MPEG Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped MPEG picture element
    /// </remarks>
    public static readonly byte[] ClipWrappedMPEGPictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x06, 0x7F];
    /// <summary>
    /// Clip-wrapped MPEG Sound Element
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped MPEG coded sound element
    /// </remarks>
    public static readonly byte[] ClipWrappedMPEGSoundElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x16, 0x7F, 0x06, 0x7F];
    /// <summary>
    /// Clip-wrapped Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped uncompressed picture element
    /// </remarks>
    public static readonly byte[] ClipWrappedPictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x03, 0x7F];
    /// <summary>
    /// Clip-wrapped TIFF/EP Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped TIFF/EP Picture Element
    /// </remarks>
    public static readonly byte[] ClipWrappedTIFF_EPPictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x0F, 0x7F];
    /// <summary>
    /// Clip-wrapped VC-1 Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped VC-1 Picture Element
    /// </remarks>
    public static readonly byte[] ClipWrappedVC1PictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x0B, 0x7F];
    /// <summary>
    /// Clip-wrapped VC-2 Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped VC-2 Picture Element
    /// </remarks>
    public static readonly byte[] ClipWrappedVC2PictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x11, 0x7F];
    /// <summary>
    /// Clip-wrapped VC-3 Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped VC-3 Picture Element
    /// </remarks>
    public static readonly byte[] ClipWrappedVC3PictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x0D, 0x7F];
    /// <summary>
    /// Clip-wrapped VC-5 Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped VC-5 Picture Element
    /// </remarks>
    public static readonly byte[] ClipWrappedVC5PictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x15, 0x7F];
    /// <summary>
    /// Control Element
    /// </summary>
    /// <remarks>
    /// Identifies a Control data element
    /// </remarks>
    public static readonly byte[] ControlElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x07, 0x7F, 0x78, 0x7F];
    /// <summary>
    /// CP Data Item
    /// </summary>
    /// <remarks>
    /// Identifies CP-compatible Data Item
    /// </remarks>
    public static readonly byte[] CPDataItem = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x07, 0x00, 0x00, 0x00];
    /// <summary>
    /// CP Picture Item
    /// </summary>
    /// <remarks>
    /// Identifies CP-compatible Picture item
    /// </remarks>
    public static readonly byte[] CPPictureItem = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x05, 0x00, 0x00, 0x00];
    /// <summary>
    /// CP Sound Item
    /// </summary>
    /// <remarks>
    /// Identifies CP-compatible Sound Item
    /// </remarks>
    public static readonly byte[] CPSoundItem = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x06, 0x00, 0x00, 0x00];
    /// <summary>
    /// Custom-wrapped A-law Sound Element
    /// </summary>
    /// <remarks>
    /// Identifies a custom-wrapped A-law coded sound element
    /// </remarks>
    public static readonly byte[] CustomWrappedAlawSoundElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x16, 0x7F, 0x0A, 0x7F];
    /// <summary>
    /// Custom-wrapped MPEG Data Element
    /// </summary>
    /// <remarks>
    /// Identifies a custom-wrapped MPEG data element
    /// </remarks>
    public static readonly byte[] CustomWrappedMPEGDataElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x17, 0x7F, 0x07, 0x7F];
    /// <summary>
    /// Custom-wrapped MPEG Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a custom-wrapped MPEG picture element
    /// </remarks>
    public static readonly byte[] CustomWrappedMPEGPictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x07, 0x7F];
    /// <summary>
    /// Custom-wrapped MPEG Sound Element
    /// </summary>
    /// <remarks>
    /// Identifies a custom-wrapped MPEG coded sound element
    /// </remarks>
    public static readonly byte[] CustomWrappedMPEGSoundElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x16, 0x7F, 0x07, 0x7F];
    /// <summary>
    /// Custom-wrapped VC-5 Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a custom-wrapped VC-5 Picture Element
    /// </remarks>
    public static readonly byte[] CustomWrappedVC5PictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x16, 0x7F];
    /// <summary>
    /// DV-DIF Clip-wrapped
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped DV-DIF compound element
    /// </remarks>
    public static readonly byte[] DVDIFClipWrapped = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x18, 0x7F, 0x02, 0x7F];
    /// <summary>
    /// DV-DIF Frame-wrapped
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped DV-DIF compound element
    /// </remarks>
    public static readonly byte[] DVDIFFrameWrapped = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x18, 0x7F, 0x01, 0x7F];
    /// <summary>
    /// ESSENCE DICTIONARY
    /// </summary>
    /// <remarks>
    /// CATALOGUE OF ESSENCE ITEMS
    /// </remarks>
    public static readonly byte[] ESSENCEDICTIONARY = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    /// <summary>
    /// Frame-wrapped ACES Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped ACES Picture Element
    /// </remarks>
    public static readonly byte[] FrameWrappedACESPictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x12, 0x7F];
    /// <summary>
    /// Frame-wrapped A-law Sound Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped A-law coded sound element
    /// </remarks>
    public static readonly byte[] FrameWrappedAlawSoundElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x16, 0x7F, 0x08, 0x7F];
    /// <summary>
    /// Frame-Wrapped ANC Data Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-Wrapped ANC Data Element
    /// </remarks>
    public static readonly byte[] FrameWrappedANCDataElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x17, 0x7F, 0x02, 0x01];
    /// <summary>
    /// Frame-wrapped DMCVT Element
    /// </summary>
    /// <remarks>
    /// Identifies Frame-wrapped DMCVT Element
    /// </remarks>
    public static readonly byte[] FrameWrappedDMCVTElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x17, 0x7F, 0x0E, 0x7F];
    /// <summary>
    /// Frame-wrapped DNxPacked Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies Frame-wrapped DNxPacked Picture Element
    /// </remarks>
    public static readonly byte[] FrameWrappedDNxPackedPictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x18, 0x7F];
    /// <summary>
    /// Frame-wrapped FFV1 Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped FFV1 Picture Element
    /// </remarks>
    public static readonly byte[] FrameWrappedFFV1PictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x1D, 0x7F];
    /// <summary>
    /// Frame Wrapped ISXD Data
    /// </summary>
    /// <remarks>
    /// Identifies Frame Wrapped ISXD Data Essence
    /// </remarks>
    public static readonly byte[] FrameWrappedISXDData = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x05, 0x0E, 0x09, 0x05, 0x02, 0x01, 0x7F, 0x01, 0x7F];
    /// <summary>
    /// Frame-wrapped JPEG 2000 Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped JPEG 2000 Picture Element
    /// </remarks>
    public static readonly byte[] FrameWrappedJPEG2000PictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x08, 0x7F];
    /// <summary>
    /// Frame-wrapped JPEG XS Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped JPEG XS Picture Element
    /// </remarks>
    public static readonly byte[] FrameWrappedJPEGXSPictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x1A, 0x7F];
    /// <summary>
    /// Frame-wrapped MGA Sound Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped MGA Sound Element
    /// </remarks>
    public static readonly byte[] FrameWrappedMGASoundElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x16, 0x7F, 0x0E, 0x7F];
    /// <summary>
    /// Frame-wrapped MPEG Data Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped MPEG data element
    /// </remarks>
    public static readonly byte[] FrameWrappedMPEGDataElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x17, 0x7F, 0x05, 0x7F];
    /// <summary>
    /// Frame-wrapped MPEG Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped MPEG picture element
    /// </remarks>
    public static readonly byte[] FrameWrappedMPEGPictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x05, 0x7F];
    /// <summary>
    /// Frame-wrapped MPEG Sound Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped MPEG coded sound element
    /// </remarks>
    public static readonly byte[] FrameWrappedMPEGSoundElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x16, 0x7F, 0x05, 0x7F];
    /// <summary>
    /// Frame-wrapped Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped uncompressed picture element
    /// </remarks>
    public static readonly byte[] FrameWrappedPictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x02, 0x7F];
    /// <summary>
    /// Frame-wrapped ProRes Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped ProRes Picture Element
    /// </remarks>
    public static readonly byte[] FrameWrappedProResPictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x17, 0x7F];
    /// <summary>
    /// Frame-wrapped TIFF/EP Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped TIFF/EP Picture Element
    /// </remarks>
    public static readonly byte[] FrameWrappedTIFF_EPPictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x0E, 0x7F];
    /// <summary>
    /// Frame-Wrapped VBI Data Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-Wrapped VBI Data Element
    /// </remarks>
    public static readonly byte[] FrameWrappedVBIDataElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x17, 0x7F, 0x01, 0x01];
    /// <summary>
    /// Frame-wrapped VC-1 Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped VC-1 Picture Element
    /// </remarks>
    public static readonly byte[] FrameWrappedVC1PictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x0A, 0x7F];
    /// <summary>
    /// Frame-wrapped VC-2 Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped VC-2 Picture Element
    /// </remarks>
    public static readonly byte[] FrameWrappedVC2PictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x10, 0x7F];
    /// <summary>
    /// Frame-wrapped VC-3 Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped VC-3 Picture Element
    /// </remarks>
    public static readonly byte[] FrameWrappedVC3PictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x0C, 0x7F];
    /// <summary>
    /// Frame-wrapped VC-5 Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped VC-5 Picture Element
    /// </remarks>
    public static readonly byte[] FrameWrappedVC5PictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x14, 0x7F];
    /// <summary>
    /// GC Compound Item
    /// </summary>
    /// <remarks>
    /// Key values for GC Compound Elements
    /// </remarks>
    public static readonly byte[] GCCompoundItem = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x18, 0x00, 0x00, 0x00];
    /// <summary>
    /// GC Data Item
    /// </summary>
    /// <remarks>
    /// Key values for GC Data Elements
    /// </remarks>
    public static readonly byte[] GCDataItem = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x17, 0x00, 0x00, 0x00];
    /// <summary>
    /// GC Picture Item
    /// </summary>
    /// <remarks>
    /// Identifies GC Picture Item
    /// </remarks>
    public static readonly byte[] GCPictureItem = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x00, 0x00, 0x00];
    /// <summary>
    /// GC Sound Item
    /// </summary>
    /// <remarks>
    /// Key values for GC Sound Elements
    /// </remarks>
    public static readonly byte[] GCSoundItem = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x16, 0x00, 0x00, 0x00];
    /// <summary>
    /// General Data Element
    /// </summary>
    /// <remarks>
    /// Identifies a General data element
    /// </remarks>
    public static readonly byte[] GeneralDataElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x07, 0x7F, 0x22, 0x7F];
    /// <summary>
    /// Generic Container Application
    /// </summary>
    /// <remarks>
    /// MXF Generic Container Application
    /// </remarks>
    public static readonly byte[] GenericContainerApplication = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00];
    /// <summary>
    /// Generic Container Version
    /// </summary>
    /// <remarks>
    /// Version 1 of the MXF Generic Container
    /// </remarks>
    public static readonly byte[] GenericContainerVersion = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x00, 0x00, 0x00, 0x00];
    /// <summary>
    /// IMF IAB Essence Clip-Wrapped Element
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped Immersive Audio Bitstream sound element
    /// </remarks>
    public static readonly byte[] IMF_IABEssenceClipWrappedElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x16, 0x7F, 0x0D, 0x7F];
    /// <summary>
    /// JFIF Element
    /// </summary>
    /// <remarks>
    /// Identifies a JFIF data element
    /// </remarks>
    public static readonly byte[] JFIFElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x07, 0x7F, 0x41, 0x7F];
    /// <summary>
    /// Line-wrapped Picture Data Element
    /// </summary>
    /// <remarks>
    /// Identifies a line-wrapped picture data element
    /// </remarks>
    public static readonly byte[] LineWrappedPictureDataElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x17, 0x7F, 0x08, 0x7F];
    /// <summary>
    /// Line-wrapped Picture Element
    /// </summary>
    /// <remarks>
    /// Identifies a line-wrapped uncompressed picture element
    /// </remarks>
    public static readonly byte[] LineWrappedPictureElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x7F, 0x04, 0x7F];
    /// <summary>
    /// Line-wrapped Picture H-ANC Data Element
    /// </summary>
    /// <remarks>
    /// Identifies a line-wrapped picture H-Anc packet data element
    /// </remarks>
    public static readonly byte[] LineWrappedPictureHANCDataElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x17, 0x7F, 0x0A, 0x7F];
    /// <summary>
    /// Line-wrapped Picture V-ANC Data Element
    /// </summary>
    /// <remarks>
    /// Identifies a line-wrapped picture V-Anc packet data element
    /// </remarks>
    public static readonly byte[] LineWrappedPictureVANCDataElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x17, 0x7F, 0x09, 0x7F];
    /// <summary>
    /// Organizationally Registered for Public Use
    /// </summary>
    /// <remarks>
    /// Organizationally Registered for Public Use
    /// </remarks>
    public static readonly byte[] OrganizationallyRegisteredforPublicUse = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    /// <summary>
    /// PHDR Image Metadata Item
    /// </summary>
    /// <remarks>
    /// Identifies a PHDR image metadata item
    /// </remarks>
    public static readonly byte[] PHDRImageMetadataItem = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x05, 0x0E, 0x09, 0x06, 0x07, 0x01, 0x7F, 0x01, 0x7F];
    /// <summary>
    /// Supplemental Data Element
    /// </summary>
    /// <remarks>
    /// Identifies a Supplemental Data Element
    /// </remarks>
    public static readonly byte[] SupplementalDataElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x17, 0x7F, 0x0F, 0x7F];
    /// <summary>
    /// TIFF Element
    /// </summary>
    /// <remarks>
    /// Identifies a TIFF data element
    /// </remarks>
    public static readonly byte[] TIFFElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x07, 0x7F, 0x42, 0x7F];
    /// <summary>
    /// Timed Text Data Element
    /// </summary>
    /// <remarks>
    /// Identifies a timed text data element
    /// </remarks>
    public static readonly byte[] TimedTextDataElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x17, 0x7F, 0x0B, 0x7F];
    /// <summary>
    /// Type D-10 Element
    /// </summary>
    /// <remarks>
    /// Identifies a Type D-10 constrained MPEG2 4:2:2 coded element (see SMPTE 331)
    /// </remarks>
    public static readonly byte[] TypeD10Element = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x05, 0x01, 0x01, 0x7F];
    /// <summary>
    /// Type D-11 Element (Frame-Wrapped)
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped Type D-11 picture element
    /// </remarks>
    public static readonly byte[] TypeD11ElementFrameWrapped = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x15, 0x01, 0x01, 0x01];
    /// <summary>
    /// VBI Line Element
    /// </summary>
    /// <remarks>
    /// Identifies a VBI line data element
    /// </remarks>
    public static readonly byte[] VBILineElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x07, 0x7F, 0x20, 0x7F];
    /// <summary>
    /// Wave Clip-wrapped Sound Element
    /// </summary>
    /// <remarks>
    /// Identifies a clip-wrapped Broadcast Wave sound element
    /// </remarks>
    public static readonly byte[] WaveClipWrappedSoundElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x16, 0x7F, 0x02, 0x7F];
    /// <summary>
    /// Wave Custom-wrapped Sound Element
    /// </summary>
    /// <remarks>
    /// Identifies a custom-wrapped Wav coded sound element
    /// </remarks>
    public static readonly byte[] WaveCustomWrappedSoundElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x16, 0x7F, 0x0B, 0x7F];
    /// <summary>
    /// Wave Frame-wrapped Sound Element
    /// </summary>
    /// <remarks>
    /// Identifies a frame-wrapped Broadcast Wave sound element
    /// </remarks>
    public static readonly byte[] WaveFrameWrappedSoundElement = [0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0x16, 0x7F, 0x01, 0x7F];
}

