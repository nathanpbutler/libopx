using System;

namespace nathanbutlerDEV.libopx;

/// <summary>
/// Contains all constant values used throughout the libopx library for MXF, VBI, T42, and teletext processing.
/// </summary>
public class Constants
{
    #region MXF Constants
    /// <summary>
    /// Size of a KLV (Key-Length-Value) key in bytes.
    /// </summary>
    public const int KLV_KEY_SIZE = 16;
    /// <summary>
    /// System metadata pack global class identifier.
    /// </summary>
    public const int SYSTEM_METADATA_PACK_GC = 41;
    /// <summary>
    /// Offset to system metadata set global class within the pack.
    /// </summary>
    public const int SYSTEM_METADATA_SET_GC_OFFSET = 12;
    /// <summary>
    /// Size of SMPTE timecode data in bytes.
    /// </summary>
    public const int SMPTE_TIMECODE_SIZE = 4;
    #endregion

    #region Packet Constants
    /// <summary>
    /// Size of the packet header in bytes.
    /// </summary>
    public const int PACKET_HEADER_SIZE = 2;
    #endregion
    #region Line Constants
    /// <summary>
    /// Size of the line header in bytes.
    /// </summary>
    public const int LINE_HEADER_SIZE = 14;
    #endregion
    #region VBI Constants
    /// <summary>
    /// Size of a VBI (Vertical Blanking Interval) line in bytes.
    /// </summary>
    public const int VBI_LINE_SIZE = 720;
    /// <summary>
    /// Size of a double VBI line in bytes.
    /// </summary>
    public const int VBI_DOUBLE_LINE_SIZE = 1440;
    /// <summary>
    /// Default threshold value for VBI signal detection.
    /// </summary>
    public const float VBI_DEFAULT_THRESHOLD = 0.40f;
    /// <summary>
    /// Bit mask used for parity flip operations in VBI processing.
    /// </summary>
    public const int VBI_PARITY_FLIP_MASK = 0x80;
    /// <summary>
    /// Maximum range for offset calculations in VBI data.
    /// </summary>
    public const int VBI_MAX_OFFSET_RANGE = 100;
    /// <summary>
    /// Maximum search range for offset detection in VBI data.
    /// </summary>
    public const int VBI_MAX_OFFSET_SEARCH = 100;
    /// <summary>
    /// First clock signal offset position in VBI data.
    /// </summary>
    public const int VBI_CLOCK_OFFSET_1 = 8;
    /// <summary>
    /// First framing code offset position in VBI data.
    /// </summary>
    public const int VBI_FRAMING_OFFSET_1 = 39;
    /// <summary>
    /// Second framing code offset position in VBI data.
    /// </summary>
    public const int VBI_FRAMING_OFFSET_2 = 40;
    /// <summary>
    /// Parity lookup table for O(1) parity checking.
    /// Returns true (1) if byte has odd parity (odd number of set bits).
    /// Used for VBI decoding error correction.
    /// </summary>
    public static readonly byte[] VBI_PARITY_TABLE =
    [
        // 0x00-0x0F
        0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
        // 0x10-0x1F
        1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
        // 0x20-0x2F
        1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
        // 0x30-0x3F
        0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
        // 0x40-0x4F
        1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
        // 0x50-0x5F
        0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
        // 0x60-0x6F
        0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
        // 0x70-0x7F
        1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
        // 0x80-0x8F
        1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
        // 0x90-0x9F
        0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
        // 0xA0-0xAF
        0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
        // 0xB0-0xBF
        1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
        // 0xC0-0xCF
        0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
        // 0xD0-0xDF
        1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
        // 0xE0-0xEF
        1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
        // 0xF0-0xFF
        0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0
    ];
    #endregion

    #region T42toVBI Constants
    /// <summary>
    /// Low signal value used for VBI padding and signal generation.
    /// </summary>
    public const byte VBI_LOW_VALUE = 0x10;
    /// <summary>
    /// High signal value used for VBI padding and signal generation.
    /// </summary>
    public const byte VBI_HIGH_VALUE = 0xEB;
    /// <summary>
    /// Starting position for padding in VBI data.
    /// </summary>
    public const int VBI_PAD_START = 6;
    /// <summary>
    /// Size of VBI bit data in the conversion process.
    /// </summary>
    public const int VBI_BITS_SIZE = 360;
    /// <summary>
    /// Size of padding applied to VBI data.
    /// </summary>
    public const int VBI_PADDING_SIZE = 16;
    /// <summary>
    /// Pre-allocated padding bytes for VBI data generation.
    /// </summary>
    public static readonly byte[] VBI_PADDING_BYTES = [.. Enumerable.Repeat(VBI_LOW_VALUE, VBI_PADDING_SIZE)];
    /// <summary>
    /// Target size for resized VBI data during conversion.
    /// </summary>
    public const int VBI_RESIZE_SIZE = 701;
    /// <summary>
    /// Pre-allocated array for resized VBI data during processing.
    /// </summary>
    public static readonly byte[] VBI_RESIZE_BYTES = new byte[VBI_RESIZE_SIZE];
    /// <summary>
    /// Scale factor for converting VBI bits to T42 line size.
    /// </summary>
    public const float VBI_SCALE = (float)VBI_BITS_SIZE / VBI_RESIZE_SIZE;
    #endregion

    #region T42 Constants
    /// <summary>
    /// T42 CRIFC (Clock, Clock, Framing Code) byte sequence for teletext synchronization.
    /// </summary>
    public static readonly byte[] T42_CRIFC = [0x55, 0x55, 0x27];
    /// <summary>
    /// Size of a T42 teletext line in bytes.
    /// </summary>
    public const int T42_LINE_SIZE = 42;
    /// <summary>
    /// Clock synchronization byte value for T42 format.
    /// </summary>
    public const byte T42_CLOCK_BYTE = 0x55;
    /// <summary>
    /// Framing code byte value for T42 format synchronization.
    /// </summary>
    public const byte T42_FRAMING_CODE = 0x27;
    /// <summary>
    /// T42 line size plus framing code byte.
    /// </summary>
    public const int T42_PLUS_FRAMING = 43;
    /// <summary>
    /// T42 line size plus complete CRIFC sequence.
    /// </summary>
    public const int T42_PLUS_CRIFC = 45;
    /// <summary>
    /// Number of bits per byte used in T42 teletext encoding.
    /// </summary>
    public const int T42_BITS_PER_BYTE = 28;
    /// <summary>
    /// Normal byte step size for T42 processing.
    /// </summary>
    public const int T42_BYTE_STEP_NORMAL = 31;
    /// <summary>
    /// Extended byte step size for T42 processing.
    /// </summary>
    public const int T42_BYTE_STEP_EXTENDED = 32;
    /// <summary>
    /// Bit mask used for parity flip operations in T42 processing.
    /// </summary>
    public const int T42_PARITY_FLIP_MASK = 0x80;
    /// <summary>
    /// Default magazine number used when magazine field is 0 in T42 data.
    /// </summary>
    public const int T42_DEFAULT_MAGAZINE = 8;
    /// <summary>
    /// Bit mask for extracting magazine bits from T42 data.
    /// </summary>
    public const int T42_MAGAZINE_MASK = 0x7;
    /// <summary>
    /// Bit shift value for row extraction from Hamming 16-bit decode.
    /// </summary>
    public const int T42_ROW_SHIFT = 3;
    /// <summary>
    /// High nibble shift value for Hamming 16-bit decode operations.
    /// </summary>
    public const int T42_HIGH_NIBBLE_SHIFT = 4;
    /// <summary>
    /// Minimum number of bytes required for row extraction from T42 data.
    /// </summary>
    public const int T42_MIN_BYTES_FOR_ROW = 2;
    /// <summary>
    /// Block start byte value for T42 teletext blocks.
    /// </summary>
    public const byte T42_BLOCK_START_BYTE = 0x0B;
    /// <summary>
    /// Background control character code for T42 teletext display (New Background - 0x1D).
    /// Sets background color to current foreground color.
    /// </summary>
    public const byte T42_BACKGROUND_CONTROL = 0x1D;
    /// <summary>
    /// Black background control character code for T42 teletext display (0x1C).
    /// Resets background to black.
    /// </summary>
    public const byte T42_BLACK_BACKGROUND = 0x1C;
    /// <summary>
    /// Normal height control character code for T42 teletext display (End Box - 0x0A).
    /// </summary>
    public const byte T42_NORMAL_HEIGHT = 0x0A;
    /// <summary>
    /// Graphics foreground color range start (0x10).
    /// </summary>
    public const byte T42_GRAPHICS_COLOR_START = 0x10;
    /// <summary>
    /// Graphics foreground color range end (0x17).
    /// </summary>
    public const byte T42_GRAPHICS_COLOR_END = 0x17;
    /// <summary>
    /// ANSI escape sequence for resetting terminal formatting.
    /// </summary>
    public const string T42_ANSI_RESET = "\x1b[0m";
    /// <summary>
    /// Teletext 3-bit colors mapped to ANSI 256-color palette for terminal-theme-independent display.
    /// Index: 0=Black, 1=Red, 2=Green, 3=Yellow, 4=Blue, 5=Magenta, 6=Cyan, 7=White
    /// These explicit color codes ensure consistent display regardless of terminal color scheme.
    /// </summary>
    public static readonly int[] T42_ANSI_256_COLORS = [16, 196, 46, 226, 21, 201, 51, 231];
    /// <summary>
    /// ANSI escape sequence for default T42 colors using 256-color palette (white foreground, black background).
    /// Uses explicit 256-color codes to ensure colors are not affected by terminal themes.
    /// </summary>
    public const string T42_DEFAULT_COLORS = "\x1b[38;5;231m\x1b[48;5;16m";
    /// <summary>
    /// Pre-formatted blank line with default T42 colors and spacing (40 characters).
    /// </summary>
    public const string T42_BLANK_LINE = "\x1b[38;5;231m\x1b[48;5;16m                                        \x1b[0m";
    /// <summary>
    /// Display width for teletext content (40 characters per line).
    /// </summary>
    public const int T42_DISPLAY_WIDTH = 40;
    #endregion

