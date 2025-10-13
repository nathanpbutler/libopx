using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.SMPTE;

namespace nathanbutlerDEV.libopx.Formats;

/// <summary>
/// Parser for MXF (Material Exchange Format) files with support for stream extraction, filtering, and timecode restriping.
/// Handles KLV (Key-Length-Value) packets and SMPTE timecode processing.
/// </summary>
public class MXF : IDisposable
{
    /// <summary>
    /// Buffer for storing KLV keys.
    /// </summary>
    private readonly byte[] _keyBuffer = new byte[Constants.KLV_KEY_SIZE];
    /// <summary>
    /// Buffer for storing SMPTE timecodes.
    /// </summary>
    private readonly byte[] _smpteBuffer = new byte[Constants.SMPTE_TIMECODE_SIZE];
    /// <summary>
    /// Gets or sets the input MXF file to be processed.
    /// </summary>
    public required FileInfo InputFile { get; set; }
    /// <summary>
    /// Gets or sets the output file. If null, writes to stdout.
    /// </summary>
    public FileInfo? OutputFile { get; set; } = null;
    /// <summary>
    /// Stream for writing MXF data.
    /// </summary>
    private Stream? _outputStream;
    /// <summary>
    /// Gets or sets the input stream for reading MXF data.
    /// </summary>
    public required Stream Input { get; set; }
    /// <summary>
    /// Gets the output stream for writing processed data.
    /// </summary>
    public Stream Output => _outputStream ??= OutputFile == null ? Console.OpenStandardOutput() : OutputFile.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
    /// <summary>
    /// Gets or sets the start timecode extracted from the MXF file.
    /// </summary>
    public Timecode StartTimecode { get; set; } = new Timecode(0);
    /// <summary>
    /// Gets or sets the list of SMPTE timecodes per-frame in the MXF file.
    /// </summary>
    public List<Timecode> SMPTETimecodes { get; set; } = [];
    /// <summary>
    /// Gets or sets the list of packets parsed from the MXF data stream.
    /// </summary>
    public List<Packet> Packets { get; set; } = [];
    /// <summary>
    /// Gets or sets the list of required key types for filtering during parsing.
    /// </summary>
    public List<KeyType> RequiredKeys { get; set; } = [];
    /// <summary>
    /// Gets or sets whether to validate that SMPTE timecodes are sequential.
    /// </summary>
    public bool CheckSequential { get; set; } = true;
    /// <summary>
    /// Gets or sets the output format for processed data. Default is T42.
    /// </summary>
    public Format? OutputFormat { get; set; } = Format.T42;
    /// <summary>
    /// Gets or sets the function mode for processing. Default is Filter.
    /// </summary>
    public Function Function { get; set; } = Function.Filter;
    /// <summary>
    /// Gets or sets whether to extract all keys found as separate output files.
    /// </summary>
    public bool DemuxMode { get; set; } = false;
    /// <summary>
    /// Gets or sets whether to use Key/Essence names instead of hex keys for output filenames.
    /// </summary>
    public bool UseKeyNames { get; set; } = false;
    /// <summary>
    /// Gets or sets whether to include key and length bytes in output files.
    /// </summary>
    public bool KlvMode { get; set; } = false;
    /// <summary>
    /// Gets or sets the base path for extracted files.
    /// </summary>
    public string? OutputBasePath { get; set; }
    /// <summary>
    /// Gets or sets whether to enable verbose output for debugging.
    /// </summary>
    public bool Verbose { get; set; } = false;
    /// <summary>
    /// Gets or sets whether to print progress updates during parsing.
    /// </summary>
    public bool PrintProgress { get; set; } = false;
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
    
    // Extraction-related fields
    /// <summary>
    /// Mapping of key types to output streams.
    /// </summary>
    private readonly Dictionary<KeyType, FileStream> _outputStreams = [];
    /// <summary>
    /// Mapping of key strings to output streams for demuxing.
    /// </summary>
    private readonly Dictionary<string, FileStream> _demuxStreams = [];
    /// <summary>
    /// Buffer for storing BER length values.
    /// </summary>
    private readonly List<byte> _berLengthBuffer = [];
    /// <summary>
    /// Buffer for storing KLV keys.
    /// </summary>
    private readonly HashSet<string> _foundKeys = [];

    /// <summary>
    /// Initializes a new instance of the MXF parser with the specified input file path.
    /// </summary>
    /// <param name="inputFile">Path to the MXF file to be processed</param>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not a valid MXF file</exception>
    /// <exception cref="InvalidOperationException">Thrown when unable to retrieve start timecode</exception>
    [SetsRequiredMembers]
    public MXF(string inputFile)
    {
        // Initialize input file
        InputFile = new FileInfo(inputFile);

        // If the input file does not exist, throw an exception
        if (!InputFile.Exists)
        {
            throw new FileNotFoundException("The specified MXF file does not exist.", inputFile);
        }

        // Open the input file for reading
        Input = InputFile.OpenRead();

        // If the file is not a valid MXF file, throw an exception
        if (!IsValidMXFFile())
        {
            throw new InvalidDataException("The specified file is not a valid MXF file.");
        }

        // If we cannot retrieve the start timecode, throw an exception
        if (!GetStartTimecode())
        {
            throw new InvalidOperationException("Failed to retrieve start timecode from the MXF file.");
        }

        // Reset stream position to the beginning
        Input.Seek(0, SeekOrigin.Begin);
    }

    /// <summary>
    /// Initializes a new instance of the MXF parser with the specified FileInfo object for read/write operations.
    /// </summary>
    /// <param name="inputFileInfo">FileInfo object representing the MXF file to be processed</param>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not a valid MXF file</exception>
    /// <exception cref="InvalidOperationException">Thrown when unable to retrieve start timecode</exception>
    [SetsRequiredMembers]
    public MXF(FileInfo inputFileInfo)
    {
        // Initialize input file
        InputFile = inputFileInfo;

        // If the input file does not exist, throw an exception
        if (!InputFile.Exists)
        {
            throw new FileNotFoundException("The specified MXF file does not exist.", InputFile.FullName);
        }

        // Open the input file for reading and writing
        Input = new FileStream(InputFile.FullName, FileMode.Open, FileAccess.ReadWrite);

        // If the stream is not a valid MXF file, throw an exception
        if (!IsValidMXFFile())
        {
            throw new InvalidDataException("The specified stream is not a valid MXF file.");
        }

        // If we cannot retrieve the start timecode, throw an exception
        if (!GetStartTimecode())
        {
            throw new InvalidOperationException("Failed to retrieve start timecode from the MXF file.");
        }

        // Reset stream position to the beginning
        Input.Seek(0, SeekOrigin.Begin);
    }

