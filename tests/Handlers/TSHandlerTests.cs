using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Handlers;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Handlers;

/// <summary>
/// Unit tests for the TSHandler class.
/// Tests MPEG-TS format parsing with input.ts sample file.
/// </summary>
[Collection("Sample Files Sequential")]
public class TSHandlerTests : IAsyncLifetime, IDisposable
{
    private readonly List<Stream> _streamsToDispose = [];
    private byte[]? _sampleData;

    public async Task InitializeAsync()
    {
        // Load input.ts sample file once for all tests
        var filePath = "input.ts";
        if (!File.Exists(filePath))
        {
            await SampleFiles.EnsureAsync(filePath);
        }
        if (File.Exists(filePath))
        {
            _sampleData = await File.ReadAllBytesAsync(filePath);
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
    }

    [Fact]
    public void Constructor_DefaultParameters_SetsProperties()
    {
        // Act
        var handler = new TSHandler();

        // Assert
        Assert.Equal(Format.TS, handler.InputFormat);
        Assert.Equal(25, handler.FrameRate); // Default frame rate
        Assert.Contains(Format.T42, handler.ValidOutputs);
        Assert.Contains(Format.VBI, handler.ValidOutputs);
        Assert.Contains(Format.VBI_DOUBLE, handler.ValidOutputs);
    }

    [Fact]
    public void Constructor_WithCustomFrameRate_SetsFrameRate()
    {
        // Act
        var handler = new TSHandler(frameRate: 30);

        // Assert
        Assert.Equal(30, handler.FrameRate);
    }

    [Fact]
    public void Constructor_WithPIDs_AllowsManualFiltering()
    {
        // Act
        var handler = new TSHandler(pids: new[] { 100, 200 });

        // Assert - No exception thrown
        Assert.Equal(Format.TS, handler.InputFormat);
    }

    [Fact]
    public void Parse_NullStream_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new TSHandler();
        var options = new ParseOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => handler.Parse(null!, options).ToList());
    }

    [Fact]
    public void Parse_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new TSHandler();
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => handler.Parse(stream, null!).ToList());
    }

    [Fact]
    public void Parse_EmptyStream_ReturnsNoPackets()
    {
        // Arrange
        var handler = new TSHandler();
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };

        // Act
        var packets = handler.Parse(stream, options).ToList();

        // Assert
        Assert.Empty(packets);
    }

    [Fact]
    public void Parse_WithSampleData_ReturnsPackets()
    {
        // Arrange
        var handler = new TSHandler();
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
        var handler = new TSHandler();
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
        var handler = new TSHandler();
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
        Assert.All(packets, packet => Assert.Contains(21, packet.Row));
    }

    [Fact]
    public async Task ParseAsync_WithSampleData_ReturnsPackets()
    {
        // Arrange
        var handler = new TSHandler();
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
        var handler = new TSHandler();
        var stream = new MemoryStream(_sampleData!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
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
        var handler = new TSHandler();
        var stream = new MemoryStream(_sampleData!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };

        // Act
        var packets = handler.Parse(stream, options).ToList();

        // Assert
        // input.ts should contain 50 packets (1 packet per frame × 50 frames @ 25fps)
        Assert.Equal(50, packets.Count);
    }

    [Fact]
    public void Parse_AutoDetectMode_ExtractsPackets()
    {
        // Arrange
        var handler = new TSHandler(autoDetect: true); // Auto-detect teletext PIDs
        var stream = new MemoryStream(_sampleData!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };

        // Act
        var packets = handler.Parse(stream, options).ToList();

        // Assert
        Assert.NotEmpty(packets);
    }

    [Fact]
    public void Parse_WithPIDsInOptions_FiltersCorrectly()
    {
        // Arrange
        var handler = new TSHandler();
        var stream = new MemoryStream(_sampleData!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions
        {
            OutputFormat = Format.T42,
            PIDs = new[] { 0x1234 } // Specify PID in options
        };

        // Act
        var packets = handler.Parse(stream, options).ToList();

        // Assert - May be empty if PID doesn't exist in stream, but shouldn't throw
        // Just verify it doesn't crash
        Assert.NotNull(packets);
    }

    [Fact]
    public void FrameRate_CanBeModified()
    {
        // Arrange
        var handler = new TSHandler(frameRate: 25);

        // Act
        handler.FrameRate = 30;

        // Assert
        Assert.Equal(30, handler.FrameRate);
    }

    [Fact]
    public void Parse_WithDifferentFrameRates_DoesNotThrow()
    {
        // Arrange - Test with different common frame rates
        var frameRates = new[] { 24, 25, 30, 50, 60 };
        var stream = new MemoryStream(_sampleData!);
        _streamsToDispose.Add(stream);

        foreach (var frameRate in frameRates)
        {
            // Reset stream position
            stream.Position = 0;

            // Act
            var handler = new TSHandler(frameRate: frameRate);
            var options = new ParseOptions { OutputFormat = Format.T42 };
            var packets = handler.Parse(stream, options).ToList();

            // Assert
            Assert.NotEmpty(packets);
        }
    }
}
