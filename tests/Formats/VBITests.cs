using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Formats;

/// <summary>
/// Unit tests for the VBI (Vertical Blanking Interval) class.
/// Tests class structure, constructors, and properties.
/// </summary>
public class VBITests : IDisposable
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
        File.WriteAllBytes(tempFile, new byte[1440]); // VBI line size * 10 lines

        // Act
        using var vbi = new VBI(tempFile);

        // Assert
        Assert.NotNull(vbi.InputFile);
        Assert.Equal(tempFile, vbi.InputFile.FullName);
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
        // Arrange
        var tempFile = Path.GetTempFileName();
        _tempFilesToDelete.Add(tempFile);
        File.WriteAllBytes(tempFile, new byte[1440]);
        var vbi = new VBI(tempFile);
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

}
