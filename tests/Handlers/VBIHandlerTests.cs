using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;
using nathanbutlerDEV.libopx.Handlers;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Handlers;

/// <summary>
/// Unit tests for the VBIHandler class.
/// Tests VBI format parsing with input.vbi and input.vbid sample files.
/// </summary>
[Collection("Sample Files Sequential")]
public class VBIHandlerTests : IAsyncLifetime, IDisposable
{
    private readonly List<Stream> _streamsToDispose = [];
    private byte[]? _sampleDataVbi;
    private byte[]? _sampleDataVbid;

    public async Task InitializeAsync()
    {
        // Load input.vbi sample file
        var filePathVbi = "input.vbi";
        if (!File.Exists(filePathVbi))
        {
            await SampleFiles.EnsureAsync(filePathVbi);
        }
        if (File.Exists(filePathVbi))
        {
            _sampleDataVbi = await File.ReadAllBytesAsync(filePathVbi);
        }

        // Load input.vbid sample file
        var filePathVbid = "input.vbid";
        if (!File.Exists(filePathVbid))
        {
            await SampleFiles.EnsureAsync(filePathVbid);
        }
        if (File.Exists(filePathVbid))
        {
            _sampleDataVbid = await File.ReadAllBytesAsync(filePathVbid);
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
    public void Constructor_DefaultVBI_SetsProperties()
    {
        // Act
        var handler = new VBIHandler();

        // Assert
        Assert.Equal(Format.VBI, handler.InputFormat);
        Assert.Equal(Constants.VBI_LINE_SIZE, handler.LineLength);
        Assert.Contains(Format.VBI, handler.ValidOutputs);
        Assert.Contains(Format.VBI_DOUBLE, handler.ValidOutputs);
        Assert.Contains(Format.T42, handler.ValidOutputs);
    }

    [Fact]
    public void Constructor_VBIDouble_SetsProperties()
    {
        // Act
        var handler = new VBIHandler(Format.VBI_DOUBLE);

        // Assert
        Assert.Equal(Format.VBI_DOUBLE, handler.InputFormat);
        Assert.Equal(Constants.VBI_DOUBLE_LINE_SIZE, handler.LineLength);
        Assert.Contains(Format.VBI, handler.ValidOutputs);
        Assert.Contains(Format.VBI_DOUBLE, handler.ValidOutputs);
        Assert.Contains(Format.T42, handler.ValidOutputs);
    }

    [Fact]
    public void Parse_NullStream_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new VBIHandler();
        var options = new ParseOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => handler.Parse(null!, options).ToList());
    }

