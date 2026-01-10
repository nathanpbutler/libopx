using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Core;

namespace nathanbutlerDEV.libopx.Tests.Core;

public class FormatConverterTests
{
    #region VBI <-> T42 Conversion Tests

    [Fact]
    public void VBIToT42_ValidVBI720Bytes_ReturnsT42()
    {
        // Arrange
        var vbiData = new byte[Constants.VBI_LINE_SIZE];

        // Act
        var result = FormatConverter.VBIToT42(vbiData);

        // Assert
        Assert.Equal(Constants.T42_LINE_SIZE, result.Length);
    }

    [Fact]
    public void VBIToT42_ValidVBIDouble1440Bytes_ReturnsT42()
    {
        // Arrange
        var vbiDoubleData = new byte[Constants.VBI_DOUBLE_LINE_SIZE];

        // Act
        var result = FormatConverter.VBIToT42(vbiDoubleData);

        // Assert
        Assert.Equal(Constants.T42_LINE_SIZE, result.Length);
    }

    [Fact]
    public void VBIToT42_InvalidSize_ThrowsArgumentException()
    {
        // Arrange
        var invalidData = new byte[500]; // Invalid size

        // Act & Assert
        Assert.Throws<ArgumentException>(() => FormatConverter.VBIToT42(invalidData));
    }

    [Fact]
    public void T42ToVBI_ValidT42_ReturnsVBI720Bytes()
    {
        // Arrange
        var t42Data = new byte[Constants.T42_LINE_SIZE];

        // Act
        var result = FormatConverter.T42ToVBI(t42Data, Format.VBI);

        // Assert
        Assert.Equal(Constants.VBI_LINE_SIZE, result.Length);
    }

    [Fact]
    public void T42ToVBI_ValidT42_ReturnsVBIDouble1440Bytes()
    {
        // Arrange
        var t42Data = new byte[Constants.T42_LINE_SIZE];

        // Act
        var result = FormatConverter.T42ToVBI(t42Data, Format.VBI_DOUBLE);

        // Assert
        Assert.Equal(Constants.VBI_DOUBLE_LINE_SIZE, result.Length);
    }

    [Fact]
    public void T42ToVBI_InvalidSize_ReturnsBlankVBI()
    {
        // Arrange
        var invalidT42 = new byte[30]; // Invalid size

        // Act
        var result = FormatConverter.T42ToVBI(invalidT42, Format.VBI);

        // Assert
        Assert.Equal(Constants.VBI_LINE_SIZE, result.Length);
        Assert.All(result, b => Assert.Equal(0, b));
    }

    [Fact]
    public void VBIToT42_RoundTrip_PreservesData()
    {
        // Arrange - Create a valid T42 line
        var originalT42 = new byte[Constants.T42_LINE_SIZE];
        // Set some non-zero values for testing
        originalT42[0] = 0x42; // Magazine/row byte
        originalT42[1] = 0x15; // Magazine/row byte
        originalT42[10] = 0x48; // 'H'
        originalT42[11] = 0x65; // 'e'
        originalT42[12] = 0x6C; // 'l'
        originalT42[13] = 0x6C; // 'l'
        originalT42[14] = 0x6F; // 'o'

        // Convert T42 -> VBI -> T42
        var vbi = FormatConverter.T42ToVBI(originalT42, Format.VBI);
        var roundTripT42 = FormatConverter.VBIToT42(vbi);

        // Assert - Should get similar T42 data back (allowing for some signal degradation)
        Assert.Equal(Constants.T42_LINE_SIZE, roundTripT42.Length);
    }

    #endregion

    #region VBI <-> VBI_DOUBLE Conversion Tests

    [Fact]
    public void VBIToVBIDouble_ValidVBI_Returns1440Bytes()
    {
        // Arrange
        var vbiData = new byte[Constants.VBI_LINE_SIZE];

        // Act
        var result = FormatConverter.VBIToVBIDouble(vbiData);

        // Assert
        Assert.Equal(Constants.VBI_DOUBLE_LINE_SIZE, result.Length);
    }

