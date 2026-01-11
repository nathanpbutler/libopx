using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Exporters;

namespace nathanbutlerDEV.libopx;

/// <summary>
/// Fluent API for teletext format parsing and conversion.
/// Provides chainable methods for opening, configuring, converting, and saving teletext data.
/// </summary>
/// <remarks>
/// FormatIO wraps the existing handler infrastructure to provide a cleaner, more intuitive API.
/// It supports both line-based formats (VBI, T42) and packet-based formats (ANC, TS, MXF).
/// All operations are chainable for a fluent programming style.
/// </remarks>
public sealed class FormatIO : IDisposable
{
    #region Private Fields

    private readonly Stream _inputStream;
    private readonly Format _inputFormat;
    private readonly bool _ownsStream;
    private ParseOptions _options;
    private Format? _convertToFormat;
    private Stream? _outputStream;
    private bool _ownsOutputStream;
    private FileInfo? _outputFile;
    private bool _disposed;

    #endregion

    #region Constructor (Private)

    /// <summary>
    /// Private constructor - use factory methods (Open, OpenStdin) to create instances.
    /// </summary>
    private FormatIO(Stream inputStream, Format inputFormat, bool ownsStream)
    {
        _inputStream = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
        _inputFormat = inputFormat;
        _ownsStream = ownsStream;
        _options = new ParseOptions
        {
            OutputFormat = inputFormat // Default: no conversion
        };
    }

    #endregion

    #region Factory Methods

