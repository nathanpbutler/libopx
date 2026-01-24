using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Formats;

/// <summary>
/// Unit tests for the T42 class.
/// Tests class structure, constructors, and properties.
/// </summary>
[Collection("Sample Files Sequential")]
public class T42Tests : IAsyncLifetime, IDisposable
{
    private readonly List<Stream> _streamsToDispose = [];
    private string? _sampleFilePath;

    public async Task InitializeAsync()
    {
        // Load input.t42 sample file once for all tests
        var filePath = "input.t42";
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
        using var t42 = new T42(_sampleFilePath);

        // Assert
        Assert.NotNull(t42.InputFile);
        Assert.Equal(Path.GetFullPath(_sampleFilePath), t42.InputFile.FullName);
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
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePath == null)
        {
            return;
        }

        // Arrange
        var t42 = new T42(_sampleFilePath);
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

    // Integration Tests with Real Data

    [Fact]
    public void Parse_WithInputT42_ReturnsExpectedLines()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePath == null)
        {
            return;
        }

        // Act
        using var t42 = new T42(_sampleFilePath);
        var lines = t42.Parse(magazine: null, rows: null).ToList();

        // Assert
        Assert.NotEmpty(lines);
        Assert.All(lines, line =>
        {
            Assert.Equal(Constants.T42_LINE_SIZE, line.Length);
            Assert.NotNull(line.Data);
        });
    }

    [Fact]
    public async Task ParseAsync_WithInputT42_ReturnsExpectedLines()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePath == null)
        {
            return;
        }

        // Act
        using var t42 = new T42(_sampleFilePath);
        var lines = new List<Line>();
        await foreach (var line in t42.ParseAsync(magazine: null, rows: null))
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

    [Theory]
    [InlineData(new byte[] { 0x15, 0x2A, 0x48, 0x65, 0x6C, 0x6C, 0x6F }, true)]   // "Hello"
    [InlineData(new byte[] { 0x15, 0x2A, 0x41, 0x42, 0x43 }, true)]               // "ABC"
    [InlineData(new byte[] { 0x15, 0x2A, 0x20, 0x20, 0x20, 0x20 }, false)]        // Only spaces
    [InlineData(new byte[] { 0x15, 0x2A, 0x00, 0x01, 0x07, 0x0D }, false)]        // Only control codes
    [InlineData(new byte[] { 0x15, 0x2A, 0x20, 0x00, 0x20, 0x07 }, false)]        // Spaces + control
    [InlineData(new byte[] { 0x15, 0x2A, 0x20, 0x20, 0x21 }, true)]               // Space + '!'
    [InlineData(new byte[] { 0x15, 0x2A, 0x1F, 0x1F, 0x1F }, false)]              // Control codes (0x1F)
    [InlineData(new byte[] { 0x15, 0x2A, 0x7E, 0x7F }, true)]                     // High characters
    public void HasMeaningfulContent_VariousInputs_ReturnsExpected(byte[] data, bool expected)
    {
        // Arrange - Ensure data is at least T42_LINE_SIZE
        var paddedData = new byte[Constants.T42_LINE_SIZE];
        Array.Copy(data, 0, paddedData, 0, Math.Min(data.Length, Constants.T42_LINE_SIZE));

        // Act
        var result = T42.HasMeaningfulContent(paddedData);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void HasMeaningfulContent_NullData_ReturnsFalse()
    {
        // Act
        var result = T42.HasMeaningfulContent(null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasMeaningfulContent_EmptyData_ReturnsFalse()
    {
        // Arrange
        var data = new byte[0];

        // Act
        var result = T42.HasMeaningfulContent(data);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasMeaningfulContent_TooShortData_ReturnsFalse()
    {
        // Arrange
        var data = new byte[1];

        // Act
        var result = T42.HasMeaningfulContent(data);

        // Assert
        Assert.False(result);
    }
}
