using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Handlers;
using nathanbutlerDEV.libopx.SMPTE;

namespace nathanbutlerDEV.libopx.Formats;

/// <summary>
/// Parser for MXF (Material Exchange Format) files with support for stream extraction, filtering, and timecode restriping.
/// Handles KLV (Key-Length-Value) packets and SMPTE timecode processing.
/// </summary>
public class MXF : FormatIOBase
{

    /// <summary>
    /// Gets or sets the start timecode extracted from the MXF file.
    /// </summary>
    public Timecode StartTimecode { get; set; } = new Timecode(0);
    /// <summary>
    /// Handler instance for parsing operations.
    /// </summary>
    private MXFHandler? _handler;

    /// <summary>
    /// Gets the list of SMPTE timecodes per-frame in the MXF file.
    /// </summary>
    public List<Timecode> SMPTETimecodes => _handler?.SMPTETimecodes ?? [];

    /// <summary>
    /// Gets the list of packets parsed from the MXF data stream.
    /// </summary>
    public List<Packet> Packets => _handler?.Packets ?? [];
    /// <summary>
    /// Gets or sets the list of required key types for filtering during parsing.
    /// </summary>
    public List<KeyType> RequiredKeys { get; set; } = [];
    /// <summary>
    /// Gets or sets whether to validate that SMPTE timecodes are sequential.
    /// </summary>
    public bool CheckSequential { get; set; } = true;
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
    /// Gets or sets the progress reporter for tracking operation progress (0-100).
    /// </summary>
    public IProgress<double>? Progress { get; set; }

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

        // Set default output format
        OutputFormat = Format.T42;

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

        // Set default output format
        OutputFormat = Format.T42;

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
    public override void Dispose()
    {
        // Close MXF-specific extraction streams
        CloseExtractionStreams();

        // Call base class disposal for common streams
        base.Dispose();
    }

    /// <summary>
    /// Gets or creates the MXF handler with current configuration.
    /// </summary>
    private MXFHandler GetHandler()
    {
        _handler = new MXFHandler(
            StartTimecode,
            InputFile?.FullName,
            RequiredKeys,
            CheckSequential,
            DemuxMode,
            UseKeyNames,
            KlvMode,
            OutputBasePath,
            Verbose,
            PrintProgress,
            Function,
            Progress);
        return _handler;
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
        var keyBuffer = new byte[Constants.KLV_KEY_SIZE];

        while (Input.Position < 128000)
        {
            var keyBytesRead = Input.Read(keyBuffer, 0, Constants.KLV_KEY_SIZE);
            if (keyBytesRead != Constants.KLV_KEY_SIZE) break;

            var keyType = Keys.GetKeyType(keyBuffer.AsSpan(0, Constants.KLV_KEY_SIZE));

            var length = ReadBerLengthLocal(Input);
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
    /// Local helper for reading BER length in GetStartTimecode.
    /// </summary>
    private static int ReadBerLengthLocal(Stream input)
    {
        var firstByte = input.ReadByte();
        if (firstByte == -1) return -1;

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
                length = (length << 8) | b;
            }

            return length;
        }
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
        var handler = GetHandler();
        var options = new ParseOptions
        {
            Magazine = magazine,
            Rows = rows,
            StartTimecode = startTimecode == null
                ? null
                : new Timecode(startTimecode, StartTimecode.Timebase, StartTimecode.DropFrame),
            OutputFormat = OutputFormat ?? Format.T42
        };
        return handler.Parse(Input, options);
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
        var handler = GetHandler();
        var options = new ParseOptions
        {
            Magazine = magazine,
            Rows = rows,
            StartTimecode = startTimecode == null
                ? null
                : new Timecode(startTimecode, StartTimecode.Timebase, StartTimecode.DropFrame),
            OutputFormat = OutputFormat ?? Format.T42
        };

        await foreach (var packet in handler.ParseAsync(Input, options, cancellationToken))
        {
            yield return packet;
        }
    }

    private void CloseExtractionStreams()
    {
        // Delegate to handler for cleanup
        // Handler manages its own stream lifecycle
    }


    /// <summary>
    /// Parses and extracts all SMPTE timecodes from the MXF file's System metadata packets.
    /// </summary>
    /// <returns>True if timecodes were successfully parsed, false otherwise</returns>
    public bool ParseSMPTETimecodes()
    {
        // Create a handler specifically for extracting SMPTE timecodes
        _handler = new MXFHandler(
            StartTimecode,
            InputFile?.FullName,
            [KeyType.System], // Only process System packets
            CheckSequential,
            false, // demuxMode
            false, // useKeyNames
            false, // klvMode
            null, // outputBasePath
            Verbose,
            false, // printProgress
            Function.Filter); // Use Filter mode but only process System packets

        var options = new ParseOptions
        {
            OutputFormat = OutputFormat ?? Format.T42
        };

        // Iterate through Parse to trigger System packet processing
        // The handler will populate SMPTETimecodes as a side effect
        foreach (var _ in _handler.Parse(Input, options))
        {
            // No-op, just trigger the iteration
        }

        return SMPTETimecodes.Count > 0;
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


    /// <summary>
    /// Backward compatibility alias for ANC class.
    /// MXFData has been renamed to ANC (Ancillary Data) in v2.2.0 for clarity.
    /// Please use the ANC class directly for new code.
    /// </summary>
    /// <remarks>
    /// This type alias provides backward compatibility for existing code using MXF.MXFData.
    /// The ANC class is now a top-level class in the nathanbutlerDEV.libopx.Formats namespace.
    /// </remarks>
    public class MXFData : ANC
    {
        /// <summary>
        /// Constructor for extracted MXF data from file
        /// </summary>
        /// <param name="inputFile">Path to the input file (typically *.bin)</param>
        [SetsRequiredMembers]
        public MXFData(string inputFile) : base(inputFile) { }

        /// <summary>
        /// Constructor for extracted MXF data from stdin
        /// </summary>
        [SetsRequiredMembers]
        public MXFData() : base() { }

        /// <summary>
        /// Constructor for extracted MXF data with custom stream
        /// </summary>
        /// <param name="inputStream">The input stream to read from</param>
        [SetsRequiredMembers]
        public MXFData(Stream inputStream) : base(inputStream) { }
    }
}