    [Fact]
    public void Parse_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new VBIHandler();
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => handler.Parse(stream, null!).ToList());
    }

    [Fact]
    public void Parse_EmptyStream_ReturnsNoLines()
    {
        // Arrange
        var handler = new VBIHandler();
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.VBI };

        // Act
        var lines = handler.Parse(stream, options).ToList();

        // Assert
        Assert.Empty(lines);
    }

    [Fact]
    public void Parse_WithSampleDataVBI_ReturnsLines()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleDataVbi == null || _sampleDataVbi.Length == 0)
        {
            return;
        }

        // Arrange
        var handler = new VBIHandler(Format.VBI);
        var stream = new MemoryStream(_sampleDataVbi);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.VBI };

        // Act
        var lines = handler.Parse(stream, options).ToList();

        // Assert
        Assert.NotEmpty(lines);
        Assert.All(lines, line =>
        {
            Assert.Equal(Constants.VBI_LINE_SIZE, line.Length);
            Assert.NotNull(line.Data);
            Assert.Equal(0x31, line.SampleCoding); // VBI sample coding
        });
    }

    [Fact]
    public void Parse_WithSampleDataVBIDouble_ReturnsLines()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleDataVbid == null || _sampleDataVbid.Length == 0)
        {
            return;
        }

        // Arrange
        var handler = new VBIHandler(Format.VBI_DOUBLE);
        var stream = new MemoryStream(_sampleDataVbid);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.VBI_DOUBLE };

        // Act
        var lines = handler.Parse(stream, options).ToList();

        // Assert
        Assert.NotEmpty(lines);
        Assert.All(lines, line =>
        {
            Assert.Equal(Constants.VBI_DOUBLE_LINE_SIZE, line.Length);
            Assert.NotNull(line.Data);
            Assert.Equal(0x32, line.SampleCoding); // VBI_DOUBLE sample coding
        });
    }

    [Fact]
    public void Parse_WithMagazineFilter_FiltersCorrectly()
    {
        // Arrange
        var handler = new VBIHandler();
        var stream = new MemoryStream(_sampleDataVbi!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions
        {
            OutputFormat = Format.VBI,
            Magazine = 8 // Filter to magazine 8
        };

        // Act
        var lines = handler.Parse(stream, options).ToList();

        // Assert
        Assert.All(lines, line => Assert.Equal(8, line.Magazine));
    }

    [Fact]
    public void Parse_VBIToT42_DoesNotThrowException()
    {
        // Arrange
        var handler = new VBIHandler();
        var stream = new MemoryStream(_sampleDataVbi!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };

        // Act
        var lines = handler.Parse(stream, options).ToList();

        // Assert - conversion may filter out invalid lines, so just check no exception
        // If lines are returned, they should have correct format
        foreach (var line in lines)
        {
            Assert.Equal(Constants.T42_LINE_SIZE, line.Length);
            Assert.NotNull(line.Data);
        }
    }

    [Fact]
    public void Parse_VBIToVBIDouble_DoesNotThrowException()
    {
        // Arrange
        var handler = new VBIHandler(Format.VBI);
        var stream = new MemoryStream(_sampleDataVbi!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.VBI_DOUBLE };

        // Act
        var lines = handler.Parse(stream, options).ToList();

        // Assert - conversion may filter out invalid lines, so just check no exception
        // If lines are returned, they should have correct format
        foreach (var line in lines)
        {
            Assert.Equal(Constants.VBI_DOUBLE_LINE_SIZE, line.Length);
            Assert.Equal(0x32, line.SampleCoding);
        }
    }

    [Fact]
    public async Task ParseAsync_WithSampleDataVBI_ReturnsLines()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleDataVbi == null || _sampleDataVbi.Length == 0)
        {
            return;
        }

        // Arrange
        var handler = new VBIHandler();
        var stream = new MemoryStream(_sampleDataVbi);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.VBI };

        // Act
        var lines = new List<Line>();
        await foreach (var line in handler.ParseAsync(stream, options))
        {
            lines.Add(line);
        }

        // Assert
        Assert.NotEmpty(lines);
        Assert.All(lines, line =>
        {
            Assert.Equal(Constants.VBI_LINE_SIZE, line.Length);
            Assert.NotNull(line.Data);
        });
    }

    [Fact]
    public async Task ParseAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var handler = new VBIHandler();
        var stream = new MemoryStream(_sampleDataVbi!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.VBI };
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var line in handler.ParseAsync(stream, options, cts.Token))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public void Parse_WithRowFilter_FiltersCorrectly()
    {
        // Arrange
        var handler = new VBIHandler();
        var stream = new MemoryStream(_sampleDataVbi!);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions
        {
            OutputFormat = Format.VBI,
            Rows = new[] { 21 } // Filter to row 21 only
        };

        // Act
        var lines = handler.Parse(stream, options).ToList();

        // Assert
        Assert.All(lines, line => Assert.Equal(21, line.Row));
    }

    [Fact]
    public void Parse_ValidatesExpectedLineCount()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleDataVbi == null || _sampleDataVbi.Length == 0)
        {
            return;
        }

        // Arrange
        var handler = new VBIHandler();
        var stream = new MemoryStream(_sampleDataVbi);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.VBI };

        // Act
        var lines = handler.Parse(stream, options).ToList();

        // Assert
        // input.vbi should contain 100 lines (2 lines per frame × 50 frames @ 25fps)
        // Each frame has: 1 header row (line with coding) + 1 caption row
        Assert.Equal(100, lines.Count);
    }

    [Fact]
    public void Parse_VBIDouble_ValidatesExpectedLineCount()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleDataVbid == null || _sampleDataVbid.Length == 0)
        {
            return;
        }

        // Arrange
        var handler = new VBIHandler(Format.VBI_DOUBLE);
        var stream = new MemoryStream(_sampleDataVbid);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.VBI_DOUBLE };

        // Act
        var lines = handler.Parse(stream, options).ToList();

        // Assert
        // input.vbid should contain 100 lines (2 lines per frame × 50 frames @ 25fps)
        Assert.Equal(100, lines.Count);
    }
}