    #region TS Constants
    /// <summary>
    /// Size of a standard MPEG-TS packet in bytes (188-byte variant).
    /// </summary>
    public const int TS_PACKET_SIZE = 188;
    /// <summary>
    /// MPEG-TS packet synchronization byte value (0x47).
    /// </summary>
    public const byte TS_SYNC_BYTE = 0x47;
    /// <summary>
    /// Bit mask for extracting the 13-bit PID (Packet Identifier) from TS header.
    /// </summary>
    public const int TS_PID_MASK = 0x1FFF;
    /// <summary>
    /// PID for Program Association Table (PAT), always 0x0000.
    /// </summary>
    public const int TS_PAT_PID = 0x0000;
    /// <summary>
    /// Minimum size of a TS packet header in bytes.
    /// </summary>
    public const int TS_HEADER_SIZE = 4;
    /// <summary>
    /// Adaptation field control flag indicating adaptation field is present.
    /// </summary>
    public const byte TS_ADAPTATION_FIELD_FLAG = 0x20;
    /// <summary>
    /// Transport scrambling control flag indicating payload is present.
    /// </summary>
    public const byte TS_PAYLOAD_FLAG = 0x10;
    /// <summary>
    /// Payload unit start indicator flag in TS header.
    /// </summary>
    public const byte TS_PAYLOAD_START_INDICATOR = 0x40;
    /// <summary>
    /// Stream type identifier for DVB teletext in PMT (Program Map Table).
    /// </summary>
    public const byte TS_STREAM_TYPE_TELETEXT = 0x06;
    /// <summary>
    /// Stream type identifier for MPEG-1 Video in PMT.
    /// </summary>
    public const byte TS_STREAM_TYPE_MPEG1_VIDEO = 0x01;
    /// <summary>
    /// Stream type identifier for MPEG-2 Video in PMT.
    /// </summary>
    public const byte TS_STREAM_TYPE_MPEG2_VIDEO = 0x02;
    /// <summary>
    /// Stream type identifier for H.264/AVC Video in PMT.
    /// </summary>
    public const byte TS_STREAM_TYPE_H264_VIDEO = 0x1B;
    /// <summary>
    /// Stream type identifier for H.265/HEVC Video in PMT.
    /// </summary>
    public const byte TS_STREAM_TYPE_H265_VIDEO = 0x24;
    /// <summary>
    /// Descriptor tag for DVB teletext descriptor in PMT.
    /// </summary>
    public const byte TS_DESCRIPTOR_TAG_TELETEXT = 0x56;
    /// <summary>
    /// PES (Packetized Elementary Stream) packet start code prefix.
    /// </summary>
    public static readonly byte[] TS_PES_START_CODE = [0x00, 0x00, 0x01];
    /// <summary>
    /// Data identifier for EBU teletext in PES data field.
    /// </summary>
    public const byte TS_DATA_IDENTIFIER_EBU_TELETEXT = 0x10;
    /// <summary>
    /// Size of a single teletext data unit within PES payload (44 bytes: 2-byte header + 42-byte T42 data).
    /// </summary>
    public const int TS_TELETEXT_DATA_UNIT_SIZE = 44;
    /// <summary>
    /// Data unit ID for EBU teletext non-subtitle data (0x02 or 0x03).
    /// </summary>
    public const byte TS_DATA_UNIT_ID_TELETEXT = 0x02;
    /// <summary>
    /// Data unit ID for EBU teletext subtitle data.
    /// </summary>
    public const byte TS_DATA_UNIT_ID_TELETEXT_SUBTITLE = 0x03;

