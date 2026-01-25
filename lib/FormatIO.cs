using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Exporters;
using nathanbutlerDEV.libopx.Formats;

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
    private bool _isConsumed;  // Set to true after terminal operations (Extract/Restripe)

    // Store original filter settings for keepBlanks checking
    private int? _filterMagazine;
    private int[]? _filterRows;

    // Store input file path for MXF operations
    private string? _inputFilePath;

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
        var io = new FormatIO(stream, format, ownsStream: true)
        {
            _inputFilePath = fileInfo.FullName
        };

        // For MXF files, read the start timecode from the file
        if (format == Format.MXF)
        {
            io.ReadMXFStartTimecode();
        }

        return io;
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

    #region Public Accessors for Extension Methods

    /// <summary>
    /// Gets the input file path if FormatIO was opened from a file.
    /// Returns null if opened from stdin or a stream.
    /// </summary>
    /// <returns>The full path to the input file, or null</returns>
    public string? GetInputFilePath() => _inputFilePath;

    /// <summary>
    /// Gets the input format.
    /// </summary>
    /// <returns>The input format</returns>
    public Format GetInputFormat() => _inputFormat;

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

        // Store original filter settings for keepBlanks checking
        _filterMagazine = magazine;
        _filterRows = rows?.Length > 0 ? rows : null;

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

    /// <summary>
    /// Sets the page number filter for teletext data.
    /// </summary>
    /// <param name="pageNumber">Page number (2-digit hex)</param>
    /// <returns>This FormatIO instance for method chaining</returns>
    public FormatIO WithPageNumber(string? pageNumber)
    {
        _options.PageNumber = pageNumber;
        return this;
    }

    /// <summary>
    /// Sets whether to use caption row filtering (1-24) with content filtering.
    /// </summary>
    /// <param name="useCaps">Whether to filter out rows with only spaces/control codes</param>
    /// <returns>This FormatIO instance for method chaining</returns>
    public FormatIO WithUseCaps(bool useCaps)
    {
        _options.UseCaps = useCaps;
        return this;
    }

    /// <summary>
    /// Configures whether to preserve blank bytes for filtered-out rows.
    /// When enabled, filtered rows are replaced with blank bytes instead of being omitted,
    /// preserving stream structure and byte alignment.
    /// </summary>
    /// <param name="keepBlanks">True to write blank bytes for filtered rows, false to omit them</param>
    /// <returns>This FormatIO instance for method chaining</returns>
    public FormatIO WithKeepBlanks(bool keepBlanks = true)
    {
        _options.KeepBlanks = keepBlanks;
        return this;
    }

    /// <summary>
    /// Configures extraction to demux all keys to separate files.
    /// MXF-specific option for Extract operations.
    /// </summary>
    /// <param name="demuxMode">Whether to enable demux mode (default: true)</param>
    /// <returns>This FormatIO instance for method chaining</returns>
    public FormatIO WithDemuxMode(bool demuxMode = true)
    {
        _options.DemuxMode = demuxMode;
        return this;
    }

    /// <summary>
    /// Configures extraction to use key names instead of hex identifiers for output files.
    /// MXF-specific option, only applies when DemuxMode is true.
    /// </summary>
    /// <param name="useKeyNames">Whether to use key names (default: true)</param>
    /// <returns>This FormatIO instance for method chaining</returns>
    public FormatIO WithKeyNames(bool useKeyNames = true)
    {
        _options.UseKeyNames = useKeyNames;
        return this;
    }

    /// <summary>
    /// Configures extraction to include KLV headers in output files.
    /// MXF-specific option for Extract operations.
    /// </summary>
    /// <param name="klvMode">Whether to include KLV headers (default: true)</param>
    /// <returns>This FormatIO instance for method chaining</returns>
    public FormatIO WithKlvMode(bool klvMode = true)
    {
        _options.KlvMode = klvMode;
        return this;
    }

    /// <summary>
    /// Configures which key types to extract from MXF files.
    /// MXF-specific option for Extract operations.
    /// </summary>
    /// <param name="keys">Key types to extract (Data, Video, Audio, System, TimecodeComponent)</param>
    /// <returns>This FormatIO instance for method chaining</returns>
    public FormatIO WithKeys(params KeyType[] keys)
    {
        _options.RequiredKeys = keys?.Length > 0 ? keys.ToList() : null;
        return this;
    }

    /// <summary>
    /// Configures whether to print progress during operations.
    /// Useful for long-running MXF operations like Extract and Restripe.
    /// </summary>
    /// <param name="printProgress">Whether to print progress (default: true)</param>
    /// <returns>This FormatIO instance for method chaining</returns>
    public FormatIO WithProgress(bool printProgress = true)
    {
        _options.PrintProgress = printProgress;
        return this;
    }

    /// <summary>
    /// Configures verbose output for operations.
    /// </summary>
    /// <param name="verbose">Whether to enable verbose output (default: true)</param>
    /// <returns>This FormatIO instance for method chaining</returns>
    public FormatIO WithVerbose(bool verbose = true)
    {
        _options.Verbose = verbose;
        return this;
    }

    #endregion

    #region Private Validation Helpers

    /// <summary>
    /// Ensures the FormatIO was opened from a file path (not stdin/stream).
    /// </summary>
    /// <exception cref="InvalidOperationException">If not opened from a file</exception>
    private void EnsureFileInput()
    {
        if (string.IsNullOrEmpty(_inputFilePath))
        {
            throw new InvalidOperationException(
                "This operation requires FormatIO to be opened from a file. " +
                "Use FormatIO.Open(filePath) instead of OpenStdin or Open(stream).");
        }
    }

    /// <summary>
    /// Ensures the input format is MXF.
    /// </summary>
    /// <exception cref="InvalidOperationException">If format is not MXF</exception>
    private void EnsureMXFFormat()
    {
        if (_inputFormat != Format.MXF)
        {
            throw new InvalidOperationException(
                $"This operation is only supported for MXF format. Current format: {_inputFormat}");
        }
    }

    /// <summary>
    /// Ensures the FormatIO has not been consumed by a terminal operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">If already consumed</exception>
    private void EnsureNotConsumed()
    {
        if (_isConsumed)
        {
            throw new InvalidOperationException(
                "This FormatIO instance has been consumed by a terminal operation (Extract/Restripe) " +
                "and cannot be used for further operations.");
        }
    }

    /// <summary>
    /// Closes the input stream if we own it, preparing for MXF to take over file access.
    /// </summary>
    private void CloseInputStream()
    {
        if (_ownsStream && _inputStream != null)
        {
            try
            {
                _inputStream.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
        }
    }

    /// <summary>
    /// Reads the start timecode from an MXF file by parsing the TimecodeComponent.
    /// This ensures that when parsing MXF files, the correct start timecode is used
    /// (especially important after restriping operations).
    /// </summary>
    private void ReadMXFStartTimecode()
    {
        if (_inputStream == null || !_inputStream.CanSeek)
            return;

        try
        {
            _inputStream.Seek(0, SeekOrigin.Begin);
            var keyBuffer = new byte[Constants.KLV_KEY_SIZE];

            // Search for TimecodeComponent within first 128KB of file
            while (_inputStream.Position < 128000)
            {
                var keyBytesRead = _inputStream.Read(keyBuffer, 0, Constants.KLV_KEY_SIZE);
                if (keyBytesRead != Constants.KLV_KEY_SIZE) break;

                KeyType keyType;
                try
                {
                    keyType = Keys.GetKeyType(keyBuffer.AsSpan(0, Constants.KLV_KEY_SIZE));
                }
                catch (IOException)
                {
                    // Not a valid MXF file, skip reading start timecode
                    break;
                }

                var length = ReadBerLength(_inputStream);
                if (length < 0) break;

                if (keyType == KeyType.TimecodeComponent)
                {
                    var data = new byte[length];
                    var dataBytesRead = _inputStream.Read(data, 0, length);
                    if (dataBytesRead != length) break;

                    var timecodeComponent = TimecodeComponent.Parse(data);
                    _options.StartTimecode = new Timecode(
                        timecodeComponent.StartTimecode,
                        timecodeComponent.RoundedTimecodeTimebase,
                        timecodeComponent.DropFrame);

                    _inputStream.Seek(0, SeekOrigin.Begin);
                    return;
                }
                else
                {
                    _inputStream.Seek(length, SeekOrigin.Current);
                }
            }
        }
        finally
        {
            // Always reset stream position
            _inputStream.Seek(0, SeekOrigin.Begin);
        }
    }

    /// <summary>
    /// Reads a BER (Basic Encoding Rules) length from the stream.
    /// Used for parsing MXF KLV packets.
    /// </summary>
    private static int ReadBerLength(Stream input)
    {
        var firstByte = input.ReadByte();
        if (firstByte < 0) return -1;

        if ((firstByte & 0x80) == 0)
        {
            // Short form: length is in the first byte
            return firstByte;
        }

        // Long form: first byte indicates number of length bytes
        var numLengthBytes = firstByte & 0x7F;
        if (numLengthBytes > 4 || numLengthBytes == 0) return -1;

        var lengthBytes = new byte[numLengthBytes];
        var bytesRead = input.Read(lengthBytes, 0, numLengthBytes);
        if (bytesRead != numLengthBytes) return -1;

        int length = 0;
        for (int i = 0; i < numLengthBytes; i++)
        {
            length = (length << 8) | lengthBytes[i];
        }

        return length;
    }

    #endregion

    #region Parsing Methods

    /// <summary>
    /// Parses the input as lines (for line-based formats like VBI, T42).
    /// For packet-based formats, automatically flattens packets to lines.
    /// </summary>
    /// <returns>Enumerable of parsed lines</returns>
    /// <exception cref="InvalidOperationException">If no handler is registered for the format, or instance has been consumed</exception>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public IEnumerable<Line> ParseLines()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureNotConsumed();

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
    /// <exception cref="InvalidOperationException">If no handler is registered for the format, or instance has been consumed</exception>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public IEnumerable<Packet> ParsePackets()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureNotConsumed();

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
    /// <exception cref="InvalidOperationException">If no handler is registered for the format, or instance has been consumed</exception>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public async IAsyncEnumerable<Line> ParseLinesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureNotConsumed();

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
    /// <exception cref="InvalidOperationException">If no handler is registered for the format, or instance has been consumed</exception>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public async IAsyncEnumerable<Packet> ParsePacketsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureNotConsumed();

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
    /// <exception cref="InvalidOperationException">If instance has been consumed</exception>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public void SaveTo(string outputPath, bool useSTLMerging = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureNotConsumed();

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
    /// <exception cref="InvalidOperationException">If instance has been consumed</exception>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public void SaveToStdout(bool useSTLMerging = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureNotConsumed();

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
    /// <exception cref="InvalidOperationException">If instance has been consumed</exception>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public async Task SaveToAsync(string outputPath, bool useSTLMerging = false,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureNotConsumed();

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
    /// <exception cref="InvalidOperationException">If instance has been consumed</exception>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public async Task SaveToStdoutAsync(bool useSTLMerging = false,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureNotConsumed();

        _outputStream = Console.OpenStandardOutput();
        _ownsOutputStream = false;
        await SaveToInternalAsync(_outputStream, useSTLMerging, cancellationToken);
    }

    #endregion

    #region MXF Terminal Operations

    /// <summary>
    /// Extracts essence data from an MXF file to separate output files.
    /// This is a terminal operation - after calling, the FormatIO instance is consumed
    /// and cannot be used for further parsing or saving operations.
    /// </summary>
    /// <param name="outputBasePath">Base path for output files (without extension).
    /// Files will be created as {base}_d.raw, {base}_v.raw, etc.
    /// If null, uses the input file's directory and name without extension.</param>
    /// <returns>ExtractResult containing paths to extracted files and operation status</returns>
    /// <exception cref="InvalidOperationException">If FormatIO was not opened from a file,
    /// format is not MXF, or instance has been consumed</exception>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public ExtractResult ExtractTo(string? outputBasePath = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureNotConsumed();
        EnsureFileInput();
        EnsureMXFFormat();

        var result = new ExtractResult();

        try
        {
            // Close our stream so MXF can open the file
            CloseInputStream();

            // Determine output base path
            var basePath = outputBasePath ?? Path.Combine(
                Path.GetDirectoryName(_inputFilePath) ?? ".",
                Path.GetFileNameWithoutExtension(_inputFilePath) ?? "output");

            // Create MXF instance with read-only access for extraction
            using var mxf = new MXF(_inputFilePath!)
            {
                OutputBasePath = basePath,
                DemuxMode = _options.DemuxMode,
                UseKeyNames = _options.UseKeyNames && _options.DemuxMode,
                KlvMode = _options.KlvMode,
                Verbose = _options.Verbose,
                PrintProgress = _options.PrintProgress
            };

            // Configure required keys
            if (_options.RequiredKeys != null && _options.RequiredKeys.Count > 0)
            {
                mxf.ClearRequiredKeys();
                foreach (var key in _options.RequiredKeys)
                {
                    mxf.AddRequiredKey(key);
                }
            }

            // Execute extraction
            mxf.ExtractEssence();

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            // Mark as consumed - no further operations allowed
            _isConsumed = true;
        }

        return result;
    }

    /// <summary>
    /// Asynchronously extracts essence data from an MXF file to separate output files.
    /// This is a terminal operation - after calling, the FormatIO instance is consumed.
    /// </summary>
    /// <param name="outputBasePath">Base path for output files (without extension)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ExtractResult containing paths to extracted files and operation status</returns>
    /// <exception cref="InvalidOperationException">If FormatIO was not opened from a file,
    /// format is not MXF, or instance has been consumed</exception>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public async Task<ExtractResult> ExtractToAsync(
        string? outputBasePath = null,
        CancellationToken cancellationToken = default)
    {
        // Run on thread pool since MXF.ExtractEssence is synchronous
        return await Task.Run(() => ExtractTo(outputBasePath), cancellationToken);
    }

    /// <summary>
    /// Restripes the MXF file in-place with a new start timecode.
    /// WARNING: This modifies the source file directly.
    /// This is a terminal operation - after calling, the FormatIO instance is consumed.
    /// </summary>
    /// <param name="newStartTimecode">New start timecode in HH:MM:SS:FF format</param>
    /// <exception cref="InvalidOperationException">If FormatIO was not opened from a file,
    /// format is not MXF, or instance has been consumed</exception>
    /// <exception cref="ArgumentException">If timecode format is invalid</exception>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public void Restripe(string newStartTimecode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureNotConsumed();
        EnsureFileInput();
        EnsureMXFFormat();

        // Validate timecode format
        try
        {
            _ = new Timecode(newStartTimecode);
        }
        catch (FormatException)
        {
            throw new ArgumentException(
                $"Invalid timecode format: {newStartTimecode}. Expected format: HH:MM:SS:FF",
                nameof(newStartTimecode));
        }

        try
        {
            // Close our stream so MXF can open the file with read-write access
            CloseInputStream();

            // Create MXF instance with read-write access for restripe
            using var mxf = new MXF(new FileInfo(_inputFilePath!))
            {
                Function = Function.Restripe,
                Verbose = _options.Verbose,
                PrintProgress = _options.PrintProgress
            };

            // Execute restripe by iterating through Parse with timecode
            foreach (var _ in mxf.Parse(startTimecode: newStartTimecode))
            {
                // Parse handles restripe internally
            }
        }
        finally
        {
            // Mark as consumed - no further operations allowed
            _isConsumed = true;
        }
    }

    /// <summary>
    /// Asynchronously restripes the MXF file in-place with a new start timecode.
    /// WARNING: This modifies the source file directly.
    /// This is a terminal operation - after calling, the FormatIO instance is consumed.
    /// </summary>
    /// <param name="newStartTimecode">New start timecode in HH:MM:SS:FF format</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="progress">Optional progress reporter (0-100)</param>
    /// <exception cref="InvalidOperationException">If FormatIO was not opened from a file,
    /// format is not MXF, or instance has been consumed</exception>
    /// <exception cref="ArgumentException">If timecode format is invalid</exception>
    /// <exception cref="ObjectDisposedException">If the FormatIO instance has been disposed</exception>
    public async Task RestripeAsync(
        string newStartTimecode,
        CancellationToken cancellationToken = default,
        IProgress<double>? progress = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureNotConsumed();
        EnsureFileInput();
        EnsureMXFFormat();

        // Validate timecode format
        try
        {
            _ = new Timecode(newStartTimecode);
        }
        catch (FormatException)
        {
            throw new ArgumentException(
                $"Invalid timecode format: {newStartTimecode}. Expected format: HH:MM:SS:FF",
                nameof(newStartTimecode));
        }

        try
        {
            // Close our stream so MXF can open the file with read-write access
            CloseInputStream();

            // Create MXF instance with read-write access for restripe
            using var mxf = new MXF(new FileInfo(_inputFilePath!))
            {
                Function = Function.Restripe,
                Verbose = _options.Verbose,
                PrintProgress = _options.PrintProgress,
                Progress = progress
            };

            // Execute restripe asynchronously
            await foreach (var _ in mxf.ParseAsync(startTimecode: newStartTimecode, cancellationToken: cancellationToken))
            {
                // Parse handles restripe internally
            }
        }
        finally
        {
            // Mark as consumed - no further operations allowed
            _isConsumed = true;
        }
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

        // When keepBlanks is true, parse all rows and filter during output
        var originalRows = _options.Rows;
        if (_options.KeepBlanks)
        {
            _options.Rows = Constants.DEFAULT_ROWS;
        }

        try
        {
            // Standard output path
            foreach (var packet in ParsePackets())
            {
                foreach (var line in packet.Lines)
                {
                    WriteLineToStream(line, outputStream, outputFormat);
                }
            }
        }
        finally
        {
            // Restore original rows
            _options.Rows = originalRows;
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

        // When keepBlanks is true, parse all rows and filter during output
        var originalRows = _options.Rows;
        if (_options.KeepBlanks)
        {
            _options.Rows = Constants.DEFAULT_ROWS;
        }

        try
        {
            // Standard output path
            await foreach (var packet in ParsePacketsAsync(cancellationToken))
            {
                foreach (var line in packet.Lines)
                {
                    await WriteLineToStreamAsync(line, outputStream, outputFormat, cancellationToken);
                }
            }
        }
        finally
        {
            // Restore original rows
            _options.Rows = originalRows;
        }
    }

    private void WriteLineToStream(Line line, Stream outputStream, Format outputFormat)
    {
        if (line.Type == Format.Unknown)
            return;

        byte[] dataToWrite = ConvertLineData(line, outputFormat);

        if (dataToWrite.Length > 0)
        {
            // Check if line matches filter when keepBlanks is enabled
            if (_options.KeepBlanks && !LineMatchesFilter(line))
            {
                // Write blank bytes to preserve stream structure
                var blankData = new byte[dataToWrite.Length];
                outputStream.Write(blankData, 0, blankData.Length);
            }
            else
            {
                outputStream.Write(dataToWrite, 0, dataToWrite.Length);
            }
        }
    }

    private async Task WriteLineToStreamAsync(Line line, Stream outputStream, Format outputFormat,
        CancellationToken cancellationToken)
    {
        if (line.Type == Format.Unknown)
            return;

        byte[] dataToWrite = ConvertLineData(line, outputFormat);

        if (dataToWrite.Length > 0)
        {
            // Check if line matches filter when keepBlanks is enabled
            if (_options.KeepBlanks && !LineMatchesFilter(line))
            {
                // Write blank bytes to preserve stream structure
                var blankData = new byte[dataToWrite.Length];
                await outputStream.WriteAsync(blankData, cancellationToken);
            }
            else
            {
                await outputStream.WriteAsync(dataToWrite, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Converts line data to the target output format.
    /// Handles all supported format conversions (T42, VBI, VBI_DOUBLE, RCWT, STL).
    /// </summary>
    private byte[] ConvertLineData(Line line, Format outputFormat)
    {
        // Handle special formats first
        if (outputFormat == Format.RCWT)
            return ConvertLineToRCWT(line);
        if (outputFormat == Format.STL)
            return ConvertLineToSTL(line);

        // Get the line's current format and data
        var inputFormat = line.Type;
        var data = line.Data;

        // If formats match, no conversion needed
        if (inputFormat == outputFormat)
            return data;

        // Convert to target format based on input format
        return (inputFormat, outputFormat) switch
        {
            // T42 to VBI/VBI_DOUBLE
            (Format.T42, Format.VBI) => Core.FormatConverter.T42ToVBI(data, Format.VBI),
            (Format.T42, Format.VBI_DOUBLE) => Core.FormatConverter.T42ToVBI(data, Format.VBI_DOUBLE),

            // VBI to other formats
            (Format.VBI, Format.T42) => Core.FormatConverter.VBIToT42(data),
            (Format.VBI, Format.VBI_DOUBLE) => Core.FormatConverter.VBIToVBIDouble(data),

            // VBI_DOUBLE to other formats
            (Format.VBI_DOUBLE, Format.T42) => Core.FormatConverter.VBIToT42(data),
            (Format.VBI_DOUBLE, Format.VBI) => Core.FormatConverter.VBIDoubleToVBI(data),

            // Default: return data as-is (may happen for same format or unsupported conversion)
            _ => data
        };
    }

    /// <summary>
    /// Checks if a line matches the original filter settings (magazine and rows).
    /// Used by keepBlanks logic to determine whether to write actual data or blank bytes.
    /// </summary>
    private bool LineMatchesFilter(Line line)
    {
        // Check magazine filter
        if (_filterMagazine.HasValue && line.Magazine != _filterMagazine.Value)
            return false;

        // Check rows filter (use DEFAULT_ROWS if no filter was set)
        var rows = _filterRows ?? Constants.DEFAULT_ROWS;
        if (!rows.Contains(line.Row))
            return false;

        return true;
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
        // Skip row 0 (page header) - never contains subtitle content
        if (line.Row == 0)
            return Array.Empty<byte>();

        // Skip empty lines (spaces and control codes only)
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
        byte[] t42Data = line.Type == Format.T42 && line.Data.Length >= Constants.T42_LINE_SIZE
            ? line.Data.Take(Constants.T42_LINE_SIZE).ToArray()
            : line.Type == Format.VBI || line.Type == Format.VBI_DOUBLE
                ? Core.FormatConverter.VBIToT42(line.Data)
                : new byte[Constants.T42_LINE_SIZE];

        // Extract text data (skip first 2 bytes for mag/row)
        var textBytes = t42Data.Skip(2).Take(40).ToArray();

        // Check if all bytes are spaces (0x20) or control codes (< 0x20)
        // Strip parity bit (0x7F mask) since T42 data includes odd parity on MSB
        return textBytes.All(b => (b & 0x7F) == 0x20 || (b & 0x7F) < 0x20);
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

            // Close input stream if we own it and it wasn't already closed by a terminal operation
            if (_ownsStream && _inputStream != null && !_isConsumed)
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