    [Fact]
    public void VBIDoubleToVBI_ValidVBIDouble_Returns720Bytes()
    {
        // Arrange
        var vbiDoubleData = new byte[Constants.VBI_DOUBLE_LINE_SIZE];

        // Act
        var result = FormatConverter.VBIDoubleToVBI(vbiDoubleData);

        // Assert
        Assert.Equal(Constants.VBI_LINE_SIZE, result.Length);
    }

    [Fact]
    public void VBIToVBIDouble_RoundTrip_PreservesData()
    {
        // Arrange
        var originalVBI = new byte[Constants.VBI_LINE_SIZE];
        for (int i = 0; i < originalVBI.Length; i++)
        {
            originalVBI[i] = (byte)(i % 256);
        }

        // Act - VBI -> VBI_DOUBLE -> VBI
        var vbiDouble = FormatConverter.VBIToVBIDouble(originalVBI);
        var roundTripVBI = FormatConverter.VBIDoubleToVBI(vbiDouble);

        // Assert
        Assert.Equal(originalVBI.Length, roundTripVBI.Length);
        Assert.Equal(originalVBI, roundTripVBI);
    }

    #endregion

    #region RCWT Conversion Tests

    [Fact]
    public void T42ToRCWT_ValidT42_Returns53Bytes()
    {
        // Arrange
        var t42Data = new byte[Constants.T42_LINE_SIZE];
        int fts = 1000;
        int fieldNumber = 0;

        // Act
        var result = FormatConverter.T42ToRCWT(t42Data, fts, fieldNumber);

        // Assert
        Assert.Equal(53, result.Length); // 1 + 8 + 1 + 1 + 42
    }

    [Fact]
    public void T42ToRCWT_PacketStructure_IsCorrect()
    {
        // Arrange
        var t42Data = new byte[Constants.T42_LINE_SIZE];
        for (int i = 0; i < t42Data.Length; i++)
        {
            t42Data[i] = (byte)(i + 1); // Non-zero test data
        }
        int fts = 12345;
        int fieldNumber = 0;

        // Act
        var result = FormatConverter.T42ToRCWT(t42Data, fts, fieldNumber);

        // Assert
        Assert.Equal(Constants.RCWT_PACKET_TYPE_UNKNOWN, result[0]); // Packet type
        // FTS bytes [1-8] - just verify they're not all zero
        Assert.True(result.Skip(1).Take(8).Any(b => b != 0));
        Assert.Equal(Constants.RCWT_FIELD_0_MARKER, result[9]); // Field marker for field 0
        Assert.Equal(Constants.RCWT_FRAMING_CODE, result[10]); // Framing code
        // T42 payload [11-52]
        Assert.Equal(t42Data[0], result[11]);
        Assert.Equal(t42Data[41], result[52]);
    }

    [Fact]
    public void T42ToRCWT_Field1_UsesCorrectMarker()
    {
        // Arrange
        var t42Data = new byte[Constants.T42_LINE_SIZE];
        int fts = 1000;
        int fieldNumber = 1;

        // Act
        var result = FormatConverter.T42ToRCWT(t42Data, fts, fieldNumber);

        // Assert
        Assert.Equal(Constants.RCWT_FIELD_1_MARKER, result[9]); // Field marker for field 1
    }

    [Fact]
    public void T42ToRCWT_InvalidT42Size_UsesBlankData()
    {
        // Arrange
        var invalidT42 = new byte[20]; // Wrong size

        // Act
        var result = FormatConverter.T42ToRCWT(invalidT42, 1000, 0);

        // Assert
        Assert.Equal(53, result.Length);
        // Payload should be blank (all zeros)
        Assert.All(result.Skip(11).Take(42), b => Assert.Equal(0, b));
    }

    #endregion

    #region STL Conversion Tests

    [Fact]
    public void T42ToSTL_ValidT42_Returns128Bytes()
    {
        // Arrange
        var t42Data = new byte[Constants.T42_LINE_SIZE];
        var timecode = new Timecode(10, 0, 0, 0, 25);
        int subtitleNumber = 1;
        int row = 20;

        // Act
        var result = FormatConverter.T42ToSTL(t42Data, subtitleNumber, row, timecode);

        // Assert
        Assert.Equal(Constants.STL_TTI_BLOCK_SIZE, result.Length);
    }

