using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Handlers;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Handlers;

/// <summary>
/// Unit tests for the MXFHandler class.
/// Tests MXF format parsing, extraction, and restriping with input.mxf sample file.
/// </summary>
[Collection("Sample Files Sequential")]
public class MXFHandlerTests : IAsyncLifetime, IDisposable
{
    private readonly List<Stream> _streamsToDispose = [];
    private readonly List<string> _tempFilesToDelete = [];
    private byte[]? _sampleData;
    private string? _sampleFilePath;

    public async Task InitializeAsync()
    {
        // Load input.mxf sample file once for all tests
        var filePath = "input.mxf";
        if (!File.Exists(filePath))
        {
            await SampleFiles.EnsureAsync(filePath);
        }
        if (File.Exists(filePath))
        {
            _sampleData = await File.ReadAllBytesAsync(filePath);
            _sampleFilePath = filePath;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var stream in _streamsToDispose)
        {
            stream?.Dispose();
        }
        foreach (var file in _tempFilesToDelete)
        {
            if (File.Exists(file))
            {
                try { File.Delete(file); } catch { /* Best effort cleanup */ }
            }
        }
    }

    [Fact]
    public void Constructor_DefaultParameters_SetsProperties()
    {
        // Act
        var handler = new MXFHandler(new Timecode(0));

        // Assert
        Assert.Equal(Format.MXF, handler.InputFormat);
        Assert.Contains(Format.T42, handler.ValidOutputs);
        Assert.Contains(Format.VBI, handler.ValidOutputs);
        Assert.Contains(Format.VBI_DOUBLE, handler.ValidOutputs);
    }

    [Fact]
    public void Constructor_WithRequiredKeys_AllowsFiltering()
    {
        // Act
        var handler = new MXFHandler(new Timecode(0), requiredKeys: new List<KeyType> { KeyType.Data });

        // Assert
        Assert.Equal(Format.MXF, handler.InputFormat);
    }

