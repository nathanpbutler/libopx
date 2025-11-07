using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Formats;

/// <summary>
/// Unit tests for the TS (MPEG Transport Stream) class.
/// Tests class structure, constructors, and properties.
/// </summary>
public class TSTests : IDisposable
{
    private readonly List<Stream> _streamsToDispose = [];
    private readonly List<string> _tempFilesToDelete = [];

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
                File.Delete(file);
            }
        }
    }

    [Fact]
    public void Constructor_WithValidFile_InitializesCorrectly()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        _tempFilesToDelete.Add(tempFile);
        File.WriteAllBytes(tempFile, new byte[1880]); // TS packet size * 10 packets

        // Act
        using var ts = new TS(tempFile);

        // Assert
        Assert.NotNull(ts.InputFile);
        Assert.Equal(tempFile, ts.InputFile.FullName);
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
        // Arrange
        var tempFile = Path.GetTempFileName();
        _tempFilesToDelete.Add(tempFile);
        File.WriteAllBytes(tempFile, new byte[1880]);
        var ts = new TS(tempFile);
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
}