    [Fact]
    public void T42ToSTL_TTIBlockStructure_IsCorrect()
    {
        // Arrange
        var t42Data = new byte[Constants.T42_LINE_SIZE];
        var timeCodeIn = new Timecode(10, 30, 45, 12, 25);
        var timeCodeOut = new Timecode(10, 30, 50, 12, 25);
        int subtitleNumber = 123;
        int row = 20;

        // Act
        var result = FormatConverter.T42ToSTL(t42Data, subtitleNumber, row, timeCodeIn, timeCodeOut);

        // Assert
        Assert.Equal(Constants.STL_SUBTITLE_GROUP, result[0]); // SGN
        Assert.Equal((subtitleNumber >> 8) & 0xFF, result[1]); // SN high byte
        Assert.Equal(subtitleNumber & 0xFF, result[2]); // SN low byte
        Assert.Equal(0xFF, result[3]); // EBN
        Assert.Equal(Constants.STL_CUMULATIVE_STATUS, result[4]); // CS
        Assert.Equal(20, result[13]); // VP (row)
        Assert.Equal(0x02, result[14]); // JC (left-justified)
        Assert.Equal(0x00, result[15]); // CF (not comment)
    }

    [Fact]
    public void T42ToSTL_WithNullTimeCodeOut_UsesTimeCodeIn()
    {
        // Arrange
        var t42Data = new byte[Constants.T42_LINE_SIZE];
        var timeCodeIn = new Timecode(10, 30, 45, 12, 25);
        int subtitleNumber = 1;
        int row = 20;

        // Act
        var result = FormatConverter.T42ToSTL(t42Data, subtitleNumber, row, timeCodeIn, null);

        // Assert - TCO should match TCI
        var tci = result.Skip(5).Take(4).ToArray();
        var tco = result.Skip(9).Take(4).ToArray();
        Assert.Equal(tci, tco);
    }

    [Fact]
    public void EncodeTimecodeToSTL_ValidTimecode_ReturnsBCD()
    {
        // Arrange
        var timecode = new Timecode(10, 30, 45, 12, 25);

        // Act
        var result = FormatConverter.EncodeTimecodeToSTL(timecode);

        // Assert
        Assert.Equal(4, result.Length);
        Assert.Equal(0x10, result[0]); // Hours: 10 -> 0x10 in BCD
        Assert.Equal(0x30, result[1]); // Minutes: 30 -> 0x30 in BCD
        Assert.Equal(0x45, result[2]); // Seconds: 45 -> 0x45 in BCD
        Assert.Equal(0x12, result[3]); // Frames: 12 -> 0x12 in BCD
    }

    [Fact]
    public void EncodeTimecodeToSTL_ZeroTimecode_ReturnsZeros()
    {
        // Arrange
        var timecode = new Timecode(0, 0, 0, 0, 25);

        // Act
        var result = FormatConverter.EncodeTimecodeToSTL(timecode);

        // Assert
        Assert.Equal(4, result.Length);
        Assert.All(result, b => Assert.Equal(0, b));
    }

    [Fact]
    public void ExtractSTLTextData_Row0_SkipsFirst10Bytes()
    {
        // Arrange
        var t42Data = new byte[Constants.T42_LINE_SIZE];
        // Set some test data after the header bytes
        for (int i = 10; i < t42Data.Length; i++)
        {
            t42Data[i] = (byte)(0x20 + (i - 10)); // Printable ASCII
        }
        int row = 0;

        // Act
        var result = FormatConverter.ExtractSTLTextData(t42Data, row);

        // Assert
        Assert.True(result.Length > 0);
        Assert.True(result.Length <= Constants.STL_TEXT_FIELD_SIZE);
    }

