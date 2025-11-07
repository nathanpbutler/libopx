using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Formats;

/// <summary>
/// Unit tests for the T42 class.
/// Tests class structure, constructors, and properties.
/// </summary>
public class T42Tests : IDisposable
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
        File.WriteAllBytes(tempFile, new byte[420]); // T42 line size * 10 lines

        // Act
        using var t42 = new T42(tempFile);

        // Assert
        Assert.NotNull(t42.InputFile);
        Assert.Equal(tempFile, t42.InputFile.FullName);
        Assert.NotNull(t42.Input);
        Assert.Equal(Format.T42, t42.OutputFormat); // Default output format
    }

    [Fact]
    public void Constructor_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = "non_existent_t42_file.t42";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => new T42(nonExistentFile));
    }

    [Fact]
    public void Constructor_WithStream_InitializesCorrectly()
    {
        // Arrange
        var stream = new MemoryStream(new byte[420]);
        _streamsToDispose.Add(stream);

        // Act
        using var t42 = new T42(stream);

        // Assert
        Assert.Null(t42.InputFile);
        Assert.Same(stream, t42.Input);
        Assert.Equal(Format.T42, t42.OutputFormat);
    }

    [Fact]
    public void Constructor_WithNullStream_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new T42((Stream)null!));
    }

    [Fact]
    public void Constructor_Stdin_InitializesCorrectly()
    {
        // Act
        using var t42 = new T42();

        // Assert
        Assert.Null(t42.InputFile);
        Assert.NotNull(t42.Input);
        Assert.Equal(Format.T42, t42.OutputFormat);
    }

    [Fact]
    public void InheritsFromFormatIOBase_VerifyBaseClassProperties()
    {
        // Arrange
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);

        // Act
        using var t42 = new T42(stream);

        // Assert - Verify FormatIOBase properties are accessible
        Assert.NotNull(t42.Input);
        Assert.Equal(Function.Filter, t42.Function); // Default from base class
        t42.Function = Function.Extract;
        Assert.Equal(Function.Extract, t42.Function);
    }

    [Fact]
    public void SetOutput_InheritsFromBase_WorksCorrectly()
    {
        // Arrange
        var inputStream = new MemoryStream();
        var outputStream = new MemoryStream();
        _streamsToDispose.Add(inputStream);
        _streamsToDispose.Add(outputStream);
        using var t42 = new T42(inputStream);

        // Act
        t42.SetOutput(outputStream);

        // Assert
        Assert.Null(t42.OutputFile);
        Assert.Equal(outputStream, t42.Output);
    }

    [Fact]
    public void OutputFormat_CanBeChanged()
    {
        // Arrange
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);
        using var t42 = new T42(stream);

        // Act
        t42.OutputFormat = Format.VBI;

        // Assert
        Assert.Equal(Format.VBI, t42.OutputFormat);
    }

    [Fact]
    public void Dispose_ProperlyDisposesResources()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        _tempFilesToDelete.Add(tempFile);
        File.WriteAllBytes(tempFile, new byte[420]);
        var t42 = new T42(tempFile);
        var inputStream = t42.Input;

        // Act
        t42.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => inputStream.ReadByte());
    }

    [Fact]
    public void Dispose_WithCustomStream_DoesNotDisposeStream()
    {
        // Arrange
        var customStream = new MemoryStream();
        _streamsToDispose.Add(customStream);
        var t42 = new T42(customStream);

        // Act
        t42.Dispose();

        // Assert - Stream should still be usable
        Assert.True(customStream.CanRead);
    }

    [Fact]
    public void LineCount_Property_CanBeSet()
    {
        // Arrange
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);
        using var t42 = new T42(stream);

        // Act
        t42.LineCount = 2; // Increment timecode every 2 lines

        // Assert
        Assert.Equal(2, t42.LineCount);
    }
}
