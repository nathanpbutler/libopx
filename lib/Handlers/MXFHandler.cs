using System.Buffers;
using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.SMPTE;

namespace nathanbutlerDEV.libopx.Handlers;

/// <summary>
/// Handler for parsing MXF (Material Exchange Format) files with support for stream extraction,
/// filtering, and timecode restriping. Implements IPacketFormatHandler for teletext data extraction.
/// </summary>
public class MXFHandler : IPacketFormatHandler
{
    #region State Fields

    /// <summary>
    /// Buffer for storing KLV keys (16 bytes).
    /// </summary>
    private readonly byte[] _keyBuffer = new byte[Constants.KLV_KEY_SIZE];

    /// <summary>
    /// Buffer for storing SMPTE timecodes (4 bytes).
    /// </summary>
    private readonly byte[] _smpteBuffer = new byte[Constants.SMPTE_TIMECODE_SIZE];

    /// <summary>
    /// Buffer for storing BER length values during parsing.
    /// </summary>
    private readonly List<byte> _berLengthBuffer = [];

    /// <summary>
    /// Set of unique keys found during demux mode for tracking.
    /// </summary>
    private readonly HashSet<string> _foundKeys = [];

    /// <summary>
    /// Mapping of key types to output file streams for extraction mode.
    /// </summary>
    private readonly Dictionary<KeyType, FileStream> _outputStreams = [];

    /// <summary>
    /// Mapping of key identifiers to output file streams for demux mode.
    /// </summary>
    private readonly Dictionary<string, FileStream> _demuxStreams = [];

    /// <summary>
    /// Mapping of key types to file extensions for output files.
    /// </summary>
    private readonly Dictionary<KeyType, string> _keyTypeToExtension = new()
    {
        { KeyType.Data, "_d.raw" },
        { KeyType.Video, "_v.raw" },
        { KeyType.System, "_s.raw" },
        { KeyType.TimecodeComponent, "_t.raw" },
        { KeyType.Audio, "_a.raw" }
    };

    /// <summary>
    /// Cache the previous timecode for sequential checking to avoid recalculation.
    /// </summary>
    private Timecode? _lastTimecode;

    #endregion

    #region Configuration Properties

    /// <summary>
    /// Gets the start timecode extracted from the MXF file.
    /// </summary>
    public Timecode StartTimecode { get; }

    /// <summary>
    /// Gets the list of SMPTE timecodes per-frame in the MXF file.
    /// </summary>
    public List<Timecode> SMPTETimecodes { get; } = [];

    /// <summary>
    /// Gets the list of packets parsed from the MXF data stream.
    /// </summary>
    public List<Packet> Packets { get; } = [];

    /// <summary>
    /// Gets the list of required key types for filtering during parsing.
    /// </summary>
    private readonly List<KeyType> _requiredKeys;

    /// <summary>
    /// Gets whether to validate that SMPTE timecodes are sequential.
    /// </summary>
    private readonly bool _checkSequential;

    /// <summary>
    /// Gets whether to extract all keys found as separate output files.
    /// </summary>
    private readonly bool _demuxMode;

    /// <summary>
    /// Gets whether to use Key/Essence names instead of hex keys for output filenames.
    /// </summary>
    private readonly bool _useKeyNames;

    /// <summary>
    /// Gets whether to include key and length bytes in output files.
    /// </summary>
    private readonly bool _klvMode;

    /// <summary>
    /// Gets the base path for extracted files.
    /// </summary>
    private readonly string? _outputBasePath;

    /// <summary>
    /// Gets the input file path for generating output paths.
    /// </summary>
    private readonly string? _inputFilePath;

    /// <summary>
    /// Gets whether to enable verbose output for debugging.
    /// </summary>
    private readonly bool _verbose;

    /// <summary>
    /// Gets whether to print progress updates during parsing.
    /// </summary>
    private readonly bool _printProgress;

    /// <summary>
    /// Gets the function mode (Filter, Extract, or Restripe).
    /// </summary>
    private readonly Function _function;

    #endregion

    #region IPacketFormatHandler Implementation

    /// <summary>
    /// Gets the input format that this handler processes.
    /// </summary>
    public Format InputFormat => Format.MXF;

    /// <summary>
    /// Gets the array of valid output formats supported by this handler.
    /// </summary>
    public Format[] ValidOutputs => [Format.T42, Format.VBI, Format.VBI_DOUBLE, Format.RCWT, Format.STL];

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the MXFHandler with the specified configuration.
    /// </summary>
    /// <param name="startTimecode">The start timecode extracted from the MXF file</param>
    /// <param name="inputFilePath">Optional input file path for generating output paths</param>
    /// <param name="requiredKeys">Optional list of required key types for filtering</param>
    /// <param name="checkSequential">Whether to validate sequential SMPTE timecodes (default: true)</param>
    /// <param name="demuxMode">Whether to extract all keys to separate files (default: false)</param>
    /// <param name="useKeyNames">Whether to use key names instead of hex for filenames (default: false)</param>
    /// <param name="klvMode">Whether to include KLV headers in output (default: false)</param>
    /// <param name="outputBasePath">Optional base path for extracted files</param>
    /// <param name="verbose">Whether to enable verbose output (default: false)</param>
    /// <param name="printProgress">Whether to print progress updates (default: false)</param>
    /// <param name="function">The function mode to use (default: Filter)</param>
    public MXFHandler(
        Timecode startTimecode,
        string? inputFilePath = null,
        List<KeyType>? requiredKeys = null,
        bool checkSequential = true,
        bool demuxMode = false,
        bool useKeyNames = false,
        bool klvMode = false,
        string? outputBasePath = null,
        bool verbose = false,
        bool printProgress = false,
        Function function = Function.Filter)
    {
        StartTimecode = startTimecode;
        _inputFilePath = inputFilePath;
        _requiredKeys = requiredKeys ?? [];
        _checkSequential = checkSequential;
        _demuxMode = demuxMode;
        _useKeyNames = useKeyNames;
        _klvMode = klvMode;
        _outputBasePath = outputBasePath;
        _verbose = verbose;
        _printProgress = printProgress;
        _function = function;
    }

    #endregion

    #region Parse Methods

    /// <summary>
    /// Parses the MXF stream and returns an enumerable of packets with optional filtering.
    /// Supports multiple operation modes including filtering, extraction, and restriping.
    /// </summary>
    /// <param name="inputStream">The stream to read MXF data from</param>
    /// <param name="options">Parsing options including filters and output format</param>
    /// <returns>An enumerable of parsed packets matching the filter criteria</returns>
    public IEnumerable<Packet> Parse(Stream inputStream, ParseOptions options)
    {
        inputStream.Seek(0, SeekOrigin.Begin);
        _lastTimecode = null; // Reset sequential checking

        // Determine starting timecode
        var timecode = options.StartTimecode ?? StartTimecode;
        var smpteTimecode = options.StartTimecode ?? StartTimecode;
        var timecodeComponent = options.StartTimecode ?? StartTimecode;

        int lineNumber = 0;

        // Progress tracking
        var restripeStart = DateTime.Now;
        var restripeNow = DateTime.Now;

        try
        {
            while (TryReadKlvHeader(inputStream, out var keyType, out var length))
            {
                // Determine if we should extract or restripe this key
                bool shouldExtract = _function == Function.Extract && (_demuxMode || _requiredKeys.Contains(keyType));

                if (shouldExtract)
                {
                    ExtractPacket(inputStream, keyType, length);
                    continue; // Skip to next iteration after extraction
                }

                // Handle non-extraction processing
                switch (keyType)
                {
                    case KeyType.TimecodeComponent:
                        if (_function == Function.Restripe)
                        {
                            RestripeTimecodeComponent(inputStream, length, timecodeComponent);
                        }
                        else
                        {
                            SkipPacket(inputStream, length);
                        }
                        break;

                    case KeyType.System:
                        if (_function == Function.Restripe)
                        {
                            RestripeSystemPacket(inputStream, length, smpteTimecode);
                            smpteTimecode = smpteTimecode.GetNext(); // Increment timecode for the next packet
                        }
                        else if (ShouldProcessKey(KeyType.System))
                        {
                            ProcessSystemPacket(inputStream, length);
                        }
                        else
                        {
                            SkipPacket(inputStream, length);
                        }
                        break;

                    case KeyType.Data:
                        if (_function == Function.Filter)
                        {
                            var packet = FilterDataPacket(inputStream, options.Magazine, options.Rows, timecode, lineNumber, options.OutputFormat);
                            if (_verbose) Console.WriteLine($"Packet found at timecode {timecode}");
                            // Only yield packets that have at least one line after filtering
                            if (packet.Lines.Count > 0)
                            {
                                yield return packet;
                            }
                            timecode = timecode.GetNext(); // Increment timecode for the next packet
                        }
                        else
                        {
                            if (ShouldProcessKey(KeyType.Data))
                            {
                                ProcessDataPacket(inputStream, length);
                            }
                            else
                            {
                                SkipPacket(inputStream, length);
                            }
                        }
                        break;

                    case KeyType.Video:
                    case KeyType.Audio:
                    default:
                        SkipPacket(inputStream, length);
                        break;
                }

                // Print progress if enabled
                if (_printProgress && (DateTime.Now - restripeNow).TotalMilliseconds >= 1000 && _function == Function.Restripe)
                {
                    restripeNow = DateTime.Now;
                    var percentComplete = (double)inputStream.Position / inputStream.Length * 100;
                    Console.WriteLine($"Progress: {percentComplete:F2}% complete. Current position: {inputStream.Position} bytes of {inputStream.Length} bytes.");
                }
            }

            // Print total duration if PrintProgress is enabled
            if (_printProgress && _function == Function.Restripe)
            {
                var restripeEnd = DateTime.Now;
                var totalDuration = (restripeEnd - restripeStart).TotalSeconds;
                Console.WriteLine($"Restriping completed in {totalDuration:F2} seconds.");
            }
        }
        finally
        {
            // Close any open extraction streams
            CloseExtractionStreams();
            inputStream.Seek(0, SeekOrigin.Begin);
        }
    }

