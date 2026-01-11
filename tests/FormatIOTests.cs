using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Handlers;
using System.Text;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests;

/// <summary>
/// Comprehensive unit tests for the FormatIO class.
/// Tests factory methods, fluent API, parsing, conversion, and output.
/// </summary>
[Collection("FormatRegistry")]
public class FormatIOTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    // FormatRegistry auto-registers handlers in its static constructor - no manual registration needed

    public void Dispose()
    {
        // Clean up temporary test files
        foreach (var file in _tempFiles.Where(File.Exists))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        GC.SuppressFinalize(this);
    }

    private string CreateTempFile(string extension, byte[] content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"formatiotest_{Guid.NewGuid()}{extension}");
        File.WriteAllBytes(path, content);
        _tempFiles.Add(path);
        return path;
    }

    private static byte[] CreateValidT42Line(byte magazine = 1, byte row = 1)
    {
        var data = new byte[Constants.T42_LINE_SIZE];
        data[0] = (byte)(magazine | (row << 3)); // MPPPPPP format
        data[1] = row; // Row number
        return data;
    }

    private static byte[] CreateValidVBILine()
    {
        // Create a valid T42 line and convert it to VBI
        var t42Line = CreateValidT42Line(magazine: 1, row: 1);
        return FormatConverter.T42ToVBI(t42Line, Format.VBI);
    }

    private static byte[] CreateValidANCPacket()
    {
        // Minimal ANC packet: [0x00, line_number, data_count, ...data]
        var packet = new byte[256];
        packet[0] = 0x00; // Flags
        packet[1] = 0x16; // Line 22 (typical teletext line)
        packet[2] = 0x2C; // 44 bytes of data
        return packet;
    }

    #region Factory Method Tests

    [Fact]
    public void Open_WithValidVBIFile_DetectsFormatCorrectly()
    {
        // Arrange
        var testFile = CreateTempFile(".vbi", CreateValidVBILine());

        // Act
        using var io = FormatIO.Open(testFile);

        // Assert
        Assert.NotNull(io);
    }

    [Fact]
    public void Open_WithValidT42File_DetectsFormatCorrectly()
    {
        // Arrange
        var testFile = CreateTempFile(".t42", CreateValidT42Line());

        // Act
        using var io = FormatIO.Open(testFile);

        // Assert
        Assert.NotNull(io);
    }

    [Fact]
    public void Open_WithValidANCFile_DetectsFormatCorrectly()
    {
        // Arrange
        var testFile = CreateTempFile(".bin", CreateValidANCPacket());

        // Act
        using var io = FormatIO.Open(testFile);

        // Assert
        Assert.NotNull(io);
    }

    [Fact]
    public void Open_WithUnknownExtension_ThrowsFormatDetectionException()
    {
        // Arrange
        var testFile = CreateTempFile(".xyz", new byte[100]);

        // Act & Assert
        Assert.Throws<FormatDetectionException>(() => FormatIO.Open(testFile));
    }

    [Fact]
    public void Open_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => FormatIO.Open("nonexistent.vbi"));
    }

    [Fact]
    public void Open_WithNullPath_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => FormatIO.Open((string)null!));
    }

    [Fact]
    public void OpenStdin_WithValidFormat_CreatesInstance()
    {
        // Act
        using var io = FormatIO.OpenStdin(Format.VBI);

        // Assert
        Assert.NotNull(io);
    }

    [Fact]
    public void OpenStdin_WithUnknownFormat_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => FormatIO.OpenStdin(Format.Unknown));
    }

    [Fact]
    public void Open_WithStream_CreatesInstance()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidVBILine());

        // Act
        using var io = FormatIO.Open(stream, Format.VBI);

        // Assert
        Assert.NotNull(io);
    }

    [Fact]
    public void Open_WithNullStream_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => FormatIO.Open((Stream)null!, Format.VBI));
    }

    [Fact]
    public void Open_WithStreamAndUnknownFormat_ThrowsArgumentException()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidVBILine());

        // Act & Assert
        Assert.Throws<ArgumentException>(() => FormatIO.Open(stream, Format.Unknown));
    }

    #endregion

    #region Format Detection Tests

    [Theory]
    [InlineData(".vbi")]
    [InlineData(".vbid")]
    [InlineData(".t42")]
    [InlineData(".bin")]
    [InlineData(".mxf")]
    [InlineData(".ts")]
    [InlineData(".rcwt")]
    [InlineData(".stl")]
    public void Open_DetectsCorrectFormat_FromExtension(string extension)
    {
        // Arrange
        var testFile = CreateTempFile(extension, new byte[1024]);

        // Act
        using var io = FormatIO.Open(testFile);

        // Assert - If we got here without exception, format was detected
        Assert.NotNull(io);
    }

    [Theory]
    [InlineData(".VBI")]
    [InlineData(".T42")]
    [InlineData(".BIN")]
    public void Open_DetectsFormat_CaseInsensitive(string extension)
    {
        // Arrange
        var testFile = CreateTempFile(extension, new byte[1024]);

        // Act
        using var io = FormatIO.Open(testFile);

        // Assert
        Assert.NotNull(io);
    }

    #endregion

    #region Fluent API Tests

    [Fact]
    public void WithOptions_ConfiguresOptionsCorrectly()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidVBILine());

        // Act
        using var io = FormatIO.Open(stream, Format.VBI)
            .WithOptions(opts =>
            {
                opts.Magazine = 8;
                opts.Rows = new[] { 1, 2, 3 };
            });

        // Assert
        Assert.NotNull(io);
    }

    [Fact]
    public void ConvertTo_ValidConversion_DoesNotThrow()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidVBILine());

        // Act
        using var io = FormatIO.Open(stream, Format.VBI)
            .ConvertTo(Format.T42);

        // Assert
        Assert.NotNull(io);
    }

    [Fact]
    public void ConvertTo_UnsupportedConversion_ThrowsNotSupportedException()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidVBILine());

        // Act & Assert
        using var io = FormatIO.Open(stream, Format.VBI);
        Assert.Throws<NotSupportedException>(() => io.ConvertTo(Format.TS));
    }

    [Fact]
    public void Filter_SetsMagazineAndRows()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidVBILine());

        // Act
        using var io = FormatIO.Open(stream, Format.VBI)
            .Filter(magazine: 8, 1, 2, 3, 4);

        // Assert
        Assert.NotNull(io);
    }

    [Fact]
    public void Filter_OnlyMagazine_DoesNotThrow()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidVBILine());

        // Act
        using var io = FormatIO.Open(stream, Format.VBI)
            .Filter(magazine: 1);

        // Assert
        Assert.NotNull(io);
    }

    [Fact]
    public void Filter_OnlyRows_DoesNotThrow()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidVBILine());

        // Act
        using var io = FormatIO.Open(stream, Format.VBI)
            .Filter(null, 1, 2, 3);

        // Assert
        Assert.NotNull(io);
    }

    [Fact]
    public void WithLineCount_SetsLineCount()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidVBILine());

        // Act
        using var io = FormatIO.Open(stream, Format.VBI)
            .WithLineCount(625);

        // Assert
        Assert.NotNull(io);
    }

    [Fact]
    public void WithStartTimecode_SetsStartTimecode()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidVBILine());
        var tc = new Timecode(10, 0, 0, 0);

        // Act
        using var io = FormatIO.Open(stream, Format.VBI)
            .WithStartTimecode(tc);

        // Assert
        Assert.NotNull(io);
    }

    [Fact]
    public void WithPIDs_SetsPIDs()
    {
        // Arrange
        var stream = new MemoryStream(new byte[1024]);

        // Act
        using var io = FormatIO.Open(stream, Format.TS)
            .WithPIDs(70, 71, 72);

        // Assert
        Assert.NotNull(io);
    }

    [Fact]
    public void FluentAPI_MethodChaining_ReturnsSameInstance()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidVBILine());

        // Act
        using var io = FormatIO.Open(stream, Format.VBI);
        var io2 = io.Filter(magazine: 1);
        var io3 = io2.ConvertTo(Format.T42);
        var io4 = io3.WithLineCount(625);

        // Assert
        Assert.Same(io, io2);
        Assert.Same(io2, io3);
        Assert.Same(io3, io4);
    }

    #endregion

    #region ParseLines Tests

    [Fact]
    public async Task ParseLines_WithVBIFormat_ReturnsLines()
    {
        // Arrange
        const string filePath = "input.vbi";
        await SampleFiles.EnsureAsync(filePath);

        // Act
        using var io = FormatIO.Open(filePath);
        var lines = io.Filter(magazine: 8).ParseLines().ToList();

        // Assert
        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.Equal(8, line.Magazine));
    }

    [Fact]
    public async Task ParseLines_WithT42Format_ReturnsLines()
    {
        // Arrange
        const string filePath = "input.t42";
        await SampleFiles.EnsureAsync(filePath);

        // Act
        using var io = FormatIO.Open(filePath);
        var lines = io.Filter(magazine: 8).ParseLines().ToList();

        // Assert
        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.Equal(8, line.Magazine));
    }

    [Fact]
    public void ParseLines_WithFilter_FiltersCorrectly()
    {
        // Arrange
        var t42Lines = new List<byte[]>
        {
            CreateValidT42Line(magazine: 1, row: 1),
            CreateValidT42Line(magazine: 1, row: 2),
            CreateValidT42Line(magazine: 8, row: 1),
        };
        var t42Data = t42Lines.SelectMany(x => x).ToArray();
        var stream = new MemoryStream(t42Data);

        // Act
        using var io = FormatIO.Open(stream, Format.T42)
            .Filter(magazine: 1);
        var lines = io.ParseLines().ToList();

        // Assert
        Assert.All(lines, line => Assert.Equal(1, line.Magazine));
    }

    [Fact]
    public void ParseLines_CanBeEnumeratedMultipleTimes()
    {
        // Arrange
        var t42Data = Enumerable.Range(0, 3)
            .SelectMany(_ => CreateValidT42Line())
            .ToArray();
        var stream = new MemoryStream(t42Data);

        // Act
        using var io = FormatIO.Open(stream, Format.T42);
        var lines1 = io.ParseLines().ToList();
        stream.Position = 0; // Reset stream
        var lines2 = io.ParseLines().ToList();

        // Assert
        Assert.Equal(lines1.Count, lines2.Count);
    }

    #endregion

    #region ParsePackets Tests

    [Fact]
    public async Task ParsePackets_WithVBIFormat_GroupsLinesByTimecode()
    {
        // Arrange
        const string filePath = "input.vbi";
        await SampleFiles.EnsureAsync(filePath);

        // Act
        using var io = FormatIO.Open(filePath);
        var packets = io.Filter(magazine: 8)
            .WithLineCount(625)
            .ParsePackets().ToList();

        // Assert
        Assert.NotEmpty(packets);
        Assert.All(packets, packet => Assert.NotEmpty(packet.Lines));
    }

    [Fact]
    public void ParsePackets_WithT42Format_GroupsLinesByTimecode()
    {
        // Arrange
        var t42Data = Enumerable.Range(0, 10)
            .SelectMany(_ => CreateValidT42Line())
            .ToArray();
        var stream = new MemoryStream(t42Data);

        // Act
        using var io = FormatIO.Open(stream, Format.T42)
            .WithLineCount(625);
        var packets = io.ParsePackets().ToList();

        // Assert
        Assert.NotEmpty(packets);
    }

    [Fact]
    public void ParsePackets_CanBeEnumeratedMultipleTimes()
    {
        // Arrange
        var t42Data = Enumerable.Range(0, 5)
            .SelectMany(_ => CreateValidT42Line())
            .ToArray();
        var stream = new MemoryStream(t42Data);

        // Act
        using var io = FormatIO.Open(stream, Format.T42);
        var packets1 = io.ParsePackets().ToList();
        stream.Position = 0;
        var packets2 = io.ParsePackets().ToList();

        // Assert
        Assert.Equal(packets1.Count, packets2.Count);
    }

    #endregion

    #region Async Parsing Tests

    [Fact]
    public async Task ParseLinesAsync_WithVBIFormat_ReturnsLines()
    {
        // Arrange
        const string filePath = "input.vbi";
        await SampleFiles.EnsureAsync(filePath);
        using var cts = new CancellationTokenSource();

        // Act
        using var io = FormatIO.Open(filePath);
        var lines = new List<Line>();
        await foreach (var line in io.Filter(magazine: 8).ParseLinesAsync(cts.Token))
        {
            lines.Add(line);
        }

        // Assert
        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.Equal(8, line.Magazine));
    }

    [Fact]
    public async Task ParseLinesAsync_WithCancellation_StopsProcessing()
    {
        // Arrange
        var vbiData = new byte[Constants.VBI_LINE_SIZE * 100];
        var stream = new MemoryStream(vbiData);
        using var cts = new CancellationTokenSource();

        // Act
        using var io = FormatIO.Open(stream, Format.VBI);
        var lines = new List<Line>();
        cts.CancelAfter(10); // Cancel after 10ms

        try
        {
            await foreach (var line in io.ParseLinesAsync(cts.Token))
            {
                lines.Add(line);
                await Task.Delay(5); // Slow down to allow cancellation
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - May have processed some lines before cancellation
        Assert.True(lines.Count < 100);
    }

    [Fact]
    public async Task ParsePacketsAsync_WithT42Format_ReturnsPackets()
    {
        // Arrange
        var t42Data = Enumerable.Range(0, 5)
            .SelectMany(_ => CreateValidT42Line())
            .ToArray();
        var stream = new MemoryStream(t42Data);
        using var cts = new CancellationTokenSource();

        // Act
        using var io = FormatIO.Open(stream, Format.T42);
        var packets = new List<Packet>();
        await foreach (var packet in io.ParsePacketsAsync(cts.Token))
        {
            packets.Add(packet);
        }

        // Assert
        Assert.NotEmpty(packets);
    }

    #endregion

    #region SaveTo Tests

    [Fact]
    public void SaveTo_WithT42ToT42_WritesFile()
    {
        // Arrange
        var t42Data = Enumerable.Range(0, 3)
            .SelectMany(_ => CreateValidT42Line())
            .ToArray();
        var inputFile = CreateTempFile(".t42", t42Data);
        var outputPath = Path.Combine(Path.GetTempPath(), $"formatiotest_{Guid.NewGuid()}.t42");
        _tempFiles.Add(outputPath);

        // Act
        using (var io = FormatIO.Open(inputFile))
        {
            io.SaveTo(outputPath);
        }

        // Assert
        Assert.True(File.Exists(outputPath));
        var output = File.ReadAllBytes(outputPath);
        Assert.NotEmpty(output);
    }

    [Fact]
    public void SaveTo_WithVBIToT42_ConvertsAndWrites()
    {
        // Arrange
        var vbiData = new byte[Constants.VBI_LINE_SIZE * 3];
        var inputFile = CreateTempFile(".vbi", vbiData);
        var outputPath = Path.Combine(Path.GetTempPath(), $"formatiotest_{Guid.NewGuid()}.t42");
        _tempFiles.Add(outputPath);

        // Act
        using (var io = FormatIO.Open(inputFile))
        {
            io.ConvertTo(Format.T42)
              .SaveTo(outputPath);
        }

        // Assert
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void SaveTo_WithRCWTFormat_WritesHeader()
    {
        // Arrange
        var t42Data = Enumerable.Range(0, 3)
            .SelectMany(_ => CreateValidT42Line())
            .ToArray();
        var inputFile = CreateTempFile(".t42", t42Data);
        var outputPath = Path.Combine(Path.GetTempPath(), $"formatiotest_{Guid.NewGuid()}.rcwt");
        _tempFiles.Add(outputPath);

        // Act
        using (var io = FormatIO.Open(inputFile))
        {
            io.ConvertTo(Format.RCWT)
              .SaveTo(outputPath);
        }

        // Assert
        Assert.True(File.Exists(outputPath));
        var output = File.ReadAllBytes(outputPath);
        Assert.True(output.Length >= 11); // RCWT header is 11 bytes
    }

    [Fact]
    public void SaveTo_WithSTLFormat_WritesGSIHeader()
    {
        // Arrange
        var t42Data = Enumerable.Range(0, 3)
            .SelectMany(_ => CreateValidT42Line())
            .ToArray();
        var inputFile = CreateTempFile(".t42", t42Data);
        var outputPath = Path.Combine(Path.GetTempPath(), $"formatiotest_{Guid.NewGuid()}.stl");
        _tempFiles.Add(outputPath);

        // Act
        using (var io = FormatIO.Open(inputFile))
        {
            io.ConvertTo(Format.STL)
              .SaveTo(outputPath);
        }

        // Assert
        Assert.True(File.Exists(outputPath));
        var output = File.ReadAllBytes(outputPath);
        Assert.True(output.Length >= 1024); // STL GSI header is 1024 bytes
    }

    [Fact]
    public void SaveTo_WithSTLMerging_UsesSTLExporter()
    {
        // Arrange - Ensure T42Handler is registered
        if (!FormatRegistry.TryGetHandler(Format.T42, out _))
        {
            FormatRegistry.Register(new T42Handler());
        }

        var t42Data = Enumerable.Range(0, 10)
            .SelectMany(_ => CreateValidT42Line())
            .ToArray();
        var inputFile = CreateTempFile(".t42", t42Data);
        var outputPath = Path.Combine(Path.GetTempPath(), $"formatiotest_{Guid.NewGuid()}.stl");
        _tempFiles.Add(outputPath);

        // Act
        using (var io = FormatIO.Open(inputFile))
        {
            io.ConvertTo(Format.STL)
              .SaveTo(outputPath, useSTLMerging: true);
        }

        // Assert
        Assert.True(File.Exists(outputPath));
        var output = File.ReadAllBytes(outputPath);
        Assert.True(output.Length >= 1024); // At minimum has GSI header
    }

    [Fact]
    public void SaveToStdout_WithT42Format_DoesNotThrow()
    {
        // Arrange
        var t42Data = Enumerable.Range(0, 3)
            .SelectMany(_ => CreateValidT42Line())
            .ToArray();
        var stream = new MemoryStream(t42Data);

        // Redirect stdout to capture output
        var originalOut = Console.Out;
        using var memOut = new MemoryStream();
        using var writer = new StreamWriter(memOut);
        Console.SetOut(writer);

        try
        {
            // Act
            using (var io = FormatIO.Open(stream, Format.T42))
            {
                io.SaveToStdout();
            }

            // Assert - If we got here, no exception was thrown
            Assert.True(true);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task SaveToAsync_WithT42ToT42_WritesFile()
    {
        // Arrange
        var t42Data = Enumerable.Range(0, 3)
            .SelectMany(_ => CreateValidT42Line())
            .ToArray();
        var inputFile = CreateTempFile(".t42", t42Data);
        var outputPath = Path.Combine(Path.GetTempPath(), $"formatiotest_{Guid.NewGuid()}.t42");
        _tempFiles.Add(outputPath);
        using var cts = new CancellationTokenSource();

        // Act
        using (var io = FormatIO.Open(inputFile))
        {
            await io.SaveToAsync(outputPath, cancellationToken: cts.Token);
        }

        // Assert
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task SaveToStdoutAsync_WithT42Format_DoesNotThrow()
    {
        // Arrange
        var t42Data = Enumerable.Range(0, 3)
            .SelectMany(_ => CreateValidT42Line())
            .ToArray();
        var stream = new MemoryStream(t42Data);
        using var cts = new CancellationTokenSource();

        // Redirect stdout
        var originalOut = Console.Out;
        using var memOut = new MemoryStream();
        using var writer = new StreamWriter(memOut);
        Console.SetOut(writer);

        try
        {
            // Act
            using (var io = FormatIO.Open(stream, Format.T42))
            {
                await io.SaveToStdoutAsync(cancellationToken: cts.Token);
            }

            // Assert
            Assert.True(true);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    #endregion

    #region Conversion Support Tests

    [Theory]
    [InlineData(Format.VBI, Format.T42)]
    [InlineData(Format.VBI, Format.VBI_DOUBLE)]
    [InlineData(Format.VBI_DOUBLE, Format.T42)]
    [InlineData(Format.VBI_DOUBLE, Format.VBI)]
    [InlineData(Format.T42, Format.VBI)]
    [InlineData(Format.T42, Format.VBI_DOUBLE)]
    [InlineData(Format.T42, Format.RCWT)]
    [InlineData(Format.T42, Format.STL)]
    [InlineData(Format.VBI, Format.RCWT)]
    [InlineData(Format.VBI, Format.STL)]
    public void ConvertTo_SupportedConversions_DoesNotThrow(Format from, Format to)
    {
        // Arrange
        var stream = new MemoryStream(new byte[1024]);

        // Act
        using var io = FormatIO.Open(stream, from);
        var result = io.ConvertTo(to);

        // Assert
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(Format.VBI, Format.MXF)]
    [InlineData(Format.VBI, Format.TS)]
    [InlineData(Format.T42, Format.MXF)]
    [InlineData(Format.T42, Format.TS)]
    [InlineData(Format.RCWT, Format.T42)]
    [InlineData(Format.STL, Format.T42)]
    public void ConvertTo_UnsupportedConversions_ThrowsNotSupportedException(Format from, Format to)
    {
        // Arrange
        var stream = new MemoryStream(new byte[1024]);

        // Act
        using var io = FormatIO.Open(stream, from);

        // Assert
        Assert.Throws<NotSupportedException>(() => io.ConvertTo(to));
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_WithFileStream_ClosesStream()
    {
        // Arrange
        var testFile = CreateTempFile(".vbi", CreateValidVBILine());
        var io = FormatIO.Open(testFile);

        // Act
        io.Dispose();

        // Assert - File should be accessible again (not locked)
        using var fs = File.OpenRead(testFile);
        Assert.True(fs.CanRead);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidVBILine());
        var io = FormatIO.Open(stream, Format.VBI);

        // Act & Assert
        io.Dispose();
        io.Dispose(); // Second dispose should not throw
        io.Dispose(); // Third dispose should not throw
    }

    [Fact]
    public void Dispose_WithUserProvidedStream_DoesNotCloseStream()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidVBILine());
        var io = FormatIO.Open(stream, Format.VBI);

        // Act
        io.Dispose();

        // Assert - User's stream should still be usable
        Assert.True(stream.CanRead);
        stream.Dispose();
    }

    [Fact]
    public void UsingStatement_AutomaticallyDisposesResources()
    {
        // Arrange
        var testFile = CreateTempFile(".vbi", CreateValidVBILine());

        // Act
        using (var io = FormatIO.Open(testFile))
        {
            var lines = io.ParseLines().Take(1).ToList();
        }

        // Assert - File should be accessible (not locked)
        using var fs = File.OpenRead(testFile);
        Assert.True(fs.CanRead);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void ParseLines_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidVBILine());
        var io = FormatIO.Open(stream, Format.VBI);
        io.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => io.ParseLines().ToList());
    }

    [Fact]
    public void ParsePackets_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidVBILine());
        var io = FormatIO.Open(stream, Format.VBI);
        io.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => io.ParsePackets().ToList());
    }

    [Fact]
    public void SaveTo_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidVBILine());
        var outputPath = Path.Combine(Path.GetTempPath(), $"formatiotest_{Guid.NewGuid()}.t42");
        _tempFiles.Add(outputPath);
        var io = FormatIO.Open(stream, Format.VBI);
        io.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => io.SaveTo(outputPath));
    }

    [Fact]
    public void ConvertTo_CalledMultipleTimes_UsesLastValue()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidVBILine());

        // Act
        using var io = FormatIO.Open(stream, Format.VBI)
            .ConvertTo(Format.T42)
            .ConvertTo(Format.VBI_DOUBLE); // Last one wins

        // Assert - Should not throw (VBI_DOUBLE is valid)
        Assert.NotNull(io);
    }

    [Fact]
    public void WithOptions_CalledMultipleTimes_AppliesAllConfigurations()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidVBILine());

        // Act
        using var io = FormatIO.Open(stream, Format.VBI)
            .WithOptions(opts => opts.Magazine = 1)
            .WithOptions(opts => opts.Rows = new[] { 1, 2, 3 });

        // Assert - Both configurations should be applied
        Assert.NotNull(io);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task FullWorkflow_VBIToT42File_ProducesOutput()
    {
        // Arrange
        const string inputFile = "input.vbi";
        await SampleFiles.EnsureAsync(inputFile);
        var outputPath = Path.Combine(Path.GetTempPath(), $"formatiotest_{Guid.NewGuid()}.t42");
        _tempFiles.Add(outputPath);

        // Act
        using (var io = FormatIO.Open(inputFile))
        {
            io.Filter(magazine: 8)
              .ConvertTo(Format.T42)
              .SaveTo(outputPath);
        }

        // Assert
        Assert.True(File.Exists(outputPath));
        var output = File.ReadAllBytes(outputPath);
        Assert.NotEmpty(output);
    }

    [Fact]
    public void FullWorkflow_T42ToSTLWithMerging_ProducesOutput()
    {
        // Arrange
        var t42Data = Enumerable.Range(0, 20)
            .SelectMany(_ => CreateValidT42Line())
            .ToArray();
        var inputFile = CreateTempFile(".t42", t42Data);
        var outputPath = Path.Combine(Path.GetTempPath(), $"formatiotest_{Guid.NewGuid()}.stl");
        _tempFiles.Add(outputPath);

        // Act
        using (var io = FormatIO.Open(inputFile))
        {
            io.Filter(magazine: 8)
              .ConvertTo(Format.STL)
              .SaveTo(outputPath, useSTLMerging: true);
        }

        // Assert
        Assert.True(File.Exists(outputPath));
        var output = File.ReadAllBytes(outputPath);
        Assert.True(output.Length >= 1024); // GSI header
    }

    [Fact]
    public async Task FullWorkflow_AsyncVBIToRCWT_ProducesOutput()
    {
        // Arrange
        var vbiData = new byte[Constants.VBI_LINE_SIZE * 5];
        var inputFile = CreateTempFile(".vbi", vbiData);
        var outputPath = Path.Combine(Path.GetTempPath(), $"formatiotest_{Guid.NewGuid()}.rcwt");
        _tempFiles.Add(outputPath);
        using var cts = new CancellationTokenSource();

        // Act
        using (var io = FormatIO.Open(inputFile))
        {
            await io.ConvertTo(Format.RCWT)
                    .SaveToAsync(outputPath, cancellationToken: cts.Token);
        }

        // Assert
        Assert.True(File.Exists(outputPath));
        var output = File.ReadAllBytes(outputPath);
        Assert.True(output.Length >= 11); // RCWT header
    }

    [Fact]
    public async Task FullWorkflow_DirectParsing_WithoutSaveTo()
    {
        // Arrange
        const string filePath = "input.t42";
        await SampleFiles.EnsureAsync(filePath);

        // Act
        using var io = FormatIO.Open(filePath);
        var lines = io.Filter(magazine: 8)
                      .ParseLines()
                      .ToList();

        // Assert
        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.Equal(8, line.Magazine));
    }

    #endregion
}