    /// <summary>
    /// Size of MPEG-TS packet with 4-byte timecode suffix (192-byte variant).
    /// </summary>
    public const int TS_PACKET_SIZE_WITH_TIMECODE = 192;
    /// <summary>
    /// Size of MPEG-TS packet with 16-byte Reed-Solomon FEC (204-byte variant).
    /// </summary>
    public const int TS_PACKET_SIZE_WITH_FEC = 204;
    /// <summary>
    /// Size of the 4-byte timecode suffix in 192-byte TS packets.
    /// </summary>
    public const int TS_TIMECODE_SUFFIX_SIZE = 4;

    /// <summary>
    /// PTS/DTS flags present indicator bit in PES header flags byte (bit 7-6 = '10' for PTS only, '11' for both).
    /// </summary>
    public const byte TS_PES_PTS_DTS_FLAGS = 0xC0;
    /// <summary>
    /// PTS only present flag value (0x80 = bits 10xxxxxx).
    /// </summary>
    public const byte TS_PES_PTS_FLAG = 0x80;
    /// <summary>
    /// PTS and DTS both present flag value (0xC0 = bits 11xxxxxx).
    /// </summary>
    public const byte TS_PES_PTS_AND_DTS_FLAG = 0xC0;
    /// <summary>
    /// Marker bit value used in PTS/DTS timestamps (0x01).
    /// </summary>
    public const byte TS_PTS_MARKER_BIT = 0x01;
    /// <summary>
    /// PTS/DTS clock frequency in Hz (90 kHz).
    /// </summary>
    public const int TS_PTS_CLOCK_FREQUENCY = 90000;
    /// <summary>
    /// Maximum value for 33-bit PTS/DTS counter before wraparound (2^33).
    /// </summary>
    public const long TS_PTS_MAX_VALUE = 0x200000000; // 8589934592
    /// <summary>
    /// Offset to PES header flags byte (byte 7 of PES packet).
    /// </summary>
    public const int TS_PES_FLAGS_OFFSET = 7;
    /// <summary>
    /// Offset to PES header data length byte (byte 8 of PES packet).
    /// </summary>
    public const int TS_PES_HEADER_DATA_LENGTH_OFFSET = 8;
    /// <summary>
    /// Starting offset of PTS value in PES header (byte 9).
    /// </summary>
    public const int TS_PES_PTS_OFFSET = 9;
    /// <summary>
    /// Size of PTS/DTS timestamp in bytes (5 bytes each).
    /// </summary>
    public const int TS_PTS_SIZE = 5;
    /// <summary>
    /// Starting offset of DTS value in PES header (byte 14, after PTS).
    /// </summary>
    public const int TS_PES_DTS_OFFSET = 14;

    // Placeholder for DVB subtitle support
    // public const byte TS_STREAM_TYPE_DVB_SUBTITLE = 0x05;
    // public const byte TS_DESCRIPTOR_TAG_SUBTITLE = 0x59;
    #endregion

