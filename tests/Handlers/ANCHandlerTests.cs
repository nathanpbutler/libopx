using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Handlers;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Handlers;

/// <summary>
/// Unit tests for the ANCHandler class.
/// Tests ANC (Ancillary Data) format parsing with input.bin sample file.
/// </summary>
[Collection("Sample Files Sequential")]
public class ANCHandlerTests : IAsyncLifetime, IDisposable
{
    private readonly List<Stream> _streamsToDispose = [];
    private byte[]? _sampleData;

    public async Task InitializeAsync()
    {
        // Load input.bin sample file once for all tests
        var filePath = "input.bin";
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
    public void Constructor_SetsProperties()
    {
        // Act
        var handler = new ANCHandler();

        // Assert
        Assert.Equal(Format.ANC, handler.InputFormat);
        Assert.Contains(Format.T42, handler.ValidOutputs);
        Assert.Contains(Format.VBI, handler.ValidOutputs);
        Assert.Contains(Format.VBI_DOUBLE, handler.ValidOutputs);
        Assert.Contains(Format.RCWT, handler.ValidOutputs);
        Assert.Contains(Format.STL, handler.ValidOutputs);
    }

    [Fact]
    public void Parse_NullStream_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new ANCHandler();
        var options = new ParseOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => handler.Parse(null!, options).ToList());
    }

    [Fact]
    public void Parse_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new ANCHandler();
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => handler.Parse(stream, null!).ToList());
    }

    [Fact]
    public void Parse_EmptyStream_ReturnsNoPackets()
    {
        // Arrange
        var handler = new ANCHandler();
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
        var handler = new ANCHandler();
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
        var handler = new ANCHandler();
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
        var handler = new ANCHandler();
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
        var handler = new ANCHandler();
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
        var handler = new ANCHandler();
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
        var handler = new ANCHandler();
        var stream = new MemoryStream(_sampleData!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };

        // Act
        var packets = handler.Parse(stream, options).ToList();

        // Assert
        // input.bin should contain 50 packets (1 packet per frame × 50 frames @ 25fps)
        Assert.Equal(50, packets.Count);
    }

    [Fact]
    public void Parse_WithStartTimecode_SetsCorrectTimecodes()
    {
        // Arrange
        var handler = new ANCHandler();
        var stream = new MemoryStream(_sampleData!);
        _streamsToDispose.Add(stream);
        var startTimecode = new Timecode(12, 34, 56, 12); // 12:34:56:12
        var options = new ParseOptions
        {
            OutputFormat = Format.T42,
            StartTimecode = startTimecode
        };

        // Act
        var packets = handler.Parse(stream, options).ToList();

        // Assert
        Assert.NotEmpty(packets);
        // First packet should have the start timecode
        Assert.Equal(startTimecode, packets[0].Timecode);
    }

    [Fact]
    public void Parse_OutputToVBI_ProducesValidFormat()
    {
        // Arrange
        var handler = new ANCHandler();
        var stream = new MemoryStream(_sampleData!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.VBI };

        // Act
        var packets = handler.Parse(stream, options).ToList();

        // Assert - conversion should work without throwing
        Assert.NotEmpty(packets);
    }
}