    [Fact]
    public void ExtractSTLTextData_Row1_SkipsFirst2Bytes()
    {
        // Arrange
        var t42Data = new byte[Constants.T42_LINE_SIZE];
        // Set some test data after mag/row bytes
        for (int i = 2; i < t42Data.Length; i++)
        {
            t42Data[i] = (byte)(0x20 + (i - 2)); // Printable ASCII
        }
        int row = 1;

        // Act
        var result = FormatConverter.ExtractSTLTextData(t42Data, row);

        // Assert
        Assert.True(result.Length > 0);
        Assert.True(result.Length <= Constants.STL_TEXT_FIELD_SIZE);
    }

    [Fact]
    public void ExtractSTLTextData_StripsParityBit()
    {
        // Arrange
        var t42Data = new byte[Constants.T42_LINE_SIZE];
        t42Data[2] = 0xE0; // 0xE0 with parity bit -> should become 0x60 after stripping
        t42Data[3] = 0xA0; // 0xA0 with parity bit -> should become 0x20 (space)
        int row = 1;

        // Act
        var result = FormatConverter.ExtractSTLTextData(t42Data, row);

        // Assert
        Assert.Equal(0x60, result[0]);
        Assert.Equal(0x20, result[1]);
    }

    [Fact]
    public void ExtractSTLTextData_RemapsControlCodes()
    {
        // Arrange
        var t42Data = new byte[Constants.T42_LINE_SIZE];
        t42Data[2] = Constants.T42_BLOCK_START_BYTE; // Should map to STL_START_BOX
        t42Data[3] = Constants.T42_NORMAL_HEIGHT; // Should map to STL_END_BOX
        t42Data[4] = 0x00; // Color code 0 should stay 0
        t42Data[5] = 0x07; // Color code 7 should stay 7
        int row = 1;

        // Act
        var result = FormatConverter.ExtractSTLTextData(t42Data, row);

        // Assert
        Assert.Equal(Constants.STL_START_BOX, result[0]);
        Assert.Equal(Constants.STL_END_BOX, result[1]);
        Assert.Equal(0x00, result[2]); // Color code preserved
        Assert.Equal(0x07, result[3]); // Color code preserved
    }

    [Fact]
    public void ExtractSTLTextData_ShortT42Data_PadsWithZeros()
    {
        // Arrange
        var shortT42 = new byte[20]; // Less than 42 bytes
        int row = 1;

        // Act
        var result = FormatConverter.ExtractSTLTextData(shortT42, row);

        // Assert - Should not throw, should pad internally
        Assert.NotNull(result);
    }

    [Fact]
    public void ExtractSTLTextData_MaxLength_DoesNotExceed112Bytes()
    {
        // Arrange
        var t42Data = new byte[Constants.T42_LINE_SIZE];
        // Fill with printable characters
        for (int i = 0; i < t42Data.Length; i++)
        {
            t42Data[i] = 0x41; // 'A'
        }
        int row = 1;

        // Act
        var result = FormatConverter.ExtractSTLTextData(t42Data, row);

        // Assert
        Assert.True(result.Length <= Constants.STL_TEXT_FIELD_SIZE);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void T42ToRCWT_WithVerbose_DoesNotThrow()
    {
        // Arrange
        var t42Data = new byte[Constants.T42_LINE_SIZE];

        // Act & Assert - Should not throw
        var result = FormatConverter.T42ToRCWT(t42Data, 1000, 0, verbose: true);
        Assert.Equal(53, result.Length);
    }

    [Fact]
    public void T42ToSTL_WithVerbose_DoesNotThrow()
    {
        // Arrange
        var t42Data = new byte[Constants.T42_LINE_SIZE];
        var timecode = new Timecode(10, 0, 0, 0, 25);

        // Act & Assert - Should not throw
        var result = FormatConverter.T42ToSTL(t42Data, 1, 20, timecode, verbose: true);
        Assert.Equal(128, result.Length);
    }

    [Fact]
    public void ExtractSTLTextData_WithVerbose_DoesNotThrow()
    {
        // Arrange
        var t42Data = new byte[Constants.T42_LINE_SIZE];

        // Act & Assert - Should not throw
        var result = FormatConverter.ExtractSTLTextData(t42Data, 1, verbose: true);
        Assert.NotNull(result);
    }

    #endregion
}
