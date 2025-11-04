using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;
using nathanbutlerDEV.libopx.Handlers;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Handlers;

/// <summary>
/// Unit tests for the T42Handler class.
/// Tests T42 format parsing with various options and output formats.
/// </summary>
public class T42HandlerTests : IDisposable
{
    private readonly List<Stream> _streamsToDispose = [];

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
        var handler = new T42Handler();

        // Assert
        Assert.Equal(Format.T42, handler.InputFormat);
        Assert.Equal(Constants.T42_LINE_SIZE, handler.LineLength);
        Assert.Contains(Format.T42, handler.ValidOutputs);
        Assert.Contains(Format.VBI, handler.ValidOutputs);
        Assert.Contains(Format.VBI_DOUBLE, handler.ValidOutputs);
    }

    [Fact]
    public void Parse_NullStream_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new T42Handler();
        var options = new ParseOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => handler.Parse(null!, options).ToList());
    }

    [Fact]
    public void Parse_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new T42Handler();
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => handler.Parse(stream, null!).ToList());
    }

    [Fact]
    public void Parse_EmptyStream_ReturnsNoLines()
    {
        // Arrange
        var handler = new T42Handler();
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };

        // Act
        var lines = handler.Parse(stream, options).ToList();

        // Assert
        Assert.Empty(lines);
    }

    [Fact]
    public void Parse_WithSampleData_ReturnsLines()
    {
        // Arrange
        var handler = new T42Handler();
        var stream = new MemoryStream(T42.Sample);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };

        // Act
        var lines = handler.Parse(stream, options).ToList();

        // Assert
        Assert.NotEmpty(lines);
        Assert.All(lines, line =>
        {
            Assert.Equal(Constants.T42_LINE_SIZE, line.Length);
            Assert.NotNull(line.Data);
        });
    }

    [Fact]
    public void Parse_WithMagazineFilter_FiltersCorrectly()
    {
        // Arrange
        var handler = new T42Handler();
        var stream = new MemoryStream(T42.Sample);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions
        {
            OutputFormat = Format.T42,
            Magazine = 1 // Filter to magazine 1
        };

        // Act
        var lines = handler.Parse(stream, options).ToList();

        // Assert
        Assert.All(lines, line => Assert.Equal(1, line.Magazine));
    }

    [Fact]
    public void Parse_T42ToVBI_DoesNotThrowException()
    {
        // Arrange
        var handler = new T42Handler();
        var stream = new MemoryStream(T42.Sample);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.VBI };

        // Act
        var lines = handler.Parse(stream, options).ToList();

        // Assert - conversion may filter out invalid lines, so just check no exception
        // If lines are returned, they should have correct format
        foreach (var line in lines)
        {
            Assert.Equal(Constants.VBI_LINE_SIZE, line.Length);
            Assert.Equal(0x31, line.SampleCoding);
        }
    }

    [Fact]
    public void Parse_T42ToVBIDouble_DoesNotThrowException()
    {
        // Arrange
        var handler = new T42Handler();
        var stream = new MemoryStream(T42.Sample);
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
    public async Task ParseAsync_WithSampleData_ReturnsLines()
    {
        // Arrange
        var handler = new T42Handler();
        var stream = new MemoryStream(T42.Sample);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };

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
            Assert.Equal(Constants.T42_LINE_SIZE, line.Length);
            Assert.NotNull(line.Data);
        });
    }

    [Fact]
    public async Task ParseAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var handler = new T42Handler();
        var stream = new MemoryStream(T42.Sample);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions { OutputFormat = Format.T42 };
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
        var handler = new T42Handler();
        var stream = new MemoryStream(T42.Sample);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions
        {
            OutputFormat = Format.T42,
            Rows = new[] { 0 } // Filter to row 0 only
        };

        // Act
        var lines = handler.Parse(stream, options).ToList();

        // Assert
        Assert.All(lines, line => Assert.Equal(0, line.Row));
    }

    [Fact]
    public void Parse_WithLineCount_IncrementsTimecodeCorrectly()
    {
        // Arrange
        var handler = new T42Handler();
        var stream = new MemoryStream(T42.Sample);
        _streamsToDispose.Add(stream);
        var options = new ParseOptions
        {
            OutputFormat = Format.T42,
            LineCount = 2 // Increment timecode every 2 lines
        };

        // Act
        var lines = handler.Parse(stream, options).ToList();

        // Assert
        Assert.NotEmpty(lines);
        if (lines.Count >= 2)
        {
            // First line should have timecode 00:00:00:00
            Assert.Equal(new Timecode(0), lines[0].LineTimecode);
            // Third line (index 2) should have incremented timecode
            if (lines.Count >= 3)
            {
                Assert.NotEqual(lines[0].LineTimecode, lines[2].LineTimecode);
            }
        }
    }
}
