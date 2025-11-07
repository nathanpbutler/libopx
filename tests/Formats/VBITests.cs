using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Formats;

/// <summary>
/// Unit tests for the VBI (Vertical Blanking Interval) class.
/// Tests class structure, constructors, and properties.
/// </summary>
[Collection("Sample Files Sequential")]
public class VBITests : IAsyncLifetime, IDisposable
{
    private readonly List<Stream> _streamsToDispose = [];
    private string? _sampleFilePathVbi;
    private string? _sampleFilePathVbid;

    public async Task InitializeAsync()
    {
        // Load input.vbi sample file once for all tests
        var filePathVbi = "input.vbi";
        if (!File.Exists(filePathVbi))
        {
            await SampleFiles.EnsureAsync(filePathVbi);
        }
        if (File.Exists(filePathVbi))
        {
            _sampleFilePathVbi = filePathVbi;
        }

        // Load input.vbid sample file once for all tests
        var filePathVbid = "input.vbid";
        if (!File.Exists(filePathVbid))
        {
            await SampleFiles.EnsureAsync(filePathVbid);
        }
        if (File.Exists(filePathVbid))
        {
            _sampleFilePathVbid = filePathVbid;
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
        if (_sampleFilePathVbi == null)
        {
            return;
        }

        // Act
        using var vbi = new VBI(_sampleFilePathVbi);

        // Assert
        Assert.NotNull(vbi.InputFile);
        Assert.Equal(Path.GetFullPath(_sampleFilePathVbi), vbi.InputFile.FullName);
        Assert.NotNull(vbi.Input);
        Assert.Equal(Format.T42, vbi.OutputFormat); // Default output format
    }

    [Fact]
    public void Constructor_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = "non_existent_vbi_file.vbi";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => new VBI(nonExistentFile));
    }

    [Fact]
    public void Constructor_WithStream_InitializesCorrectly()
    {
        // Arrange
        var stream = new MemoryStream(new byte[1440]);
        _streamsToDispose.Add(stream);

        // Act
        using var vbi = new VBI(stream);

        // Assert
        Assert.Null(vbi.InputFile);
        Assert.Same(stream, vbi.Input);
        Assert.Equal(Format.T42, vbi.OutputFormat);
    }

    [Fact]
    public void Constructor_WithNullStream_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new VBI((Stream)null!));
    }

    [Fact]
    public void Constructor_Stdin_InitializesCorrectly()
    {
        // Act
        using var vbi = new VBI();

        // Assert
        Assert.Null(vbi.InputFile);
        Assert.NotNull(vbi.Input);
        Assert.Equal(Format.T42, vbi.OutputFormat);
    }

    [Fact]
    public void InheritsFromFormatIOBase_VerifyBaseClassProperties()
    {
        // Arrange
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);

        // Act
        using var vbi = new VBI(stream);

        // Assert - Verify FormatIOBase properties are accessible
        Assert.NotNull(vbi.Input);
        Assert.Equal(Function.Filter, vbi.Function); // Default from base class
        vbi.Function = Function.Extract;
        Assert.Equal(Function.Extract, vbi.Function);
    }

    [Fact]
    public void SetOutput_InheritsFromBase_WorksCorrectly()
    {
        // Arrange
        var inputStream = new MemoryStream();
        var outputStream = new MemoryStream();
        _streamsToDispose.Add(inputStream);
        _streamsToDispose.Add(outputStream);
        using var vbi = new VBI(inputStream);

        // Act
        vbi.SetOutput(outputStream);

        // Assert
        Assert.Null(vbi.OutputFile);
        Assert.Equal(outputStream, vbi.Output);
    }

    [Fact]
    public void OutputFormat_CanBeChanged()
    {
        // Arrange
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);
        using var vbi = new VBI(stream);

        // Act
        vbi.OutputFormat = Format.VBI;

        // Assert
        Assert.Equal(Format.VBI, vbi.OutputFormat);
    }

    [Fact]
    public void Dispose_ProperlyDisposesResources()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePathVbi == null)
        {
            return;
        }

        // Arrange
        var vbi = new VBI(_sampleFilePathVbi, Format.VBI);
        var inputStream = vbi.Input;

        // Act
        vbi.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => inputStream.ReadByte());
    }

    [Fact]
    public void Dispose_WithCustomStream_DoesNotDisposeStream()
    {
        // Arrange
        var customStream = new MemoryStream();
        _streamsToDispose.Add(customStream);
        var vbi = new VBI(customStream);

        // Act
        vbi.Dispose();

        // Assert - Stream should still be usable
        Assert.True(customStream.CanRead);
    }

    // Integration Tests with Real Data

    [Fact]
    public void Parse_WithInputVBI_ReturnsExpectedLines()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePathVbi == null)
        {
            return;
        }

        // Act
        using var vbi = new VBI(_sampleFilePathVbi, Format.VBI);
        var lines = vbi.Parse(magazine: null, rows: null).ToList();

        // Assert
        Assert.NotEmpty(lines);
        Assert.All(lines, line =>
        {
            Assert.Equal(Constants.T42_LINE_SIZE, line.Length);
            Assert.NotNull(line.Data);
        });
    }

    [Fact]
    public async Task ParseAsync_WithInputVBI_ReturnsExpectedLines()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePathVbi == null)
        {
            return;
        }

        // Act
        using var vbi = new VBI(_sampleFilePathVbi, Format.VBI);
        var lines = new List<Line>();
        await foreach (var line in vbi.ParseAsync(magazine: null, rows: null))
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
    public void Parse_WithInputVBID_ReturnsExpectedLines()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePathVbid == null)
        {
            return;
        }

        // Act
        using var vbi = new VBI(_sampleFilePathVbid, Format.VBI_DOUBLE);
        var lines = vbi.Parse(magazine: null, rows: null).ToList();

        // Assert
        Assert.NotEmpty(lines);
        Assert.All(lines, line =>
        {
            Assert.Equal(Constants.T42_LINE_SIZE, line.Length);
            Assert.NotNull(line.Data);
        });
    }

    [Fact]
    public async Task ParseAsync_WithInputVBID_ReturnsExpectedLines()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePathVbid == null)
        {
            return;
        }

        // Act
        using var vbi = new VBI(_sampleFilePathVbid, Format.VBI_DOUBLE);
        var lines = new List<Line>();
        await foreach (var line in vbi.ParseAsync(magazine: null, rows: null))
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

}
