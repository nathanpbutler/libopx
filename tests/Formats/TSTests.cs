using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Formats;

/// <summary>
/// Unit tests for the TS (MPEG Transport Stream) class.
/// Tests class structure, constructors, and properties.
/// </summary>
[Collection("Sample Files Sequential")]
public class TSTests : IAsyncLifetime, IDisposable
{
    private readonly List<Stream> _streamsToDispose = [];
    private string? _sampleFilePath;

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
    }

    [Fact]
    public void Constructor_WithValidFile_InitializesCorrectly()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePath == null)
        {
            return;
        }

        // Act
        using var ts = new TS(_sampleFilePath);

        // Assert
        Assert.NotNull(ts.InputFile);
        Assert.Equal(Path.GetFullPath(_sampleFilePath), ts.InputFile.FullName);
        Assert.NotNull(ts.Input);
        Assert.Equal(Format.T42, ts.OutputFormat); // Default output format
    }

    [Fact]
    public void Constructor_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = "non_existent_ts_file.ts";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => new TS(nonExistentFile));
    }

    [Fact]
    public void Constructor_WithStream_InitializesCorrectly()
    {
        // Arrange
        var stream = new MemoryStream(new byte[1880]);
        _streamsToDispose.Add(stream);

        // Act
        using var ts = new TS(stream);

        // Assert
        Assert.Null(ts.InputFile);
        Assert.Same(stream, ts.Input);
        Assert.Equal(Format.T42, ts.OutputFormat);
    }

    [Fact]
    public void Constructor_WithNullStream_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TS((Stream)null!));
    }

    [Fact]
    public void Constructor_Stdin_InitializesCorrectly()
    {
        // Act
        using var ts = new TS();

        // Assert
        Assert.Null(ts.InputFile);
        Assert.NotNull(ts.Input);
        Assert.Equal(Format.T42, ts.OutputFormat);
    }

    [Fact]
    public void InheritsFromFormatIOBase_VerifyBaseClassProperties()
    {
        // Arrange
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);

        // Act
        using var ts = new TS(stream);

        // Assert - Verify FormatIOBase properties are accessible
        Assert.NotNull(ts.Input);
        Assert.Equal(Function.Filter, ts.Function); // Default from base class
        ts.Function = Function.Extract;
        Assert.Equal(Function.Extract, ts.Function);
    }

    [Fact]
    public void SetOutput_InheritsFromBase_WorksCorrectly()
    {
        // Arrange
        var inputStream = new MemoryStream();
        var outputStream = new MemoryStream();
        _streamsToDispose.Add(inputStream);
        _streamsToDispose.Add(outputStream);
        using var ts = new TS(inputStream);

        // Act
        ts.SetOutput(outputStream);

        // Assert
        Assert.Null(ts.OutputFile);
        Assert.Equal(outputStream, ts.Output);
    }

    [Fact]
    public void OutputFormat_CanBeChanged()
    {
        // Arrange
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);
        using var ts = new TS(stream);

        // Act
        ts.OutputFormat = Format.VBI;

        // Assert
        Assert.Equal(Format.VBI, ts.OutputFormat);
    }

    [Fact]
    public void Dispose_ProperlyDisposesResources()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePath == null)
        {
            return;
        }

        // Arrange
        var ts = new TS(_sampleFilePath);
        var inputStream = ts.Input;

        // Act
        ts.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => inputStream.ReadByte());
    }

    [Fact]
    public void Dispose_WithCustomStream_DoesNotDisposeStream()
    {
        // Arrange
        var customStream = new MemoryStream();
        _streamsToDispose.Add(customStream);
        var ts = new TS(customStream);

        // Act
        ts.Dispose();

        // Assert - Stream should still be usable
        Assert.True(customStream.CanRead);
    }

    [Fact]
    public void FrameRate_Property_CanBeSet()
    {
        // Arrange
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);
        using var ts = new TS(stream);

        // Act
        ts.FrameRate = 30; // Set custom frame rate

        // Assert
        Assert.Equal(30, ts.FrameRate);
    }

    // Integration Tests with Real Data

    [Fact]
    public void Parse_WithInputTS_ReturnsExpectedPackets()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePath == null)
        {
            return;
        }

        // Act
        using var ts = new TS(_sampleFilePath);
        var packets = ts.Parse(magazine: null, rows: null).ToList();

        // Assert
        Assert.NotEmpty(packets);
        Assert.All(packets, packet =>
        {
            Assert.NotNull(packet.Lines);
            Assert.True(packet.Lines.Count > 0);
        });
    }

    [Fact]
    public async Task ParseAsync_WithInputTS_ReturnsExpectedPackets()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePath == null)
        {
            return;
        }

        // Act
        using var ts = new TS(_sampleFilePath);
        var packets = new List<Packet>();
        await foreach (var packet in ts.ParseAsync(magazine: null, rows: null))
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
}
