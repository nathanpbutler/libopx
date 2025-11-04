using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Formats;

/// <summary>
/// Unit tests for the ANC (Ancillary Data) class.
/// Tests parsing of extracted MXF ancillary data streams.
/// </summary>
public class ANCTests : IDisposable
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
        File.WriteAllBytes(tempFile, new byte[100]); // Create a dummy file

        // Act
        using var anc = new ANC(tempFile);

        // Assert
        Assert.NotNull(anc.InputFile);
        Assert.Equal(tempFile, anc.InputFile.FullName);
        Assert.NotNull(anc.Input);
        Assert.Equal(Format.T42, anc.OutputFormat); // Default output format
    }

    [Fact]
    public void Constructor_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = "non_existent_anc_file.bin";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => new ANC(nonExistentFile));
    }

    [Fact]
    public void Constructor_WithStream_InitializesCorrectly()
    {
        // Arrange
        var stream = new MemoryStream(new byte[100]);
        _streamsToDispose.Add(stream);

        // Act
        using var anc = new ANC(stream);

        // Assert
        Assert.Null(anc.InputFile);
        Assert.Same(stream, anc.Input);
        Assert.Equal(Format.T42, anc.OutputFormat);
    }

    [Fact]
    public void Constructor_WithNullStream_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ANC((Stream)null!));
    }

    [Fact]
    public void Constructor_Stdin_InitializesCorrectly()
    {
        // Act
        using var anc = new ANC();

        // Assert
        Assert.Null(anc.InputFile);
        Assert.NotNull(anc.Input);
        Assert.Equal(Format.T42, anc.OutputFormat);
    }

    [Fact]
    public void InheritsFromFormatIOBase_VerifyBaseClassProperties()
    {
        // Arrange
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);

        // Act
        using var anc = new ANC(stream);

        // Assert - Verify FormatIOBase properties are accessible
        Assert.NotNull(anc.Input);
        Assert.Equal(Function.Filter, anc.Function); // Default from base class
        anc.Function = Function.Extract;
        Assert.Equal(Function.Extract, anc.Function);
    }

    [Fact]
    public void SetOutput_InheritsFromBase_WorksCorrectly()
    {
        // Arrange
        var inputStream = new MemoryStream();
        var outputStream = new MemoryStream();
        _streamsToDispose.Add(inputStream);
        _streamsToDispose.Add(outputStream);
        using var anc = new ANC(inputStream);

        // Act
        anc.SetOutput(outputStream);

        // Assert
        Assert.Null(anc.OutputFile);
        Assert.Equal(outputStream, anc.Output);
    }

    [Fact]
    public void OutputFormat_CanBeChanged()
    {
        // Arrange
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);
        using var anc = new ANC(stream);

        // Act
        anc.OutputFormat = Format.VBI;

        // Assert
        Assert.Equal(Format.VBI, anc.OutputFormat);
    }

    [Fact]
    public void Dispose_ProperlyDisposesResources()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        _tempFilesToDelete.Add(tempFile);
        File.WriteAllBytes(tempFile, new byte[100]);
        var anc = new ANC(tempFile);
        var inputStream = anc.Input;

        // Act
        anc.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => inputStream.ReadByte());
    }

    [Fact]
    public void Dispose_WithCustomStream_DoesNotDisposeStream()
    {
        // Arrange
        var customStream = new MemoryStream();
        _streamsToDispose.Add(customStream);
        var anc = new ANC(customStream);

        // Act
        anc.Dispose();

        // Assert - Stream should still be usable
        Assert.True(customStream.CanRead);
    }

    [Fact]
    public void ObsoletePacketsProperty_ExistsForBackwardCompatibility()
    {
        // Arrange
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);
        using var anc = new ANC(stream);

        // Act & Assert - Property should exist but be marked obsolete
#pragma warning disable CS0618 // Type or member is obsolete
        var packets = anc.Packets;
        Assert.NotNull(packets);
        Assert.Empty(packets);
#pragma warning restore CS0618
    }

    // Note: Full integration tests for Parse() and ParseAsync() with real MXF data
    // are covered in MemoryBenchmarkTests.cs. These are unit tests for the class structure.
}