    /// <summary>
    /// Releases all resources used by the MXF parser including streams and file handles.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        CloseExtractionStreams();
        Input?.Close();
        Input?.Dispose();
        _outputStream?.Dispose();
    }

    /// <summary>
    /// Sets the output file for writing
    /// </summary>
    /// <param name="outputFile">Path to the output file</param>
    public void SetOutput(string outputFile)
    {
        // Initialize output file
        OutputFile = new FileInfo(outputFile);
    }
    /// <summary>
    /// Sets the output stream for writing
    /// </summary>
    /// <param name="outputStream">The output stream to write to</param>
    public void SetOutput(Stream outputStream)
    {
        OutputFile = null; // Clear OutputFile since we're using a custom stream
        _outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream), "Output stream cannot be null.");
    }

    /// <summary>
    /// Checks if the file is a valid MXF file by reading the first 4 bytes.
    /// </summary>
    /// <returns>True if the file is a valid MXF file, otherwise false.</returns>
    private bool IsValidMXFFile()
    {
        // Use existing Input stream instead of creating new one
        var currentPosition = Input.Position;
        Input.Seek(0, SeekOrigin.Begin);
        
        Span<byte> header = stackalloc byte[4];
        var bytesRead = Input.Read(header);
        
        Input.Seek(currentPosition, SeekOrigin.Begin); // Restore position
        
        return bytesRead == header.Length && header.SequenceEqual(Keys.FourCc);
    }

    /// <summary>
    /// Adds a key type to the list of required keys for filtering during parsing.
    /// </summary>
    /// <param name="keyType">The key type to add to the required keys list</param>
    public void AddRequiredKey(KeyType keyType)
    {
        if (!RequiredKeys.Contains(keyType))
        {
            RequiredKeys.Add(keyType);
        }
    }

    /// <summary>
    /// Adds a key type to the list of required keys using a string representation.
    /// </summary>
    /// <param name="keyType">String representation of the key type to add</param>
    /// <exception cref="ArgumentException">Thrown when the key type string is invalid</exception>
    public void AddRequiredKey(string keyType)
    {
        if (Enum.TryParse<KeyType>(keyType, true, out var parsedKeyType))
        {
            AddRequiredKey(parsedKeyType);
        }
        else
        {
            throw new ArgumentException($"Invalid key type: {keyType}", nameof(keyType));
        }
    }

    /// <summary>
    /// Clears all required keys from the filtering list.
    /// </summary>
    public void ClearRequiredKeys()
    {
        RequiredKeys.Clear();
    }

    /// <summary>
    /// Removes a specific key type from the required keys list.
    /// </summary>
    /// <param name="keyType">The key type to remove from the required keys list</param>
    public void RemoveRequiredKey(KeyType keyType)
    {
        RequiredKeys.Remove(keyType);
    }

    /// <summary>
    /// Removes a key type from the required keys list using a string representation.
    /// </summary>
    /// <param name="keyType">String representation of the key type to remove</param>
    /// <exception cref="ArgumentException">Thrown when the key type string is invalid</exception>
    public void RemoveRequiredKey(string keyType)
    {
        if (Enum.TryParse<KeyType>(keyType, true, out var parsedKeyType))
        {
            RemoveRequiredKey(parsedKeyType);
        }
        else
        {
            throw new ArgumentException($"Invalid key type: {keyType}", nameof(keyType));
        }
    }

    /// <summary>
    /// Retrieves the start timecode from the MXF file.
    /// </summary>
    /// <returns>True if the start timecode was successfully retrieved, otherwise false.</returns>
    public bool GetStartTimecode()
    {
        Input.Seek(0, SeekOrigin.Begin);

        while (Input.Position < 128000)
        {
            var keyBytesRead = Input.Read(_keyBuffer, 0, Constants.KLV_KEY_SIZE);
            if (keyBytesRead != Constants.KLV_KEY_SIZE) break;

            var keyType = Keys.GetKeyType(_keyBuffer.AsSpan(0, Constants.KLV_KEY_SIZE));

            var length = ReadBerLength(Input);
            if (length < 0) break;

            if (keyType == KeyType.TimecodeComponent)
            {
                var data = new byte[length];
                var dataBytesRead = Input.Read(data, 0, length);
                if (dataBytesRead != length) break;

                var timecodeComponent = TimecodeComponent.Parse(data);
                StartTimecode = new Timecode(timecodeComponent.StartTimecode, timecodeComponent.RoundedTimecodeTimebase, timecodeComponent.DropFrame);
                Input.Seek(0, SeekOrigin.Begin);
                return true;
            }
            else
            {
                Input.Seek(length, SeekOrigin.Current);
            }
        }

        Input.Seek(0, SeekOrigin.Begin);
        StartTimecode = new Timecode(0);
        return false;
    }

    /// <summary>
    /// Parses the MXF file and returns an enumerable of packets with optional filtering.
    /// Supports multiple operation modes including filtering, extraction, and restriping.
    /// </summary>
    /// <param name="magazine">Optional magazine number filter for teletext data (default: all magazines)</param>
    /// <param name="rows">Optional array of row numbers to filter (default: all rows)</param>
    /// <param name="startTimecode">Optional starting timecode override as string (HH:MM:SS:FF format)</param>
    /// <returns>An enumerable of parsed packets matching the filter criteria</returns>
    public IEnumerable<Packet> Parse(int? magazine = null, int[]? rows = null, string? startTimecode = null)
    {
        Input.Seek(0, SeekOrigin.Begin);
        _lastTimecode = null; // Reset sequential checking
        // Increment after each Data packet found
        var timecode = startTimecode == null
            ? StartTimecode
            : new Timecode(startTimecode, StartTimecode.Timebase, StartTimecode.DropFrame);
        // Increment after each System packet found
        var smpteTimecode = startTimecode == null
            ? StartTimecode
            : new Timecode(startTimecode, StartTimecode.Timebase, StartTimecode.DropFrame);
        // Used for TimecodeComponent packets
        var timecodeComponent = startTimecode == null
            ? StartTimecode
            : new Timecode(startTimecode, StartTimecode.Timebase, StartTimecode.DropFrame);

        int lineNumber = 0;

        // Used to update progress (if Verbose is true) at minimum intervals (1000ms)
        var restripeStart = DateTime.Now;
        var restripeNow = DateTime.Now;

        try
        {
            while (TryReadKlvHeader(out var keyType, out var length))
            {

                // Determine if we should extract or restripe this key
                bool shouldExtract = Function == Function.Extract && (DemuxMode || RequiredKeys.Contains(keyType));
                bool shouldRestripe = Function == Function.Restripe;

                if (shouldExtract)
                {
                    ExtractPacket(keyType, length);
                    continue; // Skip to next iteration after extraction
                }

                // Handle non-extraction processing
                switch (keyType)
                {
                    case KeyType.TimecodeComponent:
                        if (Function == Function.Restripe)
                        {
                            //RestripePacket(keyType, length, timecodeComponent);
                            RestripeTimecodeComponent(length, timecodeComponent);
                        }
                        else
                        {
                            SkipPacket(length);
                        }
                        break;
                    case KeyType.System:
                        if (Function == Function.Restripe)
                        {
                            //RestripePacket(keyType, length, smpteTimecode);
                            RestripeSystemPacket(length, smpteTimecode);
                            smpteTimecode = smpteTimecode.GetNext(); // Increment timecode for the next packet
                        }
                        else if (ShouldProcessKey(KeyType.System))
                        {
                            ProcessSystemPacket(length);
                        }
                        else
                        {
                            SkipPacket(length);
                        }
                        break;

                    case KeyType.Data:
                        if (Function == Function.Filter)
                        {
                            var packet = FilterDataPacket(magazine, rows, timecode, lineNumber, OutputFormat ?? Format.T42);
                            if (Verbose) Console.WriteLine($"Packet found at timecode {timecode}");
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
                                ProcessDataPacket(length);
                            }
                            else
                            {
                                SkipPacket(length);
                            }
                        }
                        break;

                    case KeyType.Video:
                    case KeyType.Audio:
                    default:
                        SkipPacket(length);
                        break;

                }

                // If 1000ms have passed since the last progress update, print the current position
                if (PrintProgress && (DateTime.Now - restripeNow).TotalMilliseconds >= 1000 && Function == Function.Restripe)
                {
                    restripeNow = DateTime.Now; // Reset the start time for the next interval
                    var percentComplete = (double)Input.Position / Input.Length * 100;
                    Console.WriteLine($"Progress: {percentComplete:F2}% complete. Current position: {Input.Position} bytes of {Input.Length} bytes.");
                }
            }

            // Print total duration if PrintProgress is enabled
            if (PrintProgress && Function == Function.Restripe)
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
            Input.Seek(0, SeekOrigin.Begin);
        }
    }

    /// <summary>
    /// Asynchronously parses the MXF file and returns an async enumerable of packets with optional filtering.
    /// Supports multiple operation modes including filtering, extraction, and restriping.
    /// </summary>
    /// <param name="magazine">Optional magazine number filter for teletext data (default: all magazines)</param>
    /// <param name="rows">Optional array of row numbers to filter (default: all rows)</param>
    /// <param name="startTimecode">Optional starting timecode override as string (HH:MM:SS:FF format)</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>An async enumerable of parsed packets matching the filter criteria</returns>
    public async IAsyncEnumerable<Packet> ParseAsync(
        int? magazine = null, 
        int[]? rows = null, 
        string? startTimecode = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Input.Seek(0, SeekOrigin.Begin);
        _lastTimecode = null;
        
        var timecode = startTimecode == null
            ? StartTimecode
            : new Timecode(startTimecode, StartTimecode.Timebase, StartTimecode.DropFrame);
        var smpteTimecode = startTimecode == null
            ? StartTimecode
            : new Timecode(startTimecode, StartTimecode.Timebase, StartTimecode.DropFrame);
        var timecodeComponent = startTimecode == null
            ? StartTimecode
            : new Timecode(startTimecode, StartTimecode.Timebase, StartTimecode.DropFrame);

        int lineNumber = 0;
        var restripeStart = DateTime.Now;
        var lastProgressUpdate = DateTime.Now;

        try
        {
            while (await TryReadKlvHeaderAsync(cancellationToken) is var (keyType, length) && length >= 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool shouldExtract = Function == Function.Extract && (DemuxMode || RequiredKeys.Contains(keyType));

                if (shouldExtract)
                {
                    await ExtractPacketAsync(keyType, length, cancellationToken);
                    continue;
                }

                switch (keyType)
                {
                    case KeyType.TimecodeComponent:
                        if (Function == Function.Restripe)
                        {
                            await RestripeTimecodeComponentAsync(length, timecodeComponent, cancellationToken);
                        }
                        else
                        {
                            await SkipPacketAsync(length, cancellationToken);
                        }
                        break;
                        
                    case KeyType.System:
                        if (Function == Function.Restripe)
                        {
                            await RestripeSystemPacketAsync(length, smpteTimecode, cancellationToken);
                            smpteTimecode = smpteTimecode.GetNext();
                        }
                        else if (ShouldProcessKey(KeyType.System))
                        {
                            await ProcessSystemPacketAsync(length, cancellationToken);
                        }
                        else
                        {
                            await SkipPacketAsync(length, cancellationToken);
                        }
                        break;

                    case KeyType.Data:
                        if (Function == Function.Filter)
                        {
                            var (packet, updatedLineNumber) = await FilterDataPacketAsync(magazine, rows, timecode, lineNumber, OutputFormat ?? Format.T42, cancellationToken);
                            lineNumber = updatedLineNumber; // Update lineNumber with the value from FilterDataPacketAsync
                            if (Verbose) Console.WriteLine($"Packet found at timecode {timecode}");
                            
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
                                await ProcessDataPacketAsync(length, cancellationToken);
                            }
                            else
                            {
                                await SkipPacketAsync(length, cancellationToken);
                            }
                        }
                        break;

                    case KeyType.Video:
                    case KeyType.Audio:
                    default:
                        await SkipPacketAsync(length, cancellationToken);
                        break;
                }

                // Progress reporting with throttling
                if (PrintProgress && Function == Function.Restripe && 
                    (DateTime.Now - lastProgressUpdate).TotalMilliseconds >= 1000)
                {
                    lastProgressUpdate = DateTime.Now;
                    var percentComplete = (double)Input.Position / Input.Length * 100;
                    Console.WriteLine($"Progress: {percentComplete:F2}% complete. Current position: {Input.Position} bytes of {Input.Length} bytes.");
                }
            }

            if (PrintProgress && Function == Function.Restripe)
            {
                var restripeEnd = DateTime.Now;
                var totalDuration = (restripeEnd - restripeStart).TotalSeconds;
                Console.WriteLine($"Restriping completed in {totalDuration:F2} seconds.");
            }
        }
        finally
        {
            CloseExtractionStreams();
            Input.Seek(0, SeekOrigin.Begin);
        }
    }

    private void ProcessSystemPacket(int length)
    {
        var offset = GetSystemMetadataOffset(length);
        if (offset < 0)
        {
            SkipPacket(length);
            return;
        }

        #region Myriadbits MXFInspect code
        Input.Seek(1, SeekOrigin.Current); // Skip the first byte
        int timebase = StartTimecode.Timebase;
        bool dropFrame = StartTimecode.DropFrame;
        var rate = Input.ReadByte();
        int rateIndex = (rate & 0x1E) >> 1;
        int[] rates = [0, 24, 25, 30, 48, 50, 60, 72, 75, 90, 96, 100, 120, 0, 0, 0];
        if (rateIndex < 16)
			timebase = rates[rateIndex];
        if ((rate & 0x01) == 0x01) // 1.001 divider active?
            dropFrame = true;
        Input.Seek(-2, SeekOrigin.Current); // Go back to the start of the timecode

        // If StartTimecode.Timebase and StartTimecode.DropFrame do not match what we have found, BREAK
        if (StartTimecode.Timebase != timebase || StartTimecode.DropFrame != dropFrame)
        {
            throw new InvalidOperationException($"Material Package timecode {StartTimecode} does not match existing timebase {timebase} and drop frame {dropFrame}.");
        }

        #endregion

        SkipPacket(offset);

        var smpteRead = Input.Read(_smpteBuffer, 0, Constants.SMPTE_TIMECODE_SIZE);
        if (smpteRead == Constants.SMPTE_TIMECODE_SIZE)
        {
            var smpte = Timecode.FromBytes(_smpteBuffer, StartTimecode.Timebase, StartTimecode.DropFrame);

            if (CheckSequential && _lastTimecode != null)
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
            SkipPacket(remainingBytes);
        }
        else if (remainingBytes < 0)
        {
            throw new InvalidDataException("Invalid length for System Metadata Pack or Set.");
        }
    }

    private void ProcessDataPacket(int length)
    {
        Span<byte> header = stackalloc byte[Constants.PACKET_HEADER_SIZE];
        if (Input.Read(header) != Constants.PACKET_HEADER_SIZE)
            throw new InvalidOperationException("Failed to read packet header.");

        var data = new byte[length - Constants.PACKET_HEADER_SIZE];
        if (Input.Read(data, 0, data.Length) != data.Length)
            throw new InvalidOperationException("Failed to read data for Data key.");

        var packet = new Packet(header.ToArray());
        var lines = Packet.ParseLines(data);
        packet.Lines.AddRange(lines);
        Packets.Add(packet);
    }

    private Packet FilterDataPacket(int? magazine, int[]? rows, Timecode startTimecode, int lineNumber = 0, Format outputFormat = Format.T42)
    {
        Span<byte> header = stackalloc byte[Constants.PACKET_HEADER_SIZE];
        Span<byte> lineHeader = stackalloc byte[Constants.LINE_HEADER_SIZE];
        if (Input.Read(header) != Constants.PACKET_HEADER_SIZE)
            throw new InvalidOperationException("Failed to read packet header for Data key.");
        var packet = new Packet(header.ToArray())
        {
            Timecode = startTimecode
        };
        for (var l = 0; l < packet.LineCount; l++)
        {
            if (Input.Read(lineHeader) != Constants.LINE_HEADER_SIZE)
                throw new InvalidOperationException("Failed to read line header for Data key.");
            var line = new Line(lineHeader)
            {
                LineNumber = lineNumber, // Increment line number for each line
                LineTimecode = startTimecode // Propagate packet timecode to line for RCWT
            };

            if (line.Length <= 0)
            {
                throw new InvalidDataException("Line length is invalid.");
            }

            // Use the more efficient ParseLine method
            line.ParseLine(Input, outputFormat);

            // Apply filtering if specified (only for T42 output format)
            if (magazine.HasValue && line.Magazine != magazine.Value && outputFormat == Format.T42)
            {
                lineNumber++;
                continue; // Skip lines that don't match the magazine filter
            }

            if (rows != null && !rows.Contains(line.Row) && outputFormat == Format.T42)
            {
                lineNumber++;
                continue; // Skip lines that don't match the row filter
            }

            packet.Lines.Add(line);
            lineNumber++; // Increment line number for each line processed
        }
        return packet;
    }

    /// <summary>
    /// Parses and extracts all SMPTE timecodes from the MXF file's System metadata packets.
    /// </summary>
    /// <returns>True if timecodes were successfully parsed, false otherwise</returns>
    public bool ParseSMPTETimecodes()
    {
        Input.Seek(0, SeekOrigin.Begin);
        _lastTimecode = null;

        while (TryReadKlvHeader(out var keyType, out var length))
        {
            if (keyType == KeyType.System)
            {
                ProcessSystemPacket(length);
            }
            else
            {
                SkipPacket(length);
            }
        }

        Input.Seek(0, SeekOrigin.Begin);
        return SMPTETimecodes.Count > 0;
    }

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
    
    private void ExtractPacket(KeyType keyType, int length)
    {
        var outputStream = GetOrCreateExtractionStream(keyType);
        if (outputStream == null)
        {
            SkipPacket(length);
            return;
        }
        
        // Write KLV header if requested
        if (KlvMode)
        {
            WriteKlvHeader(outputStream);
        }
        
        // Write essence data
        var essenceData = new byte[length];
        var bytesRead = Input.Read(essenceData, 0, length);
        if (bytesRead != length) throw new EndOfStreamException("Unexpected end of stream while reading value.");
        outputStream.Write(essenceData, 0, length);
    }
    
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
    
    private static string BytesToHexString(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
    
    private static string GetKeyName(byte[] keyBytes)
    {
        Type[] typesToSearch = [typeof(Essence), typeof(Keys)];
        
        foreach (var type in typesToSearch)
        {
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(byte[]))
                {
                    var fieldValue = (byte[])field.GetValue(null)!;
                    
                    if (field.Name == "FourCc")
                        continue;
                    
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
        }
        
        var keyType = Keys.GetKeyType(keyBytes.AsSpan());
        if (keyType != KeyType.Unknown)
        {
            return keyType.ToString();
        }
        
        return BytesToHexString(keyBytes);
    }

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

    private void RestripeTimecodeComponent(int length, Timecode newTimecode)
    {
        var dataStartPosition = Input.Position;
        var data = new byte[length];
        var actualRead = Input.Read(data, 0, length);
        if (actualRead != length)
            throw new EndOfStreamException($"Expected to read {length} bytes but only read {actualRead}");

        // Parse the TimecodeComponent to get the current start timecode
        var timecodeComponent = TimecodeComponent.Parse(data);
        var currentTimecode = new Timecode(timecodeComponent.StartTimecode, timecodeComponent.RoundedTimecodeTimebase, timecodeComponent.DropFrame);

        if (Verbose)
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
        Input.Seek(dataStartPosition, SeekOrigin.Begin);
        Input.Write(data, 0, length);
    }

    private void RestripeSystemPacket(int length, Timecode newTimecode)
    {
        var offset = GetSystemMetadataOffset(length);
        if (offset < 0)
        {
            SkipPacket(length);
            return;
        }

        #region Myriadbits MXFInspect code
        Input.Seek(1, SeekOrigin.Current); // Skip the first byte
        int timebase = StartTimecode.Timebase;
        bool dropFrame = StartTimecode.DropFrame;
        var rate = Input.ReadByte();
        int rateIndex = (rate & 0x1E) >> 1;
        int[] rates = [0, 24, 25, 30, 48, 50, 60, 72, 75, 90, 96, 100, 120, 0, 0, 0];
        if (rateIndex < 16)
			timebase = rates[rateIndex];
        if ((rate & 0x01) == 0x01) // 1.001 divider active?
            dropFrame = true;
        Input.Seek(-2, SeekOrigin.Current); // Go back to the start of the timecode

        // If newTimecode.Timebase and newTimecode.DropFrame do not match what we have found, BREAK
        if (newTimecode.Timebase != timebase || newTimecode.DropFrame != dropFrame)
        {
            throw new InvalidOperationException($"New timecode {newTimecode} does not match existing timebase {timebase} and drop frame {dropFrame}.");
        }

        #endregion

        SkipPacket(offset);
        var timecodePosition = Input.Position;

        var smpteRead = Input.Read(_smpteBuffer, 0, Constants.SMPTE_TIMECODE_SIZE);
        if (smpteRead == Constants.SMPTE_TIMECODE_SIZE)
        {
            if (Verbose)
            {
                var currentTimecode = Timecode.FromBytes(_smpteBuffer, timebase, dropFrame);
                Console.WriteLine($"Restriping System timecode at offset {offset}: {currentTimecode} -> {newTimecode}");
            }

            // Convert new timecode to bytes and write back to file
            var newTimecodeBytes = newTimecode.ToBytes();
            Input.Seek(timecodePosition, SeekOrigin.Begin);
            Input.Write(newTimecodeBytes, 0, Constants.SMPTE_TIMECODE_SIZE);
        }

        var remainingBytes = length - offset - Constants.SMPTE_TIMECODE_SIZE;
        if (remainingBytes > 0)
        {
            SkipPacket(remainingBytes);
        }
    }
    
    /// <summary>
    /// Extract essence elements from the MXF file based on the configured extraction settings.
    /// </summary>
    /// <param name="outputBasePath">Optional output base path for extracted files</param>
    /// <param name="demuxMode">Extract all keys found to separate files</param>
    /// <param name="useKeyNames">Use Key/Essence names instead of hex keys</param>
    /// <param name="klvMode">Include key and length bytes in output files</param>
    public void ExtractEssence(string? outputBasePath = null, bool? demuxMode = null, bool? useKeyNames = null, bool? klvMode = null)
    {
        // Apply extraction settings
        OutputBasePath = outputBasePath ?? OutputBasePath;
        DemuxMode = demuxMode ?? DemuxMode;
        UseKeyNames = useKeyNames ?? UseKeyNames;
        KlvMode = klvMode ?? KlvMode;
        Function = Function.Extract; // Set function to Extract
        
        // If no specific keys are required and not in demux mode, default to extracting all
        if (!DemuxMode && RequiredKeys.Count == 0)
        {
            RequiredKeys.Add(KeyType.Data);
            RequiredKeys.Add(KeyType.Video);
            RequiredKeys.Add(KeyType.Audio);
            RequiredKeys.Add(KeyType.System);
            RequiredKeys.Add(KeyType.TimecodeComponent);
        }
        
        // Run the parse method which will handle extraction
        var packets = Parse();
        foreach (var _ in packets)
        {
            // The Parse method handles all the extraction internally
            // We just need to iterate through to execute it
        }
    }
    
    private bool TryReadKlvHeader(out KeyType keyType, out int length)
    {
        keyType = KeyType.Unknown;
        length = -1;
        
        var keyBytesRead = Input.Read(_keyBuffer, 0, Constants.KLV_KEY_SIZE);
        if (keyBytesRead != Constants.KLV_KEY_SIZE) return false;
        
        keyType = Keys.GetKeyType(_keyBuffer.AsSpan(0, Constants.KLV_KEY_SIZE));
        length = ReadBerLength(Input, _berLengthBuffer);
        
        return length >= 0;
    }
    
    private void SkipPacket(int length)
    {
        Input.Seek(length, SeekOrigin.Current);
    }
    
    private bool ShouldProcessKey(KeyType keyType)
    {
        return RequiredKeys.Contains(keyType);
    }
    
    private FileStream CreateOutputStream(string identifier, string extension)
    {
        var outputPath = $"{OutputBasePath ?? Path.ChangeExtension(InputFile.FullName, null)}_{identifier}{extension}";
        return new FileStream(outputPath, FileMode.Create, FileAccess.Write);
    }
    
    private void WriteKlvHeader(Stream outputStream)
    {
        outputStream.Write(_keyBuffer, 0, Constants.KLV_KEY_SIZE);
        outputStream.Write([.. _berLengthBuffer], 0, _berLengthBuffer.Count);
    }
    
    private void CopyDataToStream(Stream outputStream, int length)
    {
        var buffer = new byte[Math.Min(length, 65536)];
        var remaining = length;
        while (remaining > 0)
        {
            var toRead = Math.Min(remaining, buffer.Length);
            var bytesRead = Input.Read(buffer, 0, toRead);
            if (bytesRead == 0)
                break;
            outputStream.Write(buffer, 0, bytesRead);
            remaining -= bytesRead;
        }
    }
    
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
    /// Asynchronously attempts to read a KLV header from the input stream
    /// </summary>
    private async Task<(KeyType keyType, int length)> TryReadKlvHeaderAsync(CancellationToken cancellationToken)
    {
        var keyMemory = _keyBuffer.AsMemory(0, Constants.KLV_KEY_SIZE);
        var keyBytesRead = await Input.ReadAsync(keyMemory, cancellationToken);
        
        if (keyBytesRead != Constants.KLV_KEY_SIZE)
            return (KeyType.Unknown, -1);

        var keyType = Keys.GetKeyType(_keyBuffer.AsSpan(0, Constants.KLV_KEY_SIZE));
        var length = await ReadBerLengthAsync(Input, _berLengthBuffer, cancellationToken);
        return length >= 0 ? (keyType, length) : (KeyType.Unknown, -1);
    }

    /// <summary>
    /// Asynchronously reads BER encoded length
    /// </summary>
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

    /// <summary>
    /// Asynchronously skips data in the stream
    /// </summary>
    private async Task SkipPacketAsync(int length, CancellationToken cancellationToken)
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
                var bytesRead = await Input.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);
                
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

    /// <summary>
    /// Asynchronously extracts packet data to output streams
    /// </summary>
    private async Task ExtractPacketAsync(KeyType keyType, int length, CancellationToken cancellationToken)
    {
        var outputStream = GetOrCreateExtractionStream(keyType);
        if (outputStream == null)
        {
            await SkipPacketAsync(length, cancellationToken);
            return;
        }
        
        if (KlvMode)
        {
            await outputStream.WriteAsync(_keyBuffer.AsMemory(0, Constants.KLV_KEY_SIZE), cancellationToken);
            await outputStream.WriteAsync(_berLengthBuffer.ToArray().AsMemory(), cancellationToken);
        }
        
        await CopyDataToStreamAsync(outputStream, length, cancellationToken);
    }

    /// <summary>
    /// Asynchronously copies data from input to output stream
    /// </summary>
    private async Task CopyDataToStreamAsync(Stream outputStream, int length, CancellationToken cancellationToken)
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
                var bytesRead = await Input.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);
                
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
    /// Asynchronously processes data packets for filtering
    /// </summary>
    private async Task<(Packet packet, int updatedLineNumber)> FilterDataPacketAsync(int? magazine, int[]? rows, Timecode startTimecode, int lineNumber = 0, Format outputFormat = Format.T42, CancellationToken cancellationToken = default)
    {
        var arrayPool = ArrayPool<byte>.Shared;
        var headerBuffer = arrayPool.Rent(Constants.PACKET_HEADER_SIZE);
        var lineHeaderBuffer = arrayPool.Rent(Constants.LINE_HEADER_SIZE);
        
        try
        {
            var headerMemory = headerBuffer.AsMemory(0, Constants.PACKET_HEADER_SIZE);
            var headerBytesRead = await Input.ReadAsync(headerMemory, cancellationToken);
            
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
                var lineHeaderBytesRead = await Input.ReadAsync(lineHeaderMemory, cancellationToken);
                
                if (lineHeaderBytesRead != Constants.LINE_HEADER_SIZE)
                    throw new InvalidOperationException("Failed to read line header for Data key.");

                var line = new Line(lineHeaderBuffer.AsSpan(0, Constants.LINE_HEADER_SIZE))
                {
                    LineNumber = lineNumber,
                    LineTimecode = startTimecode
                };

                if (line.Length <= 0)
                    throw new InvalidDataException("Line length is invalid.");

                await line.ParseLineAsync(Input, outputFormat, cancellationToken);

                // Apply filtering
                if (magazine.HasValue && line.Magazine != magazine.Value && outputFormat == Format.T42)
                {
                    lineNumber++;
                    continue;
                }

                if (rows != null && !rows.Contains(line.Row) && outputFormat == Format.T42)
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
    /// Asynchronously restripes TimecodeComponent packets
    /// </summary>
    private async Task RestripeTimecodeComponentAsync(int length, Timecode newTimecode, CancellationToken cancellationToken)
    {
        var arrayPool = ArrayPool<byte>.Shared;
        var dataBuffer = arrayPool.Rent(length);
        
        try
        {
            var dataMemory = dataBuffer.AsMemory(0, length);
            var dataStartPosition = Input.Position;
            var actualRead = await Input.ReadAsync(dataMemory, cancellationToken);
            
            if (actualRead != length)
                throw new EndOfStreamException($"Expected to read {length} bytes but only read {actualRead}");

            // Parse the TimecodeComponent to get the current start timecode
            var data = dataBuffer.AsSpan(0, length);
            var timecodeComponent = TimecodeComponent.Parse(data.ToArray());
            var currentTimecode = new Timecode(timecodeComponent.StartTimecode, timecodeComponent.RoundedTimecodeTimebase, timecodeComponent.DropFrame);

            if (Verbose)
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
            Input.Seek(dataStartPosition, SeekOrigin.Begin);
            await Input.WriteAsync(dataMemory, cancellationToken);
        }
        finally
        {
            arrayPool.Return(dataBuffer);
        }
    }

    /// <summary>
    /// Asynchronously restripes System packets
    /// </summary>
    private async Task RestripeSystemPacketAsync(int length, Timecode newTimecode, CancellationToken cancellationToken)
    {
        var offset = GetSystemMetadataOffset(length);
        if (offset < 0)
        {
            await SkipPacketAsync(length, cancellationToken);
            return;
        }

        #region Myriadbits MXFInspect code
        Input.Seek(1, SeekOrigin.Current); // Skip the first byte
        int timebase = StartTimecode.Timebase;
        bool dropFrame = StartTimecode.DropFrame;
        var rateBuffer = new byte[1];
        var rateBytesRead = await Input.ReadAsync(rateBuffer.AsMemory(), cancellationToken);
        if (rateBytesRead != 1)
            throw new EndOfStreamException("Unexpected end of stream while reading rate byte.");
        var rate = rateBuffer[0];
        int rateIndex = (rate & 0x1E) >> 1;
        int[] rates = [0, 24, 25, 30, 48, 50, 60, 72, 75, 90, 96, 100, 120, 0, 0, 0];
        if (rateIndex < 16)
			timebase = rates[rateIndex];
        if ((rate & 0x01) == 0x01) // 1.001 divider active?
            dropFrame = true;
        Input.Seek(-2, SeekOrigin.Current); // Go back to the start of the timecode

        // If newTimecode.Timebase and newTimecode.DropFrame do not match what we have found, BREAK
        if (newTimecode.Timebase != timebase || newTimecode.DropFrame != dropFrame)
        {
            throw new InvalidOperationException($"New timecode {newTimecode} does not match existing timebase {timebase} and drop frame {dropFrame}.");
        }

        #endregion

        await SkipPacketAsync(offset, cancellationToken);
        var timecodePosition = Input.Position;

        var smpteMemory = _smpteBuffer.AsMemory(0, Constants.SMPTE_TIMECODE_SIZE);
        var smpteRead = await Input.ReadAsync(smpteMemory, cancellationToken);
        if (smpteRead == Constants.SMPTE_TIMECODE_SIZE)
        {
            if (Verbose)
            {
                var currentTimecode = Timecode.FromBytes(_smpteBuffer, timebase, dropFrame);
                Console.WriteLine($"Restriping System timecode at offset {offset}: {currentTimecode} -> {newTimecode}");
            }

            // Convert new timecode to bytes and write back to file
            var newTimecodeBytes = newTimecode.ToBytes();
            Input.Seek(timecodePosition, SeekOrigin.Begin);
            await Input.WriteAsync(newTimecodeBytes.AsMemory(0, Constants.SMPTE_TIMECODE_SIZE), cancellationToken);
        }

        var remainingBytes = length - offset - Constants.SMPTE_TIMECODE_SIZE;
        if (remainingBytes > 0)
        {
            await SkipPacketAsync(remainingBytes, cancellationToken);
        }
    }

    /// <summary>
    /// Asynchronously processes System packets
    /// </summary>
    private async Task ProcessSystemPacketAsync(int length, CancellationToken cancellationToken)
    {
        // For now, delegate to sync version by reading data and calling sync method
        var arrayPool = ArrayPool<byte>.Shared;
        var buffer = arrayPool.Rent(length);
        
        try
        {
            var memory = buffer.AsMemory(0, length);
            var bytesRead = await Input.ReadAsync(memory, cancellationToken);
            
            if (bytesRead != length)
                throw new EndOfStreamException($"Expected to read {length} bytes but only read {bytesRead}");

            // Reset position and call sync method
            Input.Seek(-length, SeekOrigin.Current);
            ProcessSystemPacket(length);
        }
        finally
        {
            arrayPool.Return(buffer);
        }
    }

    /// <summary>
    /// Asynchronously processes Data packets
    /// </summary>
    private async Task ProcessDataPacketAsync(int length, CancellationToken cancellationToken)
    {
        // For now, delegate to sync version by reading data and calling sync method
        var arrayPool = ArrayPool<byte>.Shared;
        var buffer = arrayPool.Rent(length);
        
        try
        {
            var memory = buffer.AsMemory(0, length);
            var bytesRead = await Input.ReadAsync(memory, cancellationToken);
            
            if (bytesRead != length)
                throw new EndOfStreamException($"Expected to read {length} bytes but only read {bytesRead}");

            // Reset position and call sync method
            Input.Seek(-length, SeekOrigin.Current);
            ProcessDataPacket(length);
        }
        finally
        {
            arrayPool.Return(buffer);
        }
    }
    
    private FileStream? GetOrCreateExtractionStream(KeyType keyType)
    {
        if (DemuxMode)
        {
            var keyIdentifier = UseKeyNames ? GetKeyName(_keyBuffer) : BytesToHexString(_keyBuffer);
            
            if (!_foundKeys.Contains(keyIdentifier))
            {
                Console.WriteLine($"Found key: {keyIdentifier}");
                _foundKeys.Add(keyIdentifier);
            }
            
            if (!_demuxStreams.TryGetValue(keyIdentifier, out var outputStream))
            {
                var fileExtension = KlvMode ? ".klv" : ".raw";
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
                    if (KlvMode)
                    {
                        extension = extension.Replace(".raw", ".klv");
                    }
                    var basePath = OutputBasePath ?? Path.ChangeExtension(InputFile.FullName, null);
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
    /// Parser for extracted MXF data stream files containing teletext data packets with line headers.
    /// Supports streaming parsing with magazine and row filtering capabilities.
    /// </summary>
    /// <remarks>
    /// MXFData represents extracted data streams from MXF files (typically saved with *.bin extension).
    /// It is not a standalone file format, but rather the data essence extracted from MXF containers.
    /// </remarks>
    public class MXFData : IDisposable
    {
        private readonly byte[] _packetHeader = new byte[Constants.PACKET_HEADER_SIZE];
        private readonly byte[] _lineHeader = new byte[Constants.LINE_HEADER_SIZE];
        /// <summary>
        /// Gets or sets the input file. If null, reads from stdin.
        /// </summary>
        public FileInfo? InputFile { get; set; } = null;
        /// <summary>
        /// Gets or sets the output file. If null, writes to stdout.
        /// </summary>
        public FileInfo? OutputFile { get; set; } = null;
        private Stream? _outputStream;
        /// <summary>
        /// Gets or sets the input stream for reading extracted MXF data.
        /// </summary>
        public required Stream Input { get; set; }
        /// <summary>
        /// Gets the output stream for writing processed data.
        /// </summary>
        public Stream Output => _outputStream ??= OutputFile == null ? Console.OpenStandardOutput() : OutputFile.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
        // TODO: Change Parse() to output Packets instead of storing them in the MXFData object
        /// <summary>
        /// Gets or sets the list of packets in the extracted MXF data. Use Parse() method instead.
        /// </summary>
        [Obsolete("Use Parse() method which returns IEnumerable<Packet> instead")]
        public List<Packet> Packets { get; set; } = [];
        /// <summary>
        /// Gets or sets the output format for processed data. Default is T42.
        /// </summary>
        public Format? OutputFormat { get; set; } = Format.T42;
        // TODO: Implement Extract and Filter functions
        /// <summary>
        /// Gets or sets the function mode for processing. Default is Filter.
        /// </summary>
        public Function Function { get; set; } = Function.Filter;

        /// <summary>
        /// Constructor for extracted MXF data from file
        /// </summary>
        /// <param name="inputFile">Path to the input file (typically *.bin)</param>
        /// <exception cref="FileNotFoundException">Thrown if the specified file does not exist</exception>
        [SetsRequiredMembers]
        public MXFData(string inputFile)
        {
            InputFile = new FileInfo(inputFile);

            if (!InputFile.Exists)
            {
                throw new FileNotFoundException("The specified file does not exist.", inputFile);
            }

            Input = InputFile.OpenRead();
            Input.Seek(0, SeekOrigin.Begin); // Reset stream position to the beginning
        }

        /// <summary>
        /// Constructor for extracted MXF data from stdin
        /// </summary>
        [SetsRequiredMembers]
        public MXFData()
        {
            InputFile = null;
            Input = Console.OpenStandardInput();
        }

        /// <summary>
        /// Constructor for extracted MXF data with custom stream
        /// </summary>
        /// <param name="inputStream">The input stream to read from</param>
        [SetsRequiredMembers]
        public MXFData(Stream inputStream)
        {
            InputFile = null;
            Input = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
        }

        /// <summary>
        /// Sets the output file for writing
        /// </summary>
        /// <param name="outputFile">Path to the output file</param>
        public void SetOutput(string outputFile)
        {
            OutputFile = new FileInfo(outputFile);
        }

        /// <summary>
        /// Sets the output stream for writing
        /// </summary>
        /// <param name="outputStream">The output stream to write to</param>
        public void SetOutput(Stream outputStream)
        {
            OutputFile = null; // Clear OutputFile since we're using a custom stream
            _outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream), "Output stream cannot be null.");
        }

        /// <summary>
        /// Parses the extracted MXF data and returns an enumerable of packets with optional filtering.
        /// </summary>
        /// <param name="magazine">Optional magazine number filter (default: all magazines)</param>
        /// <param name="rows">Optional array of row numbers to filter (default: all rows)</param>
        /// <param name="startTimecode">Optional starting timecode for packet numbering</param>
        /// <returns>An enumerable of parsed packets matching the filter criteria</returns>
        public IEnumerable<Packet> Parse(int? magazine = null, int[]? rows = null, Timecode? startTimecode = null)
        {
            // Use default rows if not specified
            rows ??= Constants.DEFAULT_ROWS;

            // If OutputFormat is not set, use the provided outputFormat
            var outputFormat = OutputFormat ?? Format.T42;

            var lineNumber = 0;

            var timecode = startTimecode ?? new Timecode(0); // Default timecode, can be modified later

            while (Input.Read(_packetHeader, 0, Constants.PACKET_HEADER_SIZE) == Constants.PACKET_HEADER_SIZE)
            {
                var packet = new Packet(_packetHeader)
                {
                    Timecode = timecode // Set the timecode for the packet
                };

                for (var l = 0; l < packet.LineCount; l++)
                {
                    if (Input.Read(_lineHeader, 0, Constants.LINE_HEADER_SIZE) < Constants.LINE_HEADER_SIZE) break;
                    var line = new Line(_lineHeader)
                    {
                        LineNumber = lineNumber, // Increment line number for each line
                        LineTimecode = timecode  // Propagate packet timecode to line for RCWT
                    };

                    if (line.Length <= 0)
                    {
                        throw new InvalidDataException("Line length is invalid.");
                    }

                    // Use the more efficient ParseLine method
                    line.ParseLine(Input, outputFormat);

                    // Apply filtering if specified
                    if (magazine.HasValue && line.Magazine != magazine.Value && outputFormat == Format.T42)
                    {
                        lineNumber++;
                        continue; // Skip lines that don't match the magazine filter
                    }

                    if (rows != null && !rows.Contains(line.Row) && outputFormat == Format.T42)
                    {
                        lineNumber++;
                        continue; // Skip lines that don't match the row filter
                    }

                    packet.Lines.Add(line);
                    lineNumber++; // Increment line number for each line processed
                }
                // Only yield packets that have at least one line after filtering
                if (packet.Lines.Count > 0)
                {
                    yield return packet;
                }
                timecode = timecode.GetNext(); // Increment timecode for the next packet
            }
        }

        /// <summary>
        /// Asynchronously parses the extracted MXF data and returns an async enumerable of packets with optional filtering.
        /// </summary>
        /// <param name="magazine">Optional magazine number filter (default: all magazines)</param>
        /// <param name="rows">Optional array of row numbers to filter (default: all rows)</param>
        /// <param name="startTimecode">Optional starting timecode for packet numbering</param>
        /// <param name="cancellationToken">Token to cancel the operation</param>
        /// <returns>An async enumerable of parsed packets matching the filter criteria</returns>
        public async IAsyncEnumerable<Packet> ParseAsync(
            int? magazine = null,
            int[]? rows = null,
            Timecode? startTimecode = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            rows ??= Constants.DEFAULT_ROWS;
            var outputFormat = OutputFormat ?? Format.T42;
            int lineNumber = 0;
            var timecode = startTimecode ?? new Timecode(0);

            var arrayPool = ArrayPool<byte>.Shared;
            var packetBuffer = arrayPool.Rent(Constants.PACKET_HEADER_SIZE);
            var lineBuffer = arrayPool.Rent(Constants.LINE_HEADER_SIZE);

            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Read packet header asynchronously
                    var packetMemory = packetBuffer.AsMemory(0, Constants.PACKET_HEADER_SIZE);
                    var packetBytesRead = await Input.ReadAsync(packetMemory, cancellationToken);

                    if (packetBytesRead != Constants.PACKET_HEADER_SIZE)
                        break;

                    var packet = new Packet(packetBuffer.AsSpan(0, Constants.PACKET_HEADER_SIZE).ToArray())
                    {
                        Timecode = timecode
                    };

                    for (var l = 0; l < packet.LineCount; l++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var lineMemory = lineBuffer.AsMemory(0, Constants.LINE_HEADER_SIZE);
                        var lineBytesRead = await Input.ReadAsync(lineMemory, cancellationToken);

                        if (lineBytesRead < Constants.LINE_HEADER_SIZE) break;

                        var line = new Line(lineBuffer.AsSpan(0, Constants.LINE_HEADER_SIZE))
                        {
                            LineNumber = lineNumber,
                            LineTimecode = timecode
                        };

                        if (line.Length <= 0)
                            throw new InvalidDataException("Line length is invalid.");

                        // Parse line data asynchronously
                        await ParseLineAsync(line, Input, outputFormat, cancellationToken);

                        // Apply filtering
                        if (magazine.HasValue && line.Magazine != magazine.Value && outputFormat == Format.T42)
                        {
                            lineNumber++;
                            continue;
                        }

                        if (rows != null && !rows.Contains(line.Row) && outputFormat == Format.T42)
                        {
                            lineNumber++;
                            continue;
                        }

                        packet.Lines.Add(line);
                        lineNumber++;
                    }

                    if (packet.Lines.Count > 0)
                    {
                        yield return packet;
                    }

                    timecode = timecode.GetNext();
                }
            }
            finally
            {
                arrayPool.Return(packetBuffer);
                arrayPool.Return(lineBuffer);
            }
        }

        /// <summary>
        /// Asynchronously parses line data from a stream
        /// </summary>
        private static async Task ParseLineAsync(Line line, Stream input, Format outputFormat, CancellationToken cancellationToken)
        {
            if (line.Length <= 0)
                throw new InvalidDataException("Line length is invalid.");

            var arrayPool = ArrayPool<byte>.Shared;
            var dataBuffer = arrayPool.Rent(line.Length);

            try
            {
                var dataMemory = dataBuffer.AsMemory(0, line.Length);
                var bytesRead = await input.ReadAsync(dataMemory, cancellationToken);

                if (bytesRead < line.Length)
                    throw new InvalidDataException($"Not enough data to read the line. Expected {line.Length}, got {bytesRead}.");

                // Use the existing ParseLine logic with the read data
                line.ParseLine(dataBuffer.AsSpan(0, line.Length).ToArray(), outputFormat);
            }
            finally
            {
                arrayPool.Return(dataBuffer);
            }
        }

        /// <summary>
        /// Releases all resources used by the parser.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            // Only dispose streams we created (not stdin/stdout)
            if (InputFile != null)
            {
                Input?.Dispose();
            }
            if (OutputFile != null)
            {
                _outputStream?.Dispose();
            }
        }
    }
}