    /// <summary>
    /// Asynchronously parses the MXF stream and returns an async enumerable of packets with optional filtering.
    /// Supports multiple operation modes including filtering, extraction, and restriping.
    /// </summary>
    /// <param name="inputStream">The stream to read MXF data from</param>
    /// <param name="options">Parsing options including filters and output format</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>An async enumerable of parsed packets matching the filter criteria</returns>
    public async IAsyncEnumerable<Packet> ParseAsync(
        Stream inputStream,
        ParseOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        inputStream.Seek(0, SeekOrigin.Begin);
        _lastTimecode = null;

        var timecode = options.StartTimecode ?? StartTimecode;
        var smpteTimecode = options.StartTimecode ?? StartTimecode;
        var timecodeComponent = options.StartTimecode ?? StartTimecode;

        int lineNumber = 0;
        var restripeStart = DateTime.Now;
        var lastProgressUpdate = DateTime.Now;

        try
        {
            while (await TryReadKlvHeaderAsync(inputStream, cancellationToken) is var (keyType, length) && length >= 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool shouldExtract = _function == Function.Extract && (_demuxMode || _requiredKeys.Contains(keyType));

                if (shouldExtract)
                {
                    await ExtractPacketAsync(inputStream, keyType, length, cancellationToken);
                    continue;
                }

                switch (keyType)
                {
                    case KeyType.TimecodeComponent:
                        if (_function == Function.Restripe)
                        {
                            await RestripeTimecodeComponentAsync(inputStream, length, timecodeComponent, cancellationToken);
                        }
                        else
                        {
                            await SkipPacketAsync(inputStream, length, cancellationToken);
                        }
                        break;

                    case KeyType.System:
                        if (_function == Function.Restripe)
                        {
                            await RestripeSystemPacketAsync(inputStream, length, smpteTimecode, cancellationToken);
                            smpteTimecode = smpteTimecode.GetNext();
                        }
                        else if (ShouldProcessKey(KeyType.System))
                        {
                            await ProcessSystemPacketAsync(inputStream, length, cancellationToken);
                        }
                        else
                        {
                            await SkipPacketAsync(inputStream, length, cancellationToken);
                        }
                        break;

                    case KeyType.Data:
                        if (_function == Function.Filter)
                        {
                            var (packet, updatedLineNumber) = await FilterDataPacketAsync(inputStream, options.Magazine, options.Rows, timecode, lineNumber, options.OutputFormat, cancellationToken);
                            lineNumber = updatedLineNumber;
                            if (_verbose) Console.WriteLine($"Packet found at timecode {timecode}");

                            if (packet.Lines.Count > 0)
                            {
                                yield return packet;
                            }
                            timecode = timecode.GetNext();
                        }
                        else
                        {
                            if (ShouldProcessKey(KeyType.Data))
                            {
                                await ProcessDataPacketAsync(inputStream, length, cancellationToken);
                            }
                            else
                            {
                                await SkipPacketAsync(inputStream, length, cancellationToken);
                            }
                        }
                        break;

                    case KeyType.Video:
                    case KeyType.Audio:
                    default:
                        await SkipPacketAsync(inputStream, length, cancellationToken);
                        break;
                }

                // Progress reporting with throttling
                if (_printProgress && _function == Function.Restripe &&
                    (DateTime.Now - lastProgressUpdate).TotalMilliseconds >= 1000)
                {
                    lastProgressUpdate = DateTime.Now;
                    var percentComplete = (double)inputStream.Position / inputStream.Length * 100;
                    Console.WriteLine($"Progress: {percentComplete:F2}% complete. Current position: {inputStream.Position} bytes of {inputStream.Length} bytes.");
                }
            }

            if (_printProgress && _function == Function.Restripe)
            {
                var restripeEnd = DateTime.Now;
                var totalDuration = (restripeEnd - restripeStart).TotalSeconds;
                Console.WriteLine($"Restriping completed in {totalDuration:F2} seconds.");
            }
        }
        finally
        {
            CloseExtractionStreams();
            inputStream.Seek(0, SeekOrigin.Begin);
        }
    }

    #endregion

    #region KLV Header Reading

    /// <summary>
    /// Attempts to read a KLV (Key-Length-Value) header from the input stream.
    /// </summary>
    /// <param name="input">The input stream to read from</param>
    /// <param name="keyType">Output parameter for the parsed key type</param>
    /// <param name="length">Output parameter for the parsed length</param>
    /// <returns>True if header was successfully read, false otherwise</returns>
    private bool TryReadKlvHeader(Stream input, out KeyType keyType, out int length)
    {
        keyType = KeyType.Unknown;
        length = -1;

        var keyBytesRead = input.Read(_keyBuffer, 0, Constants.KLV_KEY_SIZE);
        if (keyBytesRead != Constants.KLV_KEY_SIZE) return false;

        keyType = Keys.GetKeyType(_keyBuffer.AsSpan(0, Constants.KLV_KEY_SIZE));
        length = ReadBerLength(input, _berLengthBuffer);

        return length >= 0;
    }

    /// <summary>
    /// Asynchronously attempts to read a KLV header from the input stream.
    /// </summary>
    /// <param name="input">The input stream to read from</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A tuple containing the key type and length, or (Unknown, -1) if failed</returns>
    private async Task<(KeyType keyType, int length)> TryReadKlvHeaderAsync(Stream input, CancellationToken cancellationToken)
    {
        var keyMemory = _keyBuffer.AsMemory(0, Constants.KLV_KEY_SIZE);
        var keyBytesRead = await input.ReadAsync(keyMemory, cancellationToken);

        if (keyBytesRead != Constants.KLV_KEY_SIZE)
            return (KeyType.Unknown, -1);

        var keyType = Keys.GetKeyType(_keyBuffer.AsSpan(0, Constants.KLV_KEY_SIZE));
        var length = await ReadBerLengthAsync(input, _berLengthBuffer, cancellationToken);
        return length >= 0 ? (keyType, length) : (KeyType.Unknown, -1);
    }

    #endregion

    #region BER Length Decoding

    /// <summary>
    /// Reads a BER (Basic Encoding Rules) encoded length from the stream.
    /// </summary>
    /// <param name="input">The input stream to read from</param>
    /// <param name="lengthBuffer">Optional buffer to store the length bytes</param>
    /// <returns>The decoded length value, or -1 if failed</returns>
    private static int ReadBerLength(Stream input, List<byte>? lengthBuffer = null)
    {
        lengthBuffer?.Clear();
        var firstByte = input.ReadByte();
        if (firstByte == -1) return -1;

        lengthBuffer?.Add((byte)firstByte);

        if ((firstByte & 0x80) == 0)
        {
            return firstByte;
        }
        else
        {
            var byteCount = firstByte & 0x7F;
            if (byteCount > 8) return -1;

            var length = 0;
            for (var i = 0; i < byteCount; i++)
            {
                var b = input.ReadByte();
                if (b == -1) return -1;
                lengthBuffer?.Add((byte)b);
                length = (length << 8) | b;
            }

            return length;
        }
    }

