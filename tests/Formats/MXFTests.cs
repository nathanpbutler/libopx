using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Formats;

/// <summary>
/// Unit tests for the MXF (Material Exchange Format) class.
/// Tests class structure, constructors, and properties.
/// </summary>
[Collection("Sample Files Sequential")]
public class MXFTests : IAsyncLifetime, IDisposable
{
    private readonly List<Stream> _streamsToDispose = [];
    private readonly List<string> _tempFilesToDelete = [];
    private string? _sampleFilePath;

    public async Task InitializeAsync()
    {
        // Load input.mxf sample file once for all tests
        var filePath = "input.mxf";
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

        foreach (var file in _tempFilesToDelete)
        {
            if (File.Exists(file))
            {
                try { File.Delete(file); } catch { /* Best effort cleanup */ }
            }
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
        using var mxf = new MXF(_sampleFilePath);

        // Assert
        Assert.NotNull(mxf.InputFile);
        Assert.Equal(Path.GetFullPath(_sampleFilePath), mxf.InputFile.FullName);
        Assert.NotNull(mxf.Input);
        Assert.Equal(Format.T42, mxf.OutputFormat); // Default output format
    }

    [Fact]
    public void Constructor_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = "non_existent_mxf_file.mxf";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => new MXF(nonExistentFile));
    }

    [Fact]
    public void InheritsFromFormatIOBase_VerifyBaseClassProperties()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePath == null)
        {
            return;
        }

        // Act
        using var mxf = new MXF(_sampleFilePath);

        // Assert - Verify FormatIOBase properties are accessible
        Assert.NotNull(mxf.Input);
        Assert.Equal(Function.Filter, mxf.Function); // Default from base class
        mxf.Function = Function.Extract;
        Assert.Equal(Function.Extract, mxf.Function);
    }

    [Fact]
    public void SetOutput_InheritsFromBase_WorksCorrectly()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePath == null)
        {
            return;
        }

        // Arrange
        var outputStream = new MemoryStream();
        _streamsToDispose.Add(outputStream);
        using var mxf = new MXF(_sampleFilePath);

        // Act
        mxf.SetOutput(outputStream);

        // Assert
        Assert.Null(mxf.OutputFile);
        Assert.Equal(outputStream, mxf.Output);
    }

    [Fact]
    public void OutputFormat_CanBeChanged()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePath == null)
        {
            return;
        }

        // Arrange
        using var mxf = new MXF(_sampleFilePath);

        // Act
        mxf.OutputFormat = Format.VBI;

        // Assert
        Assert.Equal(Format.VBI, mxf.OutputFormat);
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
        var mxf = new MXF(_sampleFilePath);
        var inputStream = mxf.Input;

        // Act
        mxf.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => inputStream.ReadByte());
    }

    [Fact]
    public void AddRequiredKey_CanAddKeys()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePath == null)
        {
            return;
        }

        // Arrange
        using var mxf = new MXF(_sampleFilePath);

        // Act
        mxf.AddRequiredKey(KeyType.Data);
        mxf.AddRequiredKey(KeyType.System);

        // Assert - No exception should be thrown
        Assert.NotNull(mxf);
    }

}
