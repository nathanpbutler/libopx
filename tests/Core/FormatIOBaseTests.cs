using System.Diagnostics.CodeAnalysis;
using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Core;

/// <summary>
/// Unit tests for the FormatIOBase abstract class.
/// Tests common I/O functionality shared across all format parsers.
/// </summary>
public class FormatIOBaseTests : IDisposable
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

        // Give the OS time to release file handles
        GC.Collect();
        GC.WaitForPendingFinalizers();

        foreach (var file in _tempFilesToDelete)
        {
            if (File.Exists(file))
            {
                try { File.Delete(file); } catch { /* Best effort cleanup */ }
            }
        }
    }

    /// <summary>
    /// Test implementation of FormatIOBase for testing purposes
    /// </summary>
    private class TestFormatParser : FormatIOBase
    {
        [SetsRequiredMembers]
        public TestFormatParser(string inputFile)
        {
            InputFile = new FileInfo(inputFile);
            if (!InputFile.Exists)
                throw new FileNotFoundException("File not found", inputFile);
            Input = InputFile.OpenRead();
        }

        [SetsRequiredMembers]
        public TestFormatParser()
        {
            InputFile = null;
            Input = Console.OpenStandardInput();
        }

        [SetsRequiredMembers]
        public TestFormatParser(Stream inputStream)
        {
            InputFile = null;
            Input = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
        }
    }

    [Fact]
    public void SetOutput_WithFilePath_SetsOutputFile()
    {
        // Arrange
        var testStream = new MemoryStream();
        _streamsToDispose.Add(testStream);
        var parser = new TestFormatParser(testStream);
        var outputPath = "output.txt";

        // Act
        parser.SetOutput(outputPath);

        // Assert
        Assert.NotNull(parser.OutputFile);
        Assert.Equal(outputPath, parser.OutputFile.Name);
    }

    [Fact]
    public void SetOutput_WithStream_SetsOutputStreamAndClearsOutputFile()
    {
        // Arrange
        var inputStream = new MemoryStream();
        var outputStream = new MemoryStream();
        _streamsToDispose.Add(inputStream);
        _streamsToDispose.Add(outputStream);
        var parser = new TestFormatParser(inputStream);

        // Act
        parser.SetOutput(outputStream);

        // Assert
        Assert.Null(parser.OutputFile);
        // Access Output property to ensure it uses the custom stream
        var actualOutput = parser.Output;
        Assert.Equal(outputStream, actualOutput);
    }

    [Fact]
    public void SetOutput_WithNullStream_ThrowsArgumentNullException()
    {
        // Arrange
        var testStream = new MemoryStream();
        _streamsToDispose.Add(testStream);
        var parser = new TestFormatParser(testStream);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => parser.SetOutput((Stream)null!));
    }

    [Fact]
    public void Output_WhenOutputFileIsNull_ReturnsStdout()
    {
        // Arrange
        var testStream = new MemoryStream();
        _streamsToDispose.Add(testStream);
        var parser = new TestFormatParser(testStream);

        // Act
        var output = parser.Output;

        // Assert
        Assert.NotNull(output);
        // stdout should be accessible (can't check exact equality, but should not be null)
        Assert.True(output.CanWrite);
    }

    [Fact]
    public void Output_WhenOutputFileIsSet_CreatesFileStream()
    {
        // Arrange
        var inputStream = new MemoryStream();
        _streamsToDispose.Add(inputStream);
        var parser = new TestFormatParser(inputStream);
        var tempFile = Path.GetTempFileName();
        _tempFilesToDelete.Add(tempFile);
        parser.SetOutput(tempFile);

        // Act
        var output = parser.Output;

        // Assert
        Assert.NotNull(output);
        Assert.True(output.CanWrite);
    }

    [Fact]
    public void Output_LazyInitialization_ReturnsSameInstance()
    {
        // Arrange
        var inputStream = new MemoryStream();
        _streamsToDispose.Add(inputStream);
        var parser = new TestFormatParser(inputStream);
        var tempFile = Path.GetTempFileName();
        _tempFilesToDelete.Add(tempFile);
        parser.SetOutput(tempFile);

        // Act
        var output1 = parser.Output;
        var output2 = parser.Output;

        // Assert
        Assert.Same(output1, output2);
    }

    [Fact]
    public void OutputFormat_DefaultsToNull()
    {
        // Arrange
        var testStream = new MemoryStream();
        _streamsToDispose.Add(testStream);
        var parser = new TestFormatParser(testStream);

        // Assert
        Assert.Null(parser.OutputFormat);
    }

    [Fact]
    public void OutputFormat_CanBeSet()
    {
        // Arrange
        var testStream = new MemoryStream();
        _streamsToDispose.Add(testStream);
        var parser = new TestFormatParser(testStream);

        // Act
        parser.OutputFormat = Format.T42;

        // Assert
        Assert.Equal(Format.T42, parser.OutputFormat);
    }

    [Fact]
    public void Function_DefaultsToFilter()
    {
        // Arrange
        var testStream = new MemoryStream();
        _streamsToDispose.Add(testStream);
        var parser = new TestFormatParser(testStream);

        // Assert
        Assert.Equal(Function.Filter, parser.Function);
    }

    [Fact]
    public void Function_CanBeSet()
    {
        // Arrange
        var testStream = new MemoryStream();
        _streamsToDispose.Add(testStream);
        var parser = new TestFormatParser(testStream);

        // Act
        parser.Function = Function.Extract;

        // Assert
        Assert.Equal(Function.Extract, parser.Function);
    }

    [Fact]
    public void Dispose_WithInputFile_DisposesInputStream()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        _tempFilesToDelete.Add(tempFile);
        File.WriteAllText(tempFile, "test content");
        var parser = new TestFormatParser(tempFile);
        var inputStream = parser.Input;

        // Act
        parser.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => inputStream.ReadByte());
    }

    [Fact]
    public void Dispose_WithCustomInputStream_DoesNotDisposeStream()
    {
        // Arrange
        var customStream = new MemoryStream();
        _streamsToDispose.Add(customStream);
        var parser = new TestFormatParser(customStream);

        // Act
        parser.Dispose();

        // Assert - Stream should still be usable (not disposed)
        Assert.True(customStream.CanRead);
    }

    [Fact]
    public void Dispose_WithOutputFile_DisposesOutputStream()
    {
        // Arrange
        var inputStream = new MemoryStream();
        _streamsToDispose.Add(inputStream);
        var parser = new TestFormatParser(inputStream);
        var tempFile = Path.GetTempFileName();
        _tempFilesToDelete.Add(tempFile);
        parser.SetOutput(tempFile);
        var outputStream = parser.Output; // Initialize the output stream

        // Act
        parser.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => outputStream.WriteByte(0));
    }

    [Fact]
    public void Dispose_WithCustomOutputStream_DoesNotDisposeStream()
    {
        // Arrange
        var inputStream = new MemoryStream();
        var outputStream = new MemoryStream();
        _streamsToDispose.Add(inputStream);
        _streamsToDispose.Add(outputStream);
        var parser = new TestFormatParser(inputStream);
        parser.SetOutput(outputStream);

        // Act
        parser.Dispose();

        // Assert - Stream should still be usable (not disposed)
        Assert.True(outputStream.CanWrite);
    }

    [Fact]
    public void Constructor_WithValidFile_SetsInputFile()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        _tempFilesToDelete.Add(tempFile);
        File.WriteAllText(tempFile, "test");

        // Act
        var parser = new TestFormatParser(tempFile);

        // Assert
        Assert.NotNull(parser.InputFile);
        Assert.Equal(tempFile, parser.InputFile.FullName);
        Assert.NotNull(parser.Input);
    }

    [Fact]
    public void Constructor_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = "non_existent_file_12345.bin";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => new TestFormatParser(nonExistentFile));
    }

    [Fact]
    public void Constructor_WithStream_SetsInput()
    {
        // Arrange
        var stream = new MemoryStream();
        _streamsToDispose.Add(stream);

        // Act
        var parser = new TestFormatParser(stream);

        // Assert
        Assert.Null(parser.InputFile);
        Assert.Same(stream, parser.Input);
    }

    [Fact]
    public void Constructor_WithNullStream_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TestFormatParser((Stream)null!));
    }
}