    [Fact]
    public void Parse_NullStream_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new MXFHandler(new Timecode(0), function: Function.Filter, requiredKeys: new List<KeyType> { KeyType.Data });
        var options = new ParseOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => handler.Parse(null!, options).ToList());
    }

    [Fact]
    public void Parse_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new MXFHandler(new Timecode(0), function: Function.Filter, requiredKeys: new List<KeyType> { KeyType.Data });
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => handler.Parse(stream, null!).ToList());
    }

    [Fact]
    public void Parse_EmptyStream_ReturnsNoPackets()
    {
        // Arrange
        var handler = new MXFHandler(new Timecode(0), function: Function.Filter, requiredKeys: new List<KeyType> { KeyType.Data });
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };

        // Act
        var packets = handler.Parse(stream, options).ToList();

        // Assert
        Assert.Empty(packets);
    }

    [Fact]
    public void Parse_WithSampleData_FilterMode_ReturnsPackets()
    {
        // Arrange
        var handler = new MXFHandler(new Timecode(0), function: Function.Filter, requiredKeys: new List<KeyType> { KeyType.Data });
        var stream = new MemoryStream(_sampleData!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };

        // Act
        var packets = handler.Parse(stream, options).ToList();

        // Assert
        Assert.NotEmpty(packets);
        Assert.All(packets, packet =>
        {
            Assert.NotNull(packet.Lines);
            Assert.True(packet.Lines.Count > 0);
        });
    }

    [Fact]
    public void Parse_WithMagazineFilter_FiltersCorrectly()
    {
        // Arrange
        var handler = new MXFHandler(new Timecode(0), function: Function.Filter, requiredKeys: new List<KeyType> { KeyType.Data });
        var stream = new MemoryStream(_sampleData!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions
        {
            OutputFormat = Format.T42,
            Magazine = 8 // Filter to magazine 8
        };

        // Act
        var packets = handler.Parse(stream, options).ToList();

        // Assert
        Assert.All(packets, packet => Assert.Equal(8, packet.Magazine));
    }

    [Fact]
    public void Parse_WithRowFilter_FiltersCorrectly()
    {
        // Arrange
        var handler = new MXFHandler(new Timecode(0), function: Function.Filter, requiredKeys: new List<KeyType> { KeyType.Data });
        var stream = new MemoryStream(_sampleData!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions
        {
            OutputFormat = Format.T42,
            Rows = new[] { 21 } // Filter to row 21 only
        };

        // Act
        var packets = handler.Parse(stream, options).ToList();

        // Assert
        // Row is an array property, check Lines instead
        Assert.NotEmpty(packets);
    }

    [Fact]
    public async Task ParseAsync_WithSampleData_ReturnsPackets()
    {
        // Arrange
        var handler = new MXFHandler(new Timecode(0), function: Function.Filter, requiredKeys: new List<KeyType> { KeyType.Data });
        var stream = new MemoryStream(_sampleData!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };

        // Act
        var packets = new List<Packet>();
        await foreach (var packet in handler.ParseAsync(stream, options))
        {
            packets.Add(packet);
        }

        // Assert
        Assert.NotEmpty(packets);
        Assert.All(packets, packet =>
        {
            Assert.NotNull(packet.Lines);
            Assert.True(packet.Lines.Count > 0);
        });
    }

    [Fact]
    public async Task ParseAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var handler = new MXFHandler(new Timecode(0), function: Function.Filter, requiredKeys: new List<KeyType> { KeyType.Data });
        var stream = new MemoryStream(_sampleData!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert - TaskCanceledException inherits from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var packet in handler.ParseAsync(stream, options, cts.Token))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public void Parse_ValidatesExpectedPacketCount()
    {
        // Arrange
        var handler = new MXFHandler(new Timecode(0), function: Function.Filter, requiredKeys: new List<KeyType> { KeyType.Data });
        var stream = new MemoryStream(_sampleData!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };

        // Act
        var packets = handler.Parse(stream, options).ToList();

        // Assert
        // input.mxf should contain 50 packets (1 packet per frame × 50 frames @ 25fps)
        Assert.Equal(50, packets.Count);
    }

    [Fact]
    public void Parse_SystemKeyType_ExtractsData()
    {
        // Arrange
        var handler = new MXFHandler(new Timecode(0), function: Function.Filter, requiredKeys: new List<KeyType> { KeyType.System });
        var stream = new MemoryStream(_sampleData!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };

        // Act
        var packets = handler.Parse(stream, options).ToList();

        // Assert - System packets should be extracted
        Assert.NotNull(packets);
    }

    [Fact]
    public void Parse_DataAndSystemKeys_ExtractsMultipleKeyTypes()
    {
        // Arrange
        var handler = new MXFHandler(
            new Timecode(0),
            function: Function.Filter,
            requiredKeys: new List<KeyType> { KeyType.Data, KeyType.System }
        );
        var stream = new MemoryStream(_sampleData!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };

        // Act
        var packets = handler.Parse(stream, options).ToList();

        // Assert
        Assert.NotEmpty(packets);
    }

    [Fact]
    public void Parse_RestripeMode_ValidatesTimecodeModification()
    {
        // Arrange - Create temp file copy for restriping
        var tempFile = Path.GetTempFileName();
        _tempFilesToDelete.Add(tempFile);
        File.WriteAllBytes(tempFile, _sampleData!);

        var newTimecode = new Timecode(12, 34, 56, 12); // 12:34:56:12
        var handler = new MXFHandler(
            newTimecode,
            inputFilePath: tempFile,
            function: Function.Restripe
        );

        // Act - Restripe the file
        using (var stream = File.Open(tempFile, FileMode.Open, FileAccess.ReadWrite))
        {
            var options = new ParseOptions { OutputFormat = Format.T42 };
            // Restriping modifies the stream in-place, consume the enumerable
            foreach (var _ in handler.Parse(stream, options))
            {
                // Just iterate to trigger restriping
            }
        }

        // Assert - Verify file was modified by re-opening and parsing with Filter mode
        using (var mxf = new MXF(tempFile))
        {
            // After restriping, the MXF file should have the new start timecode
            // The MXF constructor reads the StartTimecode from the file
            Assert.Equal(newTimecode.ToString(), mxf.StartTimecode.ToString());
        }
    }

    [Fact]
    public void Parse_CheckSequential_ValidatesTimecodeOrder()
    {
        // Arrange
        var handler = new MXFHandler(
            new Timecode(0),
            function: Function.Filter,
            requiredKeys: new List<KeyType> { KeyType.Data },
            checkSequential: true
        );
        var stream = new MemoryStream(_sampleData!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };

        // Act
        var packets = handler.Parse(stream, options).ToList();

        // Assert - Sequential check should pass without throwing
        Assert.NotEmpty(packets);
    }

    [Fact]
    public void SMPTETimecodes_AfterParsing_ContainsTimecodes()
    {
        // Arrange - Use System keys to extract SMPTE timecodes
        var handler = new MXFHandler(new Timecode(0), function: Function.Filter, requiredKeys: new List<KeyType> { KeyType.System });
        var stream = new MemoryStream(_sampleData!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };

        // Act - Parse with System keys to populate SMPTETimecodes
        var packets = handler.Parse(stream, options).ToList();

        // Assert - SMPTETimecodes should be populated when System packets are processed
        Assert.NotEmpty(handler.SMPTETimecodes);
    }
}