    /// <summary>
    /// Asynchronously reads a BER encoded length from the stream.
    /// </summary>
    /// <param name="input">The input stream to read from</param>
    /// <param name="lengthBuffer">Buffer to store the length bytes</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>The decoded length value, or -1 if failed</returns>
    private static async Task<int> ReadBerLengthAsync(Stream input, List<byte> lengthBuffer, CancellationToken cancellationToken)
    {
        lengthBuffer.Clear();
        var firstByteBuffer = new byte[1];
        var bytesRead = await input.ReadAsync(firstByteBuffer.AsMemory(), cancellationToken);
        if (bytesRead != 1) return -1;

        var firstByte = firstByteBuffer[0];
        lengthBuffer.Add(firstByte);

        if ((firstByte & 0x80) == 0)
        {
            return firstByte;
        }
        else
        {
            var byteCount = firstByte & 0x7F;
            if (byteCount > 8) return -1;

            var lengthBytes = new byte[byteCount];
            var lengthBytesRead = await input.ReadAsync(lengthBytes.AsMemory(), cancellationToken);
            if (lengthBytesRead != byteCount) return -1;

            lengthBuffer.AddRange(lengthBytes);

            var length = 0;
            for (var i = 0; i < byteCount; i++)
            {
                length = (length << 8) | lengthBytes[i];
            }
            return length;
        }
    }

    #endregion

    #region Packet Processing

    /// <summary>
    /// Processes a System packet to extract SMPTE timecode.
    /// </summary>
    private void ProcessSystemPacket(Stream input, int length)
    {
        var offset = GetSystemMetadataOffset(length);
        if (offset < 0)
        {
            SkipPacket(input, length);
            return;
        }

        #region Myriadbits MXFInspect code
        input.Seek(1, SeekOrigin.Current); // Skip the first byte
        int timebase = StartTimecode.Timebase;
        bool dropFrame = StartTimecode.DropFrame;
        var rate = input.ReadByte();
        int rateIndex = (rate & 0x1E) >> 1;
        int[] rates = [0, 24, 25, 30, 48, 50, 60, 72, 75, 90, 96, 100, 120, 0, 0, 0];
        if (rateIndex < 16)
            timebase = rates[rateIndex];
        if ((rate & 0x01) == 0x01) // 1.001 divider active?
            dropFrame = true;
        input.Seek(-2, SeekOrigin.Current); // Go back to the start of the timecode

        // If StartTimecode.Timebase and StartTimecode.DropFrame do not match what we have found, BREAK
        if (StartTimecode.Timebase != timebase || StartTimecode.DropFrame != dropFrame)
        {
            throw new InvalidOperationException($"Material Package timecode {StartTimecode} does not match existing timebase {timebase} and drop frame {dropFrame}.");
        }
        #endregion

        SkipPacket(input, offset);

        var smpteRead = input.Read(_smpteBuffer, 0, Constants.SMPTE_TIMECODE_SIZE);
        if (smpteRead == Constants.SMPTE_TIMECODE_SIZE)
        {
            var smpte = Timecode.FromBytes(_smpteBuffer, StartTimecode.Timebase, StartTimecode.DropFrame);

            if (_checkSequential && _lastTimecode != null)
            {
                var expectedPrevious = smpte.GetPrevious();
                if (_lastTimecode != expectedPrevious)
                {
                    throw new InvalidDataException($"SMPTE timecodes are not sequential: {_lastTimecode} != {expectedPrevious}");
                }
            }

            SMPTETimecodes.Add(smpte);
            _lastTimecode = smpte;
        }

        var remainingBytes = length - offset - Constants.SMPTE_TIMECODE_SIZE;
        if (remainingBytes > 0)
        {
            SkipPacket(input, remainingBytes);
        }
        else if (remainingBytes < 0)
        {
            throw new InvalidDataException("Invalid length for System Metadata Pack or Set.");
        }
    }

    /// <summary>
    /// Processes a Data packet and adds it to the Packets list.
    /// </summary>
    private void ProcessDataPacket(Stream input, int length)
    {
        Span<byte> header = stackalloc byte[Constants.PACKET_HEADER_SIZE];
        if (input.Read(header) != Constants.PACKET_HEADER_SIZE)
            throw new InvalidOperationException("Failed to read packet header.");

        var data = new byte[length - Constants.PACKET_HEADER_SIZE];
        if (input.Read(data, 0, data.Length) != data.Length)
            throw new InvalidOperationException("Failed to read data for Data key.");

        var packet = new Packet(header.ToArray());
        var lines = Packet.ParseLines(data);
        packet.Lines.AddRange(lines);
        Packets.Add(packet);
    }

    /// <summary>
    /// Filters a Data packet based on magazine and row criteria.
    /// </summary>
    private Packet FilterDataPacket(Stream input, int? magazine, int[]? rows, Timecode startTimecode, int lineNumber = 0, Format outputFormat = Format.T42)
    {
        Span<byte> header = stackalloc byte[Constants.PACKET_HEADER_SIZE];
        Span<byte> lineHeader = stackalloc byte[Constants.LINE_HEADER_SIZE];
        if (input.Read(header) != Constants.PACKET_HEADER_SIZE)
            throw new InvalidOperationException("Failed to read packet header for Data key.");
        var packet = new Packet(header.ToArray())
        {
            Timecode = startTimecode
        };
        for (var l = 0; l < packet.LineCount; l++)
        {
            if (input.Read(lineHeader) != Constants.LINE_HEADER_SIZE)
                throw new InvalidOperationException("Failed to read line header for Data key.");
            var line = new Line(lineHeader)
            {
                LineNumber = lineNumber,
                LineTimecode = startTimecode
            };

            if (line.Length <= 0)
            {
                throw new InvalidDataException("Line length is invalid.");
            }

            line.ParseLine(input, outputFormat);

            // Apply filtering if specified
            if (magazine.HasValue && line.Magazine != magazine.Value)
            {
                lineNumber++;
                continue;
            }

            if (rows != null && !rows.Contains(line.Row))
            {
                lineNumber++;
                continue;
            }

            packet.Lines.Add(line);
            lineNumber++;
        }
        return packet;
    }

