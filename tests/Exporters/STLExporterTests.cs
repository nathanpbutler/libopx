using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Exporters;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Exporters;

/// <summary>
/// Unit tests for the STLExporter class.
/// Tests intelligent subtitle merging, text growth detection, and content tracking.
/// </summary>
public class STLExporterTests : IDisposable
{
    private readonly STLExporter _exporter;

    public STLExporterTests()
    {
        _exporter = new STLExporter
        {
            Magazine = 8,
            Rows = Constants.CAPTION_ROWS,
            ClearDelayFrames = 0  // Immediate emission for testing
        };
    }

    public void Dispose()
    {
        _exporter.Dispose();
        GC.SuppressFinalize(this);
    }

    #region NormalizeText Tests

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("Hello", "Hello")]
    [InlineData("  Hello  ", "Hello")]
    [InlineData("Hello  World", "Hello World")]
    [InlineData("\x1b[37mHello\x1b[0m", "Hello")]
    [InlineData("\x1b[37m\x1b[40m  Hello World  \x1b[0m", "Hello World")]
    public void NormalizeText_ReturnsExpected(string? input, string expected)
    {
        // Act
        var result = STLExporter.NormalizeText(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeText_StripsAnsiCodes()
    {
        // Arrange
        var textWithAnsi = "\x1b[37m\x1b[40mHello World\x1b[0m";

        // Act
        var result = STLExporter.NormalizeText(textWithAnsi);

        // Assert
        Assert.Equal("Hello World", result);
    }

    #endregion

    #region IsTextGrowing Tests

    [Theory]
    [InlineData("thought", "thought we", true)]
    [InlineData("thought we", "thought we would", true)]
    [InlineData("Hello", "Hello world", true)]
    [InlineData("Hello world", "Hello world!", true)]
    [InlineData("", "Hello", true)]
    public void IsTextGrowing_WhenGrowing_ReturnsTrue(string previous, string current, bool expected)
    {
        // Act
        var result = STLExporter.IsTextGrowing(previous, current);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Hello", "Goodbye", false)]
    [InlineData("Hello world", "Hello", false)]
    [InlineData("thought we would", "thought we", false)]
    [InlineData("Hello", "Hello", false)]  // Same text is not "growing"
    [InlineData("ABC", "DEF", false)]
    public void IsTextGrowing_WhenNotGrowing_ReturnsFalse(string previous, string current, bool expected)
    {
        // Act
        var result = STLExporter.IsTextGrowing(previous, current);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsTextGrowing_WordByWordGrowth_ReturnsTrue()
    {
        // Arrange - simulate caption building word by word
        var sequence = new[]
        {
            ("having", "having a"),
            ("having a", "having a whale"),
            ("having a whale", "having a whale of"),
            ("having a whale of", "having a whale of a"),
            ("having a whale of a", "having a whale of a time")
        };

        // Act & Assert
        foreach (var (prev, curr) in sequence)
        {
            Assert.True(STLExporter.IsTextGrowing(prev, curr),
                $"Expected '{prev}' -> '{curr}' to be recognized as growing");
        }
    }

    #endregion

    #region Export Tests

    [Fact]
    public void Export_EmptyPackets_YieldsNoBlocks()
    {
        // Arrange
        var packets = new List<Packet>();

        // Act
        var blocks = _exporter.Export(packets).ToList();

        // Assert
        Assert.Empty(blocks);
    }

    [Fact]
    public void Export_SingleLineAppearsThenClears_YieldsOneBlock()
    {
        // Arrange
        var line1 = CreateTestLine(row: 18, text: "Hello World", timecode: new Timecode(0));
        var packet1 = CreatePacket([line1], new Timecode(0));

        var packet2 = CreatePacket([], new Timecode(0, 0, 0, 1));  // Empty packet (content cleared)

        // Act
        var blocks = _exporter.Export([packet1, packet2]).ToList();

        // Assert
        Assert.Single(blocks);
        Assert.Equal(128, blocks[0].Length);  // TTI block size
    }

    [Fact]
    public void Export_TextGrowsWordByWord_YieldsOneBlockAtEnd()
    {
        // Arrange - simulate word-by-word buildup
        var line1 = CreateTestLine(row: 18, text: "thought", timecode: new Timecode(0));
        var line2 = CreateTestLine(row: 18, text: "thought we", timecode: new Timecode(0, 0, 0, 1));
        var line3 = CreateTestLine(row: 18, text: "thought we would", timecode: new Timecode(0, 0, 0, 2));

        var packet1 = CreatePacket([line1], new Timecode(0));
        var packet2 = CreatePacket([line2], new Timecode(0, 0, 0, 1));
        var packet3 = CreatePacket([line3], new Timecode(0, 0, 0, 2));
        var packet4 = CreatePacket([], new Timecode(0, 0, 0, 3));  // Content cleared

        // Act
        var blocks = _exporter.Export([packet1, packet2, packet3, packet4]).ToList();

        // Assert - should produce only ONE subtitle for the full text
        Assert.Single(blocks);
    }

    [Fact]
    public void Export_ContentShiftsRow_YieldsOneBlock()
    {
        // Arrange - content appears on row 18, then shifts to row 16
        var line1 = CreateTestLine(row: 18, text: "having a whale of a time", timecode: new Timecode(0));
        var line2 = CreateTestLine(row: 16, text: "having a whale of a time", timecode: new Timecode(0, 0, 0, 1));

        var packet1 = CreatePacket([line1], new Timecode(0));
        var packet2 = CreatePacket([line2], new Timecode(0, 0, 0, 1));
        var packet3 = CreatePacket([], new Timecode(0, 0, 0, 2));  // Content cleared

        // Act
        var blocks = _exporter.Export([packet1, packet2, packet3]).ToList();

        // Assert - same content shifting rows should be ONE subtitle
        Assert.Single(blocks);
    }

    [Fact]
    public void Export_MultipleConcurrentLines_YieldsMultipleBlocks()
    {
        // Arrange - two different lines on different rows
        var line1 = CreateTestLine(row: 16, text: "First line", timecode: new Timecode(0));
        var line2 = CreateTestLine(row: 18, text: "Second line", timecode: new Timecode(0));

        var packet1 = CreatePacket([line1, line2], new Timecode(0));
        var packet2 = CreatePacket([], new Timecode(0, 0, 0, 1));  // Both cleared

        // Act
        var blocks = _exporter.Export([packet1, packet2]).ToList();

        // Assert - two different content items should produce two subtitles
        Assert.Equal(2, blocks.Count);
    }

    [Fact]
    public void Export_Row0Headers_AreIgnored()
    {
        // Arrange - row 0 is header, should be ignored
        var headerLine = CreateTestLine(row: 0, text: "Page 888", timecode: new Timecode(0));
        var captionLine = CreateTestLine(row: 18, text: "Caption text", timecode: new Timecode(0));

        var packet1 = CreatePacket([headerLine, captionLine], new Timecode(0));
        var packet2 = CreatePacket([headerLine], new Timecode(0, 0, 0, 1));  // Caption cleared, header remains

        // Act
        var blocks = _exporter.Export([packet1, packet2]).ToList();

        // Assert - only the caption should produce a subtitle
        Assert.Single(blocks);
    }

    [Fact]
    public void Export_TextGrowsAcrossFrameGap_YieldsOneBlock()
    {
        // Arrange - use a separate exporter with delayed clear enabled
        using var delayedExporter = new STLExporter
        {
            Magazine = 8,
            Rows = Constants.CAPTION_ROWS,
            ClearDelayFrames = 30  // Enable delayed clear
        };

        // Frame 1: "Alright," appears
        var line1 = CreateTestLine(row: 16, text: "Alright,", timecode: new Timecode(0, 0, 0, 0));
        var packet1 = CreatePacket([line1], new Timecode(0, 0, 0, 0));

        // Frame 2: Empty (content clears, goes to pending)
        var packet2 = CreatePacket([], new Timecode(0, 0, 0, 1));

        // Frame 3-14: Still empty (building up delay)
        var emptyPackets = Enumerable.Range(2, 13)
            .Select(f => CreatePacket([], new Timecode(0, 0, 0, f)))
            .ToArray();

        // Frame 15: "Alright, the" appears (should grow from pending)
        var line2 = CreateTestLine(row: 16, text: "Alright, the", timecode: new Timecode(0, 0, 0, 15));
        var packet3 = CreatePacket([line2], new Timecode(0, 0, 0, 15));

        // Frame 16: Empty again (final clear)
        var packet4 = CreatePacket([], new Timecode(0, 0, 0, 16));

        // Act - process all packets
        var allPackets = new[] { packet1, packet2 }
            .Concat(emptyPackets)
            .Concat(new[] { packet3, packet4 })
            .ToArray();

        var exportBlocks = delayedExporter.Export(allPackets).ToList();
        var flushBlocks = delayedExporter.Flush().ToList();
        var totalBlocks = exportBlocks.Concat(flushBlocks).ToList();

        // Assert - should produce only ONE subtitle with the final text
        // The start time should be from frame 0, end time from frame 16
        Assert.Single(totalBlocks);
    }

    #endregion

    #region Flush Tests

    [Fact]
    public void Flush_WithPendingContent_EmitsRemainingSubtitles()
    {
        // Arrange - content appears but never clears
        var line = CreateTestLine(row: 18, text: "Pending content", timecode: new Timecode(0));
        var packet = CreatePacket([line], new Timecode(0));

        // Act
        var exportBlocks = _exporter.Export([packet]).ToList();
        var flushBlocks = _exporter.Flush().ToList();

        // Assert
        Assert.Empty(exportBlocks);  // Content still present, not emitted during export
        Assert.Single(flushBlocks);  // Flush should emit the pending content
    }

    [Fact]
    public void Flush_NoPendingContent_EmitsNothing()
    {
        // Arrange - no content processed

        // Act
        var blocks = _exporter.Flush().ToList();

        // Assert
        Assert.Empty(blocks);
    }

    #endregion

    #region GSI Header Tests

    [Fact]
    public void CreateGSIHeader_ReturnsCorrectSize()
    {
        // Act
        var gsi = STLExporter.CreateGSIHeader();

        // Assert
        Assert.Equal(Constants.STL_GSI_BLOCK_SIZE, gsi.Length);
    }

    [Fact]
    public void CreateGSIHeader_ContainsCorrectDiskFormat()
    {
        // Act
        var gsi = STLExporter.CreateGSIHeader();

        // Assert - bytes 3-10 should be "STL25.01"
        var dfc = System.Text.Encoding.ASCII.GetString(gsi, 3, 8);
        Assert.Equal("STL25.01", dfc);
    }

    #endregion

    #region Helper Methods

    private static Line CreateTestLine(int row, string text, Timecode timecode)
    {
        // Create a minimal T42-like data structure
        var data = new byte[Constants.T42_LINE_SIZE];

        // Set magazine and row in first 2 bytes (simplified)
        data[0] = 0x00;
        data[1] = (byte)row;

        // Fill text starting at byte 2
        var textBytes = System.Text.Encoding.ASCII.GetBytes(text);
        Array.Copy(textBytes, 0, data, 2, Math.Min(textBytes.Length, 40));

        // Create a valid 14-byte Line header
        // Format: [Number(2)] [Wrapping(1)] [SampleCoding(1)] [SampleCount(2)] [unused(2)] [Length(2)] [unused(3)] [Type(1)=0x01]
        var header = new byte[14];
        header[0] = 0x00;  // Number high byte
        header[1] = 0x00;  // Number low byte
        header[2] = 0x01;  // Wrapping
        header[3] = 0x01;  // Sample coding
        header[4] = 0x00;  // Sample count high byte
        header[5] = Constants.T42_LINE_SIZE;  // Sample count low byte
        header[6] = 0x00;  // unused
        header[7] = 0x00;  // unused
        header[8] = 0x00;  // Length high byte
        header[9] = Constants.T42_LINE_SIZE;  // Length low byte
        header[10] = 0x00; // unused
        header[11] = 0x00; // unused
        header[12] = 0x00; // unused
        header[13] = 0x01; // Type must be 0x01

        var line = new Line(header)
        {
            Data = data,
            LineTimecode = timecode,
            Magazine = 8,
            Row = row,
            Text = $"\x1b[37m\x1b[40m{text}\x1b[0m"  // Simulate ANSI-formatted text
        };

        return line;
    }

    private static Packet CreatePacket(Line[] lines, Timecode timecode)
    {
        var header = new byte[] { 0x00, (byte)lines.Length };
        var packet = new Packet(header)
        {
            Timecode = timecode
        };

        foreach (var line in lines)
        {
            packet.Lines.Add(line);
        }

        return packet;
    }

    #endregion
}