    /// <summary>
    /// Opens a file for parsing with automatic format detection from extension.
    /// </summary>
    /// <param name="filePath">Path to the input file</param>
    /// <returns>FormatIO instance for fluent configuration</returns>
    /// <exception cref="ArgumentException">If file path is null or empty</exception>
    /// <exception cref="FileNotFoundException">If file does not exist</exception>
    /// <exception cref="FormatDetectionException">If format cannot be detected from extension</exception>
    public static FormatIO Open(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath), "File path cannot be null or empty");

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException($"File not found: {filePath}");

        var format = DetectFormatFromExtension(fileInfo.Extension);
        if (format == Format.Unknown)
            throw new FormatDetectionException(
                $"Unable to detect format from extension: {fileInfo.Extension}. " +
                $"Supported extensions: .bin, .vbi, .vbid, .t42, .mxf, .ts, .rcwt, .stl");

        var stream = fileInfo.OpenRead();
        return new FormatIO(stream, format, ownsStream: true);
    }

    /// <summary>
    /// Opens stdin for parsing with explicit format specification.
    /// </summary>
    /// <param name="format">The format of the stdin data</param>
    /// <returns>FormatIO instance for fluent configuration</returns>
    /// <exception cref="ArgumentException">If format is Unknown</exception>
    public static FormatIO OpenStdin(Format format)
    {
        if (format == Format.Unknown)
            throw new ArgumentException("Format must be specified for stdin", nameof(format));

        return new FormatIO(Console.OpenStandardInput(), format, ownsStream: false);
    }

    /// <summary>
    /// Opens a custom stream for parsing with explicit format specification.
    /// </summary>
    /// <param name="stream">The input stream to read from</param>
    /// <param name="format">The format of the stream data</param>
    /// <returns>FormatIO instance for fluent configuration</returns>
    /// <exception cref="ArgumentNullException">If stream is null</exception>
    /// <exception cref="ArgumentException">If format is Unknown</exception>
    public static FormatIO Open(Stream stream, Format format)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        if (format == Format.Unknown)
            throw new ArgumentException("Format must be specified for stream", nameof(format));

        return new FormatIO(stream, format, ownsStream: false);
    }

    #endregion

    #region Fluent Configuration Methods

    /// <summary>
    /// Configures parsing options via fluent API.
    /// </summary>
    /// <param name="configure">Action to configure ParseOptions</param>
    /// <returns>This FormatIO instance for method chaining</returns>
    /// <exception cref="ArgumentNullException">If configure action is null</exception>
    public FormatIO WithOptions(Action<ParseOptions> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        configure(_options);
        return this;
    }

    /// <summary>
    /// Sets the output format for conversion.
    /// </summary>
    /// <param name="targetFormat">The format to convert to</param>
    /// <returns>This FormatIO instance for method chaining</returns>
    /// <exception cref="ArgumentException">If target format is Unknown</exception>
    /// <exception cref="NotSupportedException">If conversion is not supported</exception>
    public FormatIO ConvertTo(Format targetFormat)
    {
        if (targetFormat == Format.Unknown)
            throw new ArgumentException("Cannot convert to Unknown format", nameof(targetFormat));

        // Validate conversion is supported
        if (!IsConversionSupported(_inputFormat, targetFormat))
            throw new NotSupportedException(
                $"Conversion from {_inputFormat} to {targetFormat} is not supported");

        _convertToFormat = targetFormat;
        _options.OutputFormat = targetFormat;
        return this;
    }

    /// <summary>
    /// Configures magazine and row filtering.
    /// </summary>
    /// <param name="magazine">Magazine number to filter (null = all)</param>
    /// <param name="rows">Row numbers to include (null = all)</param>
    /// <returns>This FormatIO instance for method chaining</returns>
    public FormatIO Filter(int? magazine = null, params int[] rows)
    {
        _options.Magazine = magazine;
        _options.Rows = rows?.Length > 0 ? rows : null;
        return this;
    }

    /// <summary>
    /// Sets the line count for timecode incrementation.
    /// </summary>
    /// <param name="lineCount">Number of lines per frame</param>
    /// <returns>This FormatIO instance for method chaining</returns>
    /// <exception cref="ArgumentOutOfRangeException">If line count is not positive</exception>
    public FormatIO WithLineCount(int lineCount)
    {
        if (lineCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(lineCount), "Line count must be positive");

        _options.LineCount = lineCount;
        return this;
    }

    /// <summary>
    /// Sets the starting timecode for packet numbering.
    /// </summary>
    /// <param name="startTimecode">Starting timecode</param>
    /// <returns>This FormatIO instance for method chaining</returns>
    /// <exception cref="ArgumentNullException">If start timecode is null</exception>
    public FormatIO WithStartTimecode(Timecode startTimecode)
    {
        _options.StartTimecode = startTimecode ?? throw new ArgumentNullException(nameof(startTimecode));
        return this;
    }

    /// <summary>
    /// Sets PIDs for TS format filtering.
    /// </summary>
    /// <param name="pids">PIDs to filter</param>
    /// <returns>This FormatIO instance for method chaining</returns>
    public FormatIO WithPIDs(params int[] pids)
    {
        _options.PIDs = pids?.Length > 0 ? pids : null;
        return this;
    }

    #endregion

    #region Parsing Methods

    /// <summary>
    /// Parses the input as lines (for line-based formats like VBI, T42).
    /// For packet-based formats, automatically flattens packets to lines.
    /// </summary>
    /// <returns>Enumerable of parsed lines</returns>
    /// <exception cref="InvalidOperationException">If no handler is registered for the format</exception>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public IEnumerable<Line> ParseLines()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Get the appropriate handler from FormatRegistry
        if (FormatRegistry.TryGetHandler(_inputFormat, out var handlerBase))
        {
            if (handlerBase is ILineFormatHandler lineHandler)
            {
                return lineHandler.Parse(_inputStream, _options);
            }
            else if (handlerBase is IPacketFormatHandler packetHandler)
            {
                // Convert packets to lines for unified API
                return FlattenPacketsToLines(packetHandler.Parse(_inputStream, _options));
            }
        }

        throw new InvalidOperationException($"No handler registered for format: {_inputFormat}");
    }

    /// <summary>
    /// Parses the input as packets (for packet-based formats like ANC, TS, MXF).
    /// For line-based formats, automatically groups lines into packets by timecode.
    /// </summary>
    /// <returns>Enumerable of parsed packets</returns>
    /// <exception cref="InvalidOperationException">If no handler is registered for the format</exception>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public IEnumerable<Packet> ParsePackets()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (FormatRegistry.TryGetHandler(_inputFormat, out var handlerBase))
        {
            if (handlerBase is IPacketFormatHandler packetHandler)
            {
                return packetHandler.Parse(_inputStream, _options);
            }
            else if (handlerBase is ILineFormatHandler lineHandler)
            {
                // Wrap lines into packets for unified API
                return GroupLinesToPackets(lineHandler.Parse(_inputStream, _options));
            }
        }

        throw new InvalidOperationException($"No handler registered for format: {_inputFormat}");
    }

    /// <summary>
    /// Asynchronously parses the input as lines.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of parsed lines</returns>
    /// <exception cref="InvalidOperationException">If no handler is registered for the format</exception>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public async IAsyncEnumerable<Line> ParseLinesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (FormatRegistry.TryGetHandler(_inputFormat, out var handlerBase))
        {
            if (handlerBase is ILineFormatHandler lineHandler)
            {
                await foreach (var line in lineHandler.ParseAsync(_inputStream, _options, cancellationToken))
                {
                    yield return line;
                }
            }
            else if (handlerBase is IPacketFormatHandler packetHandler)
            {
                await foreach (var packet in packetHandler.ParseAsync(_inputStream, _options, cancellationToken))
                {
                    foreach (var line in packet.Lines)
                    {
                        yield return line;
                    }
                }
            }
            else
            {
                throw new InvalidOperationException($"No handler registered for format: {_inputFormat}");
            }
        }
        else
        {
            throw new InvalidOperationException($"No handler registered for format: {_inputFormat}");
        }
    }

    /// <summary>
    /// Asynchronously parses the input as packets.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of parsed packets</returns>
    /// <exception cref="InvalidOperationException">If no handler is registered for the format</exception>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public async IAsyncEnumerable<Packet> ParsePacketsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (FormatRegistry.TryGetHandler(_inputFormat, out var handlerBase))
        {
            if (handlerBase is IPacketFormatHandler packetHandler)
            {
                await foreach (var packet in packetHandler.ParseAsync(_inputStream, _options, cancellationToken))
                {
                    yield return packet;
                }
            }
            else if (handlerBase is ILineFormatHandler lineHandler)
            {
                // Group lines into packets by timecode
                var currentPacket = new Packet([0, 0]);
                Timecode? lastTimecode = null;

                await foreach (var line in lineHandler.ParseAsync(_inputStream, _options, cancellationToken))
                {
                    // Group lines by timecode into packets
                    if (lastTimecode != null && line.LineTimecode != null &&
                        !line.LineTimecode.Equals(lastTimecode))
                    {
                        if (currentPacket.Lines.Count > 0)
                        {
                            currentPacket.Timecode = lastTimecode;
                            yield return currentPacket;
                            currentPacket = new Packet([0, 0]);
                        }
                    }

                    currentPacket.Lines.Add(line);
                    lastTimecode = line.LineTimecode ?? new Timecode(0);
                }

                // Yield final packet
                if (currentPacket.Lines.Count > 0)
                {
                    currentPacket.Timecode = lastTimecode ?? new Timecode(0);
                    yield return currentPacket;
                }
            }
            else
            {
                throw new InvalidOperationException($"No handler registered for format: {_inputFormat}");
            }
        }
        else
        {
            throw new InvalidOperationException($"No handler registered for format: {_inputFormat}");
        }
    }

    #endregion

    #region SaveTo Methods

    /// <summary>
    /// Saves parsed and converted data to a file synchronously.
    /// </summary>
    /// <param name="outputPath">Path to the output file</param>
    /// <param name="useSTLMerging">Whether to use intelligent STL subtitle merging (default: false)</param>
    /// <exception cref="ArgumentException">If output path is invalid</exception>
    /// <exception cref="IOException">If file write fails</exception>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public void SaveTo(string outputPath, bool useSTLMerging = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path cannot be empty", nameof(outputPath));

        _outputFile = new FileInfo(outputPath);
        _outputStream = _outputFile.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
        _ownsOutputStream = true;

        try
        {
            SaveToInternal(_outputStream, useSTLMerging);
        }
        catch
        {
            _outputStream?.Dispose();
            _outputStream = null;
            throw;
        }
    }

    /// <summary>
    /// Saves parsed and converted data to stdout synchronously.
    /// </summary>
    /// <param name="useSTLMerging">Whether to use intelligent STL subtitle merging (default: false)</param>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public void SaveToStdout(bool useSTLMerging = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _outputStream = Console.OpenStandardOutput();
        _ownsOutputStream = false;
        SaveToInternal(_outputStream, useSTLMerging);
    }

    /// <summary>
    /// Saves parsed and converted data to a file asynchronously.
    /// </summary>
    /// <param name="outputPath">Path to the output file</param>
    /// <param name="useSTLMerging">Whether to use intelligent STL subtitle merging (default: false)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="ArgumentException">If output path is invalid</exception>
    /// <exception cref="IOException">If file write fails</exception>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public async Task SaveToAsync(string outputPath, bool useSTLMerging = false,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path cannot be empty", nameof(outputPath));

        _outputFile = new FileInfo(outputPath);
        _outputStream = _outputFile.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
        _ownsOutputStream = true;

        try
        {
            await SaveToInternalAsync(_outputStream, useSTLMerging, cancellationToken);
        }
        catch
        {
            if (_outputStream != null)
                await _outputStream.DisposeAsync();
            _outputStream = null;
            throw;
        }
    }

    /// <summary>
    /// Saves parsed and converted data to stdout asynchronously.
    /// </summary>
    /// <param name="useSTLMerging">Whether to use intelligent STL subtitle merging (default: false)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public async Task SaveToStdoutAsync(bool useSTLMerging = false,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _outputStream = Console.OpenStandardOutput();
        _ownsOutputStream = false;
        await SaveToInternalAsync(_outputStream, useSTLMerging, cancellationToken);
    }

    #endregion

    #region Private Helper Methods - SaveTo Implementation

    private void SaveToInternal(Stream outputStream, bool useSTLMerging)
    {
        var outputFormat = _convertToFormat ?? _inputFormat;

        // Write headers for formats that require them
        if (outputFormat == Format.RCWT)
        {
            Functions.ResetRCWTHeader();
            outputStream.Write(Constants.RCWT_HEADER, 0, Constants.RCWT_HEADER.Length);
        }
        else if (outputFormat == Format.STL)
        {
            Functions.ResetSTLSubtitleNumber();
            WriteSTLHeader(outputStream);
        }

        // Handle STL merging if requested
        if (outputFormat == Format.STL && useSTLMerging)
        {
            SaveToSTLWithMerging(outputStream);
            return;
        }

        // Standard output path
        foreach (var packet in ParsePackets())
        {
            foreach (var line in packet.Lines)
            {
                WriteLineToStream(line, outputStream, outputFormat);
            }
        }
    }

    private async Task SaveToInternalAsync(Stream outputStream, bool useSTLMerging,
        CancellationToken cancellationToken)
    {
        var outputFormat = _convertToFormat ?? _inputFormat;

        // Write headers
        if (outputFormat == Format.RCWT)
        {
            Functions.ResetRCWTHeader();
            await outputStream.WriteAsync(Constants.RCWT_HEADER, cancellationToken);
        }
        else if (outputFormat == Format.STL)
        {
            Functions.ResetSTLSubtitleNumber();
            await WriteSTLHeaderAsync(outputStream, cancellationToken);
        }

        // Handle STL merging if requested
        if (outputFormat == Format.STL && useSTLMerging)
        {
            await SaveToSTLWithMergingAsync(outputStream, cancellationToken);
            return;
        }

        // Standard output path
        await foreach (var packet in ParsePacketsAsync(cancellationToken))
        {
            foreach (var line in packet.Lines)
            {
                await WriteLineToStreamAsync(line, outputStream, outputFormat, cancellationToken);
            }
        }
    }

    private void WriteLineToStream(Line line, Stream outputStream, Format outputFormat)
    {
        if (line.Type == Format.Unknown)
            return;

        byte[] dataToWrite = outputFormat switch
        {
            Format.RCWT => ConvertLineToRCWT(line),
            Format.STL => ConvertLineToSTL(line),
            _ => line.Data
        };

        if (dataToWrite.Length > 0)
        {
            outputStream.Write(dataToWrite, 0, dataToWrite.Length);
        }
    }

    private async Task WriteLineToStreamAsync(Line line, Stream outputStream, Format outputFormat,
        CancellationToken cancellationToken)
    {
        if (line.Type == Format.Unknown)
            return;

        byte[] dataToWrite = outputFormat switch
        {
            Format.RCWT => ConvertLineToRCWT(line),
            Format.STL => ConvertLineToSTL(line),
            _ => line.Data
        };

        if (dataToWrite.Length > 0)
        {
            await outputStream.WriteAsync(dataToWrite, cancellationToken);
        }
    }

    private byte[] ConvertLineToRCWT(Line line)
    {
        var (fts, fieldNumber) = Functions.GetRCWTState(line.LineTimecode, verbose: false);

        // Get T42 data from line
        byte[] t42Data = line.Type == Format.T42 && line.Data.Length >= Constants.T42_LINE_SIZE
            ? line.Data.Take(Constants.T42_LINE_SIZE).ToArray()
            : line.Type == Format.VBI || line.Type == Format.VBI_DOUBLE
                ? Core.FormatConverter.VBIToT42(line.Data)
                : new byte[Constants.T42_LINE_SIZE];

        return Core.FormatConverter.T42ToRCWT(t42Data, fts, fieldNumber, verbose: false);
    }

    private byte[] ConvertLineToSTL(Line line)
    {
        // Skip empty lines
        if (IsSTLLineEmpty(line))
            return Array.Empty<byte>();

        var subtitleNumber = Functions.GetNextSTLSubtitleNumber();
        var timeCodeIn = line.LineTimecode ?? new Timecode(0);
        var timeCodeOut = timeCodeIn.GetNext();

        // Get T42 data from line
        byte[] t42Data = line.Type == Format.T42 && line.Data.Length >= Constants.T42_LINE_SIZE
            ? line.Data.Take(Constants.T42_LINE_SIZE).ToArray()
            : line.Type == Format.VBI || line.Type == Format.VBI_DOUBLE
                ? Core.FormatConverter.VBIToT42(line.Data)
                : new byte[Constants.T42_LINE_SIZE];

        return Core.FormatConverter.T42ToSTL(t42Data, subtitleNumber, line.Row, timeCodeIn, timeCodeOut);
    }

    private static bool IsSTLLineEmpty(Line line)
    {
        // Check if line contains only spaces or is effectively empty
        // Reuse logic from Functions.IsSTLLineEmpty if available
        byte[] t42Data = line.Type == Format.T42 && line.Data.Length >= Constants.T42_LINE_SIZE
            ? line.Data.Take(Constants.T42_LINE_SIZE).ToArray()
            : line.Type == Format.VBI || line.Type == Format.VBI_DOUBLE
                ? Core.FormatConverter.VBIToT42(line.Data)
                : new byte[Constants.T42_LINE_SIZE];

        // Extract text data (skip first 2 bytes for mag/row)
        var textBytes = t42Data.Skip(2).Take(40).ToArray();

        // Check if all bytes are spaces (0x20) or control codes
        return textBytes.All(b => b == 0x20 || b < 0x20);
    }

    private void WriteSTLHeader(Stream outputStream)
    {
        var gsi = STLExporter.CreateGSIHeader();
        outputStream.Write(gsi, 0, gsi.Length);
    }

    private async Task WriteSTLHeaderAsync(Stream outputStream, CancellationToken cancellationToken)
    {
        var gsi = STLExporter.CreateGSIHeader();
        await outputStream.WriteAsync(gsi, cancellationToken);
    }

    private void SaveToSTLWithMerging(Stream outputStream)
    {
        using var exporter = new STLExporter
        {
            Magazine = _options.Magazine,
            Rows = _options.Rows ?? Constants.CAPTION_ROWS,
            Verbose = false
        };

        foreach (var packet in ParsePackets())
        {
            foreach (var block in exporter.Export(new[] { packet }))
            {
                outputStream.Write(block, 0, block.Length);
            }
        }

        // Flush remaining content
        foreach (var block in exporter.Flush())
        {
            outputStream.Write(block, 0, block.Length);
        }
    }

    private async Task SaveToSTLWithMergingAsync(Stream outputStream, CancellationToken cancellationToken)
    {
        using var exporter = new STLExporter
        {
            Magazine = _options.Magazine,
            Rows = _options.Rows ?? Constants.CAPTION_ROWS,
            Verbose = false
        };

        await foreach (var packet in ParsePacketsAsync(cancellationToken))
        {
            foreach (var block in exporter.Export(new[] { packet }))
            {
                await outputStream.WriteAsync(block, cancellationToken);
            }
        }

        // Flush remaining content
        foreach (var block in exporter.Flush())
        {
            await outputStream.WriteAsync(block, cancellationToken);
        }
    }

    #endregion

    #region Private Helper Methods - Format Detection & Conversion

    private static Format DetectFormatFromExtension(string extension)
    {
        return extension.TrimStart('.').ToLowerInvariant() switch
        {
            "bin" => Format.ANC,
            "vbi" => Format.VBI,
            "vbid" => Format.VBI_DOUBLE,
            "t42" => Format.T42,
            "mxf" => Format.MXF,
            "ts" => Format.TS,
            "rcwt" => Format.RCWT,
            "stl" => Format.STL,
            _ => Format.Unknown
        };
    }

    private static bool IsConversionSupported(Format from, Format to)
    {
        // Based on FormatConverter supported conversions
        return (from, to) switch
        {
            // VBI conversions
            (Format.VBI, Format.T42) => true,
            (Format.VBI, Format.VBI_DOUBLE) => true,
            (Format.VBI, Format.RCWT) => true,
            (Format.VBI, Format.STL) => true,

            // VBI_DOUBLE conversions
            (Format.VBI_DOUBLE, Format.T42) => true,
            (Format.VBI_DOUBLE, Format.VBI) => true,
            (Format.VBI_DOUBLE, Format.RCWT) => true,
            (Format.VBI_DOUBLE, Format.STL) => true,

            // T42 conversions
            (Format.T42, Format.VBI) => true,
            (Format.T42, Format.VBI_DOUBLE) => true,
            (Format.T42, Format.RCWT) => true,
            (Format.T42, Format.STL) => true,

            // ANC conversions
            (Format.ANC, Format.VBI) => true,
            (Format.ANC, Format.VBI_DOUBLE) => true,
            (Format.ANC, Format.T42) => true,
            (Format.ANC, Format.RCWT) => true,
            (Format.ANC, Format.STL) => true,

            // TS conversions
            (Format.TS, Format.VBI) => true,
            (Format.TS, Format.VBI_DOUBLE) => true,
            (Format.TS, Format.T42) => true,
            (Format.TS, Format.RCWT) => true,
            (Format.TS, Format.STL) => true,

            // MXF conversions
            (Format.MXF, Format.VBI) => true,
            (Format.MXF, Format.VBI_DOUBLE) => true,
            (Format.MXF, Format.T42) => true,
            (Format.MXF, Format.RCWT) => true,
            (Format.MXF, Format.STL) => true,

            // Same format (no conversion)
            _ when from == to => true,

            _ => false
        };
    }

    private static IEnumerable<Line> FlattenPacketsToLines(IEnumerable<Packet> packets)
    {
        foreach (var packet in packets)
        {
            foreach (var line in packet.Lines)
            {
                yield return line;
            }
        }
    }

    private static IEnumerable<Packet> GroupLinesToPackets(IEnumerable<Line> lines)
    {
        var currentPacket = new Packet([0, 0]);
        Timecode? lastTimecode = null;

        foreach (var line in lines)
        {
            if (lastTimecode != null && line.LineTimecode != null &&
                !line.LineTimecode.Equals(lastTimecode))
            {
                if (currentPacket.Lines.Count > 0)
                {
                    currentPacket.Timecode = lastTimecode;
                    yield return currentPacket;
                    currentPacket = new Packet([0, 0]);
                }
            }

            currentPacket.Lines.Add(line);
            lastTimecode = line.LineTimecode ?? new Timecode(0);
        }

        if (currentPacket.Lines.Count > 0)
        {
            currentPacket.Timecode = lastTimecode ?? new Timecode(0);
            yield return currentPacket;
        }
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Disposes resources used by this FormatIO instance.
    /// Flushes and closes output stream if owned, and closes input stream if owned.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            // Flush output if writing
            if (_outputStream != null)
            {
                try
                {
                    _outputStream.Flush();
                }
                catch (ObjectDisposedException)
                {
                    // Stream already disposed, ignore
                }

                if (_ownsOutputStream)
                {
                    _outputStream.Dispose();
                }
            }

            // Close input stream if we own it
            if (_ownsStream && _inputStream != null)
            {
                _inputStream.Dispose();
            }
        }
        catch (ObjectDisposedException)
        {
            // Stream already disposed, ignore
        }
        finally
        {
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// Exception thrown when format detection fails.
/// </summary>
public class FormatDetectionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the FormatDetectionException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error</param>
    public FormatDetectionException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the FormatDetectionException class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception</param>
    /// <param name="innerException">The exception that is the cause of the current exception</param>
    public FormatDetectionException(string message, Exception innerException)
        : base(message, innerException) { }
}