    /// <summary>
    /// Skips a packet by advancing the stream position.
    /// </summary>
    private static void SkipPacket(Stream input, int length)
    {
        input.Seek(length, SeekOrigin.Current);
    }

    /// <summary>
    /// Determines if a key type should be processed based on required keys.
    /// </summary>
    private bool ShouldProcessKey(KeyType keyType)
    {
        return _requiredKeys.Contains(keyType);
    }

    #endregion

    #region Async Packet Processing

    /// <summary>
    /// Asynchronously processes a System packet.
    /// </summary>
    private async Task ProcessSystemPacketAsync(Stream input, int length, CancellationToken cancellationToken)
    {
        var arrayPool = ArrayPool<byte>.Shared;
        var buffer = arrayPool.Rent(length);

        try
        {
            var memory = buffer.AsMemory(0, length);
            var bytesRead = await input.ReadAsync(memory, cancellationToken);

            if (bytesRead != length)
                throw new EndOfStreamException($"Expected to read {length} bytes but only read {bytesRead}");

            // Reset position and call sync method
            input.Seek(-length, SeekOrigin.Current);
            ProcessSystemPacket(input, length);
        }
        finally
        {
            arrayPool.Return(buffer);
        }
    }

    /// <summary>
    /// Asynchronously processes a Data packet.
    /// </summary>
    private async Task ProcessDataPacketAsync(Stream input, int length, CancellationToken cancellationToken)
    {
        var arrayPool = ArrayPool<byte>.Shared;
        var buffer = arrayPool.Rent(length);

        try
        {
            var memory = buffer.AsMemory(0, length);
            var bytesRead = await input.ReadAsync(memory, cancellationToken);

            if (bytesRead != length)
                throw new EndOfStreamException($"Expected to read {length} bytes but only read {bytesRead}");

            // Reset position and call sync method
            input.Seek(-length, SeekOrigin.Current);
            ProcessDataPacket(input, length);
        }
        finally
        {
            arrayPool.Return(buffer);
        }
    }

    /// <summary>
    /// Asynchronously filters a Data packet.
    /// </summary>
    private async Task<(Packet packet, int updatedLineNumber)> FilterDataPacketAsync(
        Stream input,
        int? magazine,
        int[]? rows,
        Timecode startTimecode,
        int lineNumber = 0,
        Format outputFormat = Format.T42,
        CancellationToken cancellationToken = default)
    {
        var arrayPool = ArrayPool<byte>.Shared;
        var headerBuffer = arrayPool.Rent(Constants.PACKET_HEADER_SIZE);
        var lineHeaderBuffer = arrayPool.Rent(Constants.LINE_HEADER_SIZE);

        try
        {
            var headerMemory = headerBuffer.AsMemory(0, Constants.PACKET_HEADER_SIZE);
            var headerBytesRead = await input.ReadAsync(headerMemory, cancellationToken);

            if (headerBytesRead != Constants.PACKET_HEADER_SIZE)
                throw new InvalidOperationException("Failed to read packet header for Data key.");

            var packet = new Packet(headerBuffer.AsSpan(0, Constants.PACKET_HEADER_SIZE).ToArray())
            {
                Timecode = startTimecode
            };

            for (var l = 0; l < packet.LineCount; l++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var lineHeaderMemory = lineHeaderBuffer.AsMemory(0, Constants.LINE_HEADER_SIZE);
                var lineHeaderBytesRead = await input.ReadAsync(lineHeaderMemory, cancellationToken);

                if (lineHeaderBytesRead != Constants.LINE_HEADER_SIZE)
                    throw new InvalidOperationException("Failed to read line header for Data key.");

                var line = new Line(lineHeaderBuffer.AsSpan(0, Constants.LINE_HEADER_SIZE))
                {
                    LineNumber = lineNumber,
                    LineTimecode = startTimecode
                };

                if (line.Length <= 0)
                    throw new InvalidDataException("Line length is invalid.");

                await line.ParseLineAsync(input, outputFormat, cancellationToken);

                // Apply filtering
                if (magazine.HasValue && line.Magazine != magazine.Value)
                {
                    lineNumber++;
                    continue;
                }

                if (rows != null && !rows.Contains(line.Row))
                {
                    lineNumber++;
                    continue;
                }

                packet.Lines.Add(line);
                lineNumber++;
            }

            return (packet, lineNumber);
        }
        finally
        {
            arrayPool.Return(headerBuffer);
            arrayPool.Return(lineHeaderBuffer);
        }
    }