    #region RCWT Constants
    /// <summary>
    /// RCWT packet type byte (unknown purpose).
    /// </summary>
    public const byte RCWT_PACKET_TYPE_UNKNOWN = 0x03;
    /// <summary>
    /// RCWT framing code byte for packet synchronization.
    /// </summary>
    public const byte RCWT_FRAMING_CODE = 0x27;
    /// <summary>
    /// RCWT field 0 marker byte.
    /// </summary>
    public const byte RCWT_FIELD_0_MARKER = 0xAF;
    /// <summary>
    /// RCWT field 1 marker byte.
    /// </summary>
    public const byte RCWT_FIELD_1_MARKER = 0xAB;
    /// <summary>
    /// Size of the FTS (Frame Time Stamp) field in bytes.
    /// </summary>
    public const int RCWT_FTS_BYTE_SIZE = 8;
    /// <summary>
    /// RCWT file header bytes written at the beginning of output.
    /// </summary>
    public static readonly byte[] RCWT_HEADER = [204, 204, 237, 204, 0, 80, 0, 2, 0, 0, 0];
    #endregion

    #region STL Constants
    /// <summary>
    /// Size of the GSI (General Subtitle Information) block in bytes.
    /// </summary>
    public const int STL_GSI_BLOCK_SIZE = 1024;
    /// <summary>
    /// Size of a TTI (Text and Timing Information) block in bytes.
    /// </summary>
    public const int STL_TTI_BLOCK_SIZE = 128;
    /// <summary>
    /// Maximum text field size in a TTI block (112 bytes).
    /// </summary>
    public const int STL_TEXT_FIELD_SIZE = 112;
    /// <summary>
    /// STL start box control code.
    /// </summary>
    public const byte STL_START_BOX = 0x0B;
    /// <summary>
    /// STL end box control code.
    /// </summary>
    public const byte STL_END_BOX = 0x0A;
    /// <summary>
    /// STL alpha black color code.
    /// </summary>
    public const byte STL_ALPHA_BLACK = 0x00;
    /// <summary>
    /// STL alpha red color code.
    /// </summary>
    public const byte STL_ALPHA_RED = 0x01;
    /// <summary>
    /// STL alpha green color code.
    /// </summary>
    public const byte STL_ALPHA_GREEN = 0x02;
    /// <summary>
    /// STL alpha yellow color code.
    /// </summary>
    public const byte STL_ALPHA_YELLOW = 0x03;
    /// <summary>
    /// STL alpha blue color code.
    /// </summary>
    public const byte STL_ALPHA_BLUE = 0x04;
    /// <summary>
    /// STL alpha magenta color code.
    /// </summary>
    public const byte STL_ALPHA_MAGENTA = 0x05;
    /// <summary>
    /// STL alpha cyan color code.
    /// </summary>
    public const byte STL_ALPHA_CYAN = 0x06;
    /// <summary>
    /// STL alpha white color code.
    /// </summary>
    public const byte STL_ALPHA_WHITE = 0x07;
    /// <summary>
    /// STL subtitle group number (default 0x00 for teletext).
    /// </summary>
    public const byte STL_SUBTITLE_GROUP = 0x00;
    /// <summary>
    /// STL cumulative status (0x00 = not part of cumulative set).
    /// </summary>
    public const byte STL_CUMULATIVE_STATUS = 0x00;
    #endregion

    #region Default Values
    /// <summary>
    /// Default magazine number used across the library when no specific magazine is specified (for backward compatibility).
    /// </summary>
    public const int DEFAULT_MAGAZINE = 8;
    /// <summary>
    /// Array containing all valid teletext magazine numbers (1-8) for processing.
    /// </summary>
    public static readonly int[] DEFAULT_MAGAZINES = [1, 2, 3, 4, 5, 6, 7, 8];
    /// <summary>
    /// Array containing all valid teletext row numbers (0-31) for processing, including extended range per teletext specification.
    /// </summary>
    public static readonly int[] DEFAULT_ROWS = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31];
    /// <summary>
    /// Array containing caption row numbers (1-24) used for subtitle and caption processing, excluding header row 0.
    /// </summary>
    public static readonly int[] CAPTION_ROWS = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24];
    #endregion
}
