using System;

namespace nathanbutlerDEV.libopx;

public class Constants
{
    #region MXF Constants
    public const int KLV_KEY_SIZE = 16;
    public const int SYSTEM_METADATA_PACK_GC = 41;
    public const int SYSTEM_METADATA_SET_GC_OFFSET = 12;
    public const int SMPTE_TIMECODE_SIZE = 4;
    #endregion

    #region Packet Constants
    public const int PACKET_HEADER_SIZE = 2; // Size of the packet header in bytes
    #endregion
    #region Line Constants
    public const int LINE_HEADER_SIZE = 14; // Size of the line header in bytes
    #endregion
    #region VBI Constants
    public const int VBI_LINE_SIZE = 720; // Size of a VBI line
    public const int VBI_DOUBLE_LINE_SIZE = 1440; // Size of a double VBI line
    public const float VBI_DEFAULT_THRESHOLD = 0.40f; // Default threshold
    public const int VBI_PARITY_FLIP_MASK = 0x80; // Parity flip mask
    public const int VBI_MAX_OFFSET_RANGE = 100; // Maximum offset range
    public const int VBI_MAX_OFFSET_SEARCH = 100; // Maximum offset search
    public const int VBI_CLOCK_OFFSET_1 = 8; // Clock offset 1
    public const int VBI_FRAMING_OFFSET_1 = 39; // Framing offset 1
    public const int VBI_FRAMING_OFFSET_2 = 40; // Framing offset 2
    public const byte VBI_LOW_VALUE = 0x10; // Low value for VBI padding
    public const byte VBI_HIGH_VALUE = 0xEB; // High value for VBI padding
    public const int VBI_PADDING_SIZE = 16; // Padding size for VBI
    public const int VBI_BITS_SIZE = 360; // Size of VBI bits
    public const int VBI_PAD_START = 6; // Start of padding in VBI
    public const int VBI_RESIZED_SIZE = 701; // Size of resized VBI data
    #endregion

    #region T42 Constants
    public const int T42_LINE_SIZE = 42; // Size of a T42 line
    public const byte T42_CLOCK_BYTE = 0x55; // Clock byte
    public const byte T42_FRAMING_CODE = 0x27; // Framing code
    public const int T42_PLUS_FRAMING = 43; // T42 line size plus framing code
    public const int T42_PLUS_CRIFC = 45; // T42 line size plus CRIFC
    public const int T42_BITS_PER_BYTE = 28; // Number of bits per byte in T42
    public const int T42_BYTE_STEP_NORMAL = 31; // Normal byte step for T42
    public const int T42_BYTE_STEP_EXTENDED = 32; // Extended byte step for T42
    public const int T42_PARITY_FLIP_MASK = 0x80; // Parity flip mask for T42
    public const int T42_DEFAULT_MAGAZINE = 8; // Default magazine number when magazine field is 0
    public const int T42_MAGAZINE_MASK = 0x7; // Mask for extracting magazine bits
    public const int T42_ROW_SHIFT = 3; // Bit shift for row extraction from Hamming 16-bit decode
    public const int T42_HIGH_NIBBLE_SHIFT = 4; // High nibble shift for Hamming 16-bit decode
    public const int T42_MIN_BYTES_FOR_ROW = 2; // Minimum bytes required for row extraction
    public const byte T42_BLOCK_START_BYTE = 0x0B; // Block start byte
    public const byte T42_BACKGROUND_CONTROL = 29; // Background control character
    public const byte T42_NORMAL_HEIGHT = 10; // Normal height control character
    public const string T42_ANSI_RESET = "\x1b[0m"; // ANSI reset sequence
    public const string T42_DEFAULT_COLORS = "\x1b[37m\x1b[40m"; // White foreground, black background
    public const string T42_BLANK_LINE = "\x1b[37m\x1b[40m                                        \x1b[0m";
    #endregion

    #region Default Values
    public const int DEFAULT_MAGAZINE = 8; // Default magazine number
    public static readonly int[] DEFAULT_ROWS = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24];
    public static readonly int[] CAPTION_ROWS = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24];
    #endregion
}