    /// <summary>
    /// Asynchronously skips data in the stream.
    /// </summary>
    private static async Task SkipPacketAsync(Stream input, int length, CancellationToken cancellationToken)
    {
        const int bufferSize = 65536; // 64KB buffer for efficient skipping
        var arrayPool = ArrayPool<byte>.Shared;
        var buffer = arrayPool.Rent(Math.Min(bufferSize, length));

        try
        {
            var remaining = length;
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var toRead = Math.Min(remaining, buffer.Length);
                var bytesRead = await input.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);

                if (bytesRead == 0)
                    throw new EndOfStreamException("Unexpected end of stream while skipping data.");

                remaining -= bytesRead;
            }
        }
        finally
        {
            arrayPool.Return(buffer);
        }
    }

    #endregion

    #region Extraction

    /// <summary>
    /// Extracts a packet to the appropriate output stream.
    /// </summary>
    private void ExtractPacket(Stream input, KeyType keyType, int length)
    {
        var outputStream = GetOrCreateExtractionStream(keyType);
        if (outputStream == null)
        {
            SkipPacket(input, length);
            return;
        }

        // Write KLV header if requested
        if (_klvMode)
        {
            WriteKlvHeader(outputStream);
        }

        // Write essence data
        var essenceData = new byte[length];
        var bytesRead = input.Read(essenceData, 0, length);
        if (bytesRead != length) throw new EndOfStreamException("Unexpected end of stream while reading value.");
        outputStream.Write(essenceData, 0, length);
    }

    /// <summary>
    /// Asynchronously extracts packet data to output streams.
    /// </summary>
    private async Task ExtractPacketAsync(Stream input, KeyType keyType, int length, CancellationToken cancellationToken)
    {
        var outputStream = GetOrCreateExtractionStream(keyType);
        if (outputStream == null)
        {
            await SkipPacketAsync(input, length, cancellationToken);
            return;
        }

        if (_klvMode)
        {
            await outputStream.WriteAsync(_keyBuffer.AsMemory(0, Constants.KLV_KEY_SIZE), cancellationToken);
            await outputStream.WriteAsync(_berLengthBuffer.ToArray().AsMemory(), cancellationToken);
        }

        await CopyDataToStreamAsync(input, outputStream, length, cancellationToken);
    }

    /// <summary>
    /// Gets or creates an extraction stream for the specified key type.
    /// </summary>
    private FileStream? GetOrCreateExtractionStream(KeyType keyType)
    {
        if (_demuxMode)
        {
            var keyIdentifier = _useKeyNames ? GetKeyName(_keyBuffer) : BytesToHexString(_keyBuffer);

            if (!_foundKeys.Contains(keyIdentifier))
            {
                Console.WriteLine($"Found key: {keyIdentifier}");
                _foundKeys.Add(keyIdentifier);
            }

            if (!_demuxStreams.TryGetValue(keyIdentifier, out var outputStream))
            {
                var fileExtension = _klvMode ? ".klv" : ".raw";
                outputStream = CreateOutputStream(keyIdentifier, fileExtension);
                _demuxStreams[keyIdentifier] = outputStream;
            }
            return outputStream;
        }
        else
        {
            if (!_outputStreams.TryGetValue(keyType, out var outputStream))
            {
                if (_keyTypeToExtension.TryGetValue(keyType, out var extension))
                {
                    if (_klvMode)
                    {
                        extension = extension.Replace(".raw", ".klv");
                    }
                    var basePath = _outputBasePath ?? Path.ChangeExtension(_inputFilePath, null);
                    outputStream = new FileStream(basePath + extension, FileMode.Create, FileAccess.Write);
                    _outputStreams[keyType] = outputStream;
                }
                else
                {
                    return null;
                }
            }
            return outputStream;
        }
    }

    /// <summary>
    /// Writes the KLV header (key and length) to the output stream.
    /// </summary>
    private void WriteKlvHeader(Stream outputStream)
    {
        outputStream.Write(_keyBuffer, 0, Constants.KLV_KEY_SIZE);
        outputStream.Write([.. _berLengthBuffer], 0, _berLengthBuffer.Count);
    }

    /// <summary>
    /// Copies data from input to output stream.
    /// </summary>
    private static async Task CopyDataToStreamAsync(Stream input, Stream outputStream, int length, CancellationToken cancellationToken)
    {
        const int bufferSize = 65536; // 64KB buffer
        var arrayPool = ArrayPool<byte>.Shared;
        var buffer = arrayPool.Rent(Math.Min(length, bufferSize));

        try
        {
            var remaining = length;
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var toRead = Math.Min(remaining, buffer.Length);
                var bytesRead = await input.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);

                if (bytesRead == 0)
                    throw new EndOfStreamException("Unexpected end of stream while copying data.");

                await outputStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                remaining -= bytesRead;
            }
        }
        finally
        {
            arrayPool.Return(buffer);
        }
    }

    /// <summary>
    /// Closes all extraction streams.
    /// </summary>
    private void CloseExtractionStreams()
    {
        foreach (var stream in _outputStreams.Values)
        {
            stream?.Dispose();
        }
        _outputStreams.Clear();

        foreach (var stream in _demuxStreams.Values)
        {
            stream?.Dispose();
        }
        _demuxStreams.Clear();
    }

    /// <summary>
    /// Creates an output stream for the specified identifier and extension.
    /// </summary>
    private FileStream CreateOutputStream(string identifier, string extension)
    {
        var outputPath = $"{_outputBasePath ?? Path.ChangeExtension(_inputFilePath, null)}_{identifier}{extension}";
        return new FileStream(outputPath, FileMode.Create, FileAccess.Write);
    }

    #endregion

    #region Restriping

    /// <summary>
    /// Restripes a TimecodeComponent packet with a new timecode.
    /// </summary>
    private void RestripeTimecodeComponent(Stream input, int length, Timecode newTimecode)
    {
        var dataStartPosition = input.Position;
        var data = new byte[length];
        var actualRead = input.Read(data, 0, length);
        if (actualRead != length)
            throw new EndOfStreamException($"Expected to read {length} bytes but only read {actualRead}");

        // Parse the TimecodeComponent to get the current start timecode
        var timecodeComponent = TimecodeComponent.Parse(data);
        var currentTimecode = new Timecode(timecodeComponent.StartTimecode, timecodeComponent.RoundedTimecodeTimebase, timecodeComponent.DropFrame);

        if (_verbose)
        {
            Console.WriteLine($"Restriping TimecodeComponent: {currentTimecode} -> {newTimecode}");
        }

        // Find and update timecode-related tags
        var t = 0;
        while (t < data.Length)
        {
            if (t + 6 > data.Length) break;

            var tagBytes = data[t..(t + 2)];
            if (BitConverter.IsLittleEndian) Array.Reverse(tagBytes);
            var tag = BitConverter.ToUInt16(tagBytes, 0);

            var tagLengthBytes = data[(t + 2)..(t + 4)];
            if (BitConverter.IsLittleEndian) Array.Reverse(tagLengthBytes);
            var tagLength = BitConverter.ToUInt16(tagLengthBytes, 0);

            byte[]? newValueBytes = tag switch
            {
                0x1501 => CreateTagValueBytes(newTimecode.FrameNumber, tagLength), // "Start Timecode"
                0x1502 => CreateTagValueBytes(newTimecode.Timebase, tagLength),   // "Rounded Timecode Timebase"
                0x1503 => CreateTagValueBytes(newTimecode.DropFrame ? 1 : 0, tagLength), // "Drop Frame"
                _ => null
            };

            if (newValueBytes != null)
            {
                if (BitConverter.IsLittleEndian) Array.Reverse(newValueBytes);
                Array.Copy(newValueBytes, 0, data, t + 4, Math.Min(newValueBytes.Length, tagLength));
            }

            t += 4 + tagLength;
        }

        // Write the modified data back to the input file
        input.Seek(dataStartPosition, SeekOrigin.Begin);
        input.Write(data, 0, length);
    }

    /// <summary>
    /// Restripes a System packet with a new SMPTE timecode.
    /// </summary>
    private void RestripeSystemPacket(Stream input, int length, Timecode newTimecode)
    {
        var offset = GetSystemMetadataOffset(length);
        if (offset < 0)
        {
            SkipPacket(input, length);
            return;
        }

        #region Myriadbits MXFInspect code
        input.Seek(1, SeekOrigin.Current); // Skip the first byte
        int timebase = StartTimecode.Timebase;
        bool dropFrame = StartTimecode.DropFrame;
        var rate = input.ReadByte();
        int rateIndex = (rate & 0x1E) >> 1;
        int[] rates = [0, 24, 25, 30, 48, 50, 60, 72, 75, 90, 96, 100, 120, 0, 0, 0];
        if (rateIndex < 16)
            timebase = rates[rateIndex];
        if ((rate & 0x01) == 0x01) // 1.001 divider active?
            dropFrame = true;
        input.Seek(-2, SeekOrigin.Current); // Go back to the start of the timecode

        // If newTimecode.Timebase and newTimecode.DropFrame do not match what we have found, BREAK
        if (newTimecode.Timebase != timebase || newTimecode.DropFrame != dropFrame)
        {
            throw new InvalidOperationException($"New timecode {newTimecode} does not match existing timebase {timebase} and drop frame {dropFrame}.");
        }
        #endregion

        SkipPacket(input, offset);
        var timecodePosition = input.Position;

        var smpteRead = input.Read(_smpteBuffer, 0, Constants.SMPTE_TIMECODE_SIZE);
        if (smpteRead == Constants.SMPTE_TIMECODE_SIZE)
        {
            if (_verbose)
            {
                var currentTimecode = Timecode.FromBytes(_smpteBuffer, timebase, dropFrame);
                Console.WriteLine($"Restriping System timecode at offset {offset}: {currentTimecode} -> {newTimecode}");
            }

            // Convert new timecode to bytes and write back to file
            var newTimecodeBytes = newTimecode.ToBytes();
            input.Seek(timecodePosition, SeekOrigin.Begin);
            input.Write(newTimecodeBytes, 0, Constants.SMPTE_TIMECODE_SIZE);
        }

        var remainingBytes = length - offset - Constants.SMPTE_TIMECODE_SIZE;
        if (remainingBytes > 0)
        {
            SkipPacket(input, remainingBytes);
        }
    }

    /// <summary>
    /// Asynchronously restripes a TimecodeComponent packet.
    /// </summary>
    private async Task RestripeTimecodeComponentAsync(Stream input, int length, Timecode newTimecode, CancellationToken cancellationToken)
    {
        var arrayPool = ArrayPool<byte>.Shared;
        var dataBuffer = arrayPool.Rent(length);

        try
        {
            var dataMemory = dataBuffer.AsMemory(0, length);
            var dataStartPosition = input.Position;
            var actualRead = await input.ReadAsync(dataMemory, cancellationToken);

            if (actualRead != length)
                throw new EndOfStreamException($"Expected to read {length} bytes but only read {actualRead}");

            // Parse the TimecodeComponent to get the current start timecode
            var data = dataBuffer.AsSpan(0, length);
            var timecodeComponent = TimecodeComponent.Parse(data.ToArray());
            var currentTimecode = new Timecode(timecodeComponent.StartTimecode, timecodeComponent.RoundedTimecodeTimebase, timecodeComponent.DropFrame);

            if (_verbose)
            {
                Console.WriteLine($"Restriping TimecodeComponent: {currentTimecode} -> {newTimecode}");
            }

            // Find and update timecode-related tags
            var t = 0;
            while (t < data.Length)
            {
                if (t + 6 > data.Length) break;

                var tagBytes = data.Slice(t, 2);
                var tagBytesArray = tagBytes.ToArray();
                if (BitConverter.IsLittleEndian) Array.Reverse(tagBytesArray);
                var tag = BitConverter.ToUInt16(tagBytesArray, 0);

                var tagLengthBytes = data.Slice(t + 2, 2);
                var tagLengthBytesArray = tagLengthBytes.ToArray();
                if (BitConverter.IsLittleEndian) Array.Reverse(tagLengthBytesArray);
                var tagLength = BitConverter.ToUInt16(tagLengthBytesArray, 0);

                byte[]? newValueBytes = tag switch
                {
                    0x1501 => CreateTagValueBytes(newTimecode.FrameNumber, tagLength), // "Start Timecode"
                    0x1502 => CreateTagValueBytes(newTimecode.Timebase, tagLength),   // "Rounded Timecode Timebase"
                    0x1503 => CreateTagValueBytes(newTimecode.DropFrame ? 1 : 0, tagLength), // "Drop Frame"
                    _ => null
                };

                if (newValueBytes != null)
                {
                    if (BitConverter.IsLittleEndian) Array.Reverse(newValueBytes);
                    var targetSlice = data.Slice(t + 4, Math.Min(newValueBytes.Length, tagLength));
                    newValueBytes.AsSpan(0, targetSlice.Length).CopyTo(targetSlice);
                }

                t += 4 + tagLength;
            }

            // Write the modified data back to the input file
            input.Seek(dataStartPosition, SeekOrigin.Begin);
            await input.WriteAsync(dataMemory, cancellationToken);
        }
        finally
        {
            arrayPool.Return(dataBuffer);
        }
    }

    /// <summary>
    /// Asynchronously restripes a System packet.
    /// </summary>
    private async Task RestripeSystemPacketAsync(Stream input, int length, Timecode newTimecode, CancellationToken cancellationToken)
    {
        var offset = GetSystemMetadataOffset(length);
        if (offset < 0)
        {
            await SkipPacketAsync(input, length, cancellationToken);
            return;
        }

        #region Myriadbits MXFInspect code
        input.Seek(1, SeekOrigin.Current); // Skip the first byte
        int timebase = StartTimecode.Timebase;
        bool dropFrame = StartTimecode.DropFrame;
        var rateBuffer = new byte[1];
        var rateBytesRead = await input.ReadAsync(rateBuffer.AsMemory(), cancellationToken);
        if (rateBytesRead != 1)
            throw new EndOfStreamException("Unexpected end of stream while reading rate byte.");
        var rate = rateBuffer[0];
        int rateIndex = (rate & 0x1E) >> 1;
        int[] rates = [0, 24, 25, 30, 48, 50, 60, 72, 75, 90, 96, 100, 120, 0, 0, 0];
        if (rateIndex < 16)
            timebase = rates[rateIndex];
        if ((rate & 0x01) == 0x01) // 1.001 divider active?
            dropFrame = true;
        input.Seek(-2, SeekOrigin.Current); // Go back to the start of the timecode

        // If newTimecode.Timebase and newTimecode.DropFrame do not match what we have found, BREAK
        if (newTimecode.Timebase != timebase || newTimecode.DropFrame != dropFrame)
        {
            throw new InvalidOperationException($"New timecode {newTimecode} does not match existing timebase {timebase} and drop frame {dropFrame}.");
        }
        #endregion

        await SkipPacketAsync(input, offset, cancellationToken);
        var timecodePosition = input.Position;

        var smpteMemory = _smpteBuffer.AsMemory(0, Constants.SMPTE_TIMECODE_SIZE);
        var smpteRead = await input.ReadAsync(smpteMemory, cancellationToken);
        if (smpteRead == Constants.SMPTE_TIMECODE_SIZE)
        {
            if (_verbose)
            {
                var currentTimecode = Timecode.FromBytes(_smpteBuffer, timebase, dropFrame);
                Console.WriteLine($"Restriping System timecode at offset {offset}: {currentTimecode} -> {newTimecode}");
            }

            // Convert new timecode to bytes and write back to file
            var newTimecodeBytes = newTimecode.ToBytes();
            input.Seek(timecodePosition, SeekOrigin.Begin);
            await input.WriteAsync(newTimecodeBytes.AsMemory(0, Constants.SMPTE_TIMECODE_SIZE), cancellationToken);
        }

        var remainingBytes = length - offset - Constants.SMPTE_TIMECODE_SIZE;
        if (remainingBytes > 0)
        {
            await SkipPacketAsync(input, remainingBytes, cancellationToken);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Determines the offset to the SMPTE timecode in System metadata.
    /// </summary>
    private static int GetSystemMetadataOffset(int length)
    {
        if (length >= Constants.SYSTEM_METADATA_PACK_GC + Constants.SMPTE_TIMECODE_SIZE)
        {
            return Constants.SYSTEM_METADATA_PACK_GC;
        }
        else if (length >= Constants.SYSTEM_METADATA_SET_GC_OFFSET + Constants.SMPTE_TIMECODE_SIZE)
        {
            return Constants.SYSTEM_METADATA_SET_GC_OFFSET;
        }
        return -1;
    }

    /// <summary>
    /// Converts a byte array to a lowercase hexadecimal string.
    /// </summary>
    private static string BytesToHexString(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Gets the friendly name for a key from the Keys or Essence classes.
    /// </summary>
    private static string GetKeyName(byte[] keyBytes)
    {
        Type[] typesToSearch = [typeof(Essence), typeof(Keys)];

        foreach (var type in typesToSearch)
        {
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            foreach (var field in fields.Where(f => f.FieldType == typeof(byte[]) && f.Name != "FourCc"))
            {
                var fieldValue = (byte[])field.GetValue(null)!;

                if (fieldValue.Length <= keyBytes.Length)
                {
                    var keyPrefix = keyBytes.Take(fieldValue.Length).ToArray();
                    if (keyPrefix.SequenceEqual(fieldValue))
                    {
                        return field.Name.TrimStart('_');
                    }
                }
            }
        }

        var keyType = Keys.GetKeyType(keyBytes.AsSpan());
        if (keyType != KeyType.Unknown)
        {
            return keyType.ToString();
        }

        return BytesToHexString(keyBytes);
    }

    /// <summary>
    /// Creates a byte array for a tag value with the specified length.
    /// </summary>
    private static byte[] CreateTagValueBytes(long value, int tagLength)
    {
        return tagLength switch
        {
            8 => BitConverter.GetBytes(value),
            4 => BitConverter.GetBytes((int)value),
            2 => BitConverter.GetBytes((short)value),
            1 => [(byte)value],
            _ => BitConverter.GetBytes((int)value)
        };
    }

    #endregion
}
