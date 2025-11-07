using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Formats;

/// <summary>
/// Unit tests for the ANC (Ancillary Data) class.
/// Tests parsing of extracted MXF ancillary data streams.
/// </summary>
[Collection("Sample Files Sequential")]
public class ANCTests : IAsyncLifetime, IDisposable
{
    private readonly List<Stream> _streamsToDispose = [];
    private string? _sampleFilePath;

    public async Task InitializeAsync()
    {
        // Load input.bin sample file once for all tests
        var filePath = "input.bin";
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
        using var anc = new ANC(_sampleFilePath);

        // Assert
        Assert.NotNull(anc.InputFile);
        Assert.Equal(Path.GetFullPath(_sampleFilePath), anc.InputFile.FullName);
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
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePath == null)
        {
            return;
        }

        // Arrange
        var anc = new ANC(_sampleFilePath);
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

    // Integration Tests with Real Data

    [Fact]
    public void Parse_WithInputBin_ReturnsExpectedPackets()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePath == null)
        {
            return;
        }

        // Act
        using var anc = new ANC(_sampleFilePath);
        var packets = anc.Parse(magazine: null, rows: null).ToList();

        // Assert
        Assert.NotEmpty(packets);
        Assert.All(packets, packet =>
        {
            Assert.NotNull(packet.Lines);
            Assert.True(packet.Lines.Count > 0);
        });
    }

    [Fact]
    public async Task ParseAsync_WithInputBin_ReturnsExpectedPackets()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePath == null)
        {
            return;
        }

        // Act
        using var anc = new ANC(_sampleFilePath);
        var packets = new List<Packet>();
        await foreach (var packet in anc.ParseAsync(magazine: null, rows: null))
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

    [Fact]
    public void Parse_WithInputBin_ValidatesExpectedPacketCount()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePath == null)
        {
            return;
        }

        // Act
        using var anc = new ANC(_sampleFilePath);
        var packets = anc.Parse(magazine: null, rows: null).ToList();

        // Assert
        // input.bin should contain 50 packets (1 packet per frame × 50 frames @ 25fps)
        Assert.Equal(50, packets.Count);
    }

    [Fact]
    public void Parse_WithInputBin_MagazineFilter_FiltersCorrectly()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePath == null)
        {
            return;
        }

        // Act
        using var anc = new ANC(_sampleFilePath);
        var packets = anc.Parse(magazine: 8, rows: null).ToList();

        // Assert
        Assert.All(packets, packet => Assert.Equal(8, packet.Magazine));
    }

    [Fact]
    public async Task ParseAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Skip test if sample file couldn't be downloaded
        if (_sampleFilePath == null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            using var anc = new ANC(_sampleFilePath);
            await foreach (var packet in anc.ParseAsync(magazine: null, rows: null, cancellationToken: cts.Token))
            {
                // Should not reach here
            }
        });
    }
}
