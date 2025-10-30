using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.libopx.Formats;

/// <summary>
/// Parser for MPEG Transport Stream (TS) format files with support for extracting teletext data.
/// Handles PAT/PMT parsing, PID filtering, and DVB teletext extraction with automatic stream detection.
/// </summary>
public class TS : IDisposable
{
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
    /// Gets or sets the input stream for reading TS data.
    /// </summary>
    public required Stream Input { get; set; }

    /// <summary>
    /// Gets the output stream for writing processed data.
    /// </summary>
    public Stream Output => _outputStream ??= OutputFile == null ? Console.OpenStandardOutput() : OutputFile.Open(FileMode.Create, FileAccess.Write, FileShare.Read);

    /// <summary>
    /// Gets or sets the input format. Default is TS.
    /// </summary>
    public Format InputFormat { get; set; } = Format.TS;

    /// <summary>
    /// Gets or sets the output format for processed data. Default is T42.
    /// </summary>
    public Format? OutputFormat { get; set; } = Format.T42;

    /// <summary>
    /// Gets the array of valid output formats supported by the TS parser.
    /// </summary>
    public static readonly Format[] ValidOutputs = [Format.T42, Format.VBI, Format.VBI_DOUBLE, Format.RCWT, Format.STL];

    /// <summary>
    /// Gets or sets the function mode for processing. Default is Filter.
    /// </summary>
    public Function Function { get; set; } = Function.Filter;

    /// <summary>
    /// Gets or sets the number of lines per frame for timecode incrementation. Default is 2.
    /// </summary>
    public int LineCount { get; set; } = 2;

    /// <summary>
    /// Gets or sets the PIDs to filter. If null, auto-detection is used to find all teletext streams.
    /// </summary>
    public int[]? PIDs { get; set; } = null;

    /// <summary>
    /// Gets or sets whether to auto-detect teletext PIDs from PAT/PMT tables. Default is true.
    /// </summary>
    public bool AutoDetect { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable verbose output for debugging. Default is false.
    /// </summary>
    public bool Verbose { get; set; } = false;

    // Internal state for TS parsing
    private readonly Dictionary<int, byte[]> _pesBuffers = [];
    private readonly Dictionary<int, int> _continuityCounters = [];
    private readonly HashSet<int> _pmtPIDs = [];
    private HashSet<int> _teletextPIDs = [];

    /// <summary>
    /// Constructor for TS format from file.
    /// </summary>
    /// <param name="inputFile">Path to the TS file</param>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
    [SetsRequiredMembers]
    public TS(string inputFile)
    {
        InputFile = new FileInfo(inputFile);

        if (!InputFile.Exists)
        {
            throw new FileNotFoundException("The specified TS file does not exist.", inputFile);
        }

        Input = InputFile.OpenRead();
    }

    /// <summary>
    /// Constructor for TS format from stdin.
    /// </summary>
    [SetsRequiredMembers]
    public TS()
    {
        InputFile = null;
        Input = Console.OpenStandardInput();
    }

    /// <summary>
    /// Constructor for TS format with custom stream.
    /// </summary>
    /// <param name="inputStream">The input stream to read from</param>
    [SetsRequiredMembers]
    public TS(Stream inputStream)
    {
        InputFile = null;
        Input = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
    }

    /// <summary>
    /// Sets the output file for writing.
    /// </summary>
    /// <param name="outputFile">Path to the output file</param>
    public void SetOutput(string outputFile)
    {
        OutputFile = new FileInfo(outputFile);
    }

    /// <summary>
    /// Sets the output stream for writing.
    /// </summary>
    /// <param name="outputStream">The output stream to write to</param>
    public void SetOutput(Stream outputStream)
    {
        OutputFile = null;
        _outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream), "Output stream cannot be null.");
    }

    /// <summary>
    /// Parses the TS file and returns an enumerable of lines with optional filtering.
    /// </summary>
    /// <param name="magazine">Optional magazine number filter for teletext data (default: all magazines)</param>
    /// <param name="rows">Optional array of row numbers to filter (default: all rows)</param>
    /// <param name="pids">Optional array of PIDs to filter (default: auto-detect or use class PIDs property)</param>
    /// <returns>An enumerable of parsed lines matching the filter criteria</returns>
    public IEnumerable<Line> Parse(int? magazine = null, int[]? rows = null, int[]? pids = null)
    {
        // Use default rows if not specified
        rows ??= Constants.DEFAULT_ROWS;

        // Determine PID filtering strategy
        var targetPIDs = pids ?? PIDs;
        if (targetPIDs != null)
        {
            // Manual PID filtering
            _teletextPIDs = new HashSet<int>(targetPIDs);
            AutoDetect = false;
            if (Verbose)
                Console.Error.WriteLine($"TS: Using manual PID filtering: {string.Join(", ", targetPIDs)}");
        }
        else if (Verbose)
        {
            Console.Error.WriteLine("TS: Using auto-detection for teletext PIDs");
        }

        var outputFormat = OutputFormat ?? Format.T42;

        // Initialize RCWT state if needed
        if (outputFormat == Format.RCWT)
        {
            Functions.ResetRCWTHeader();
        }

        int lineNumber = 0;
        var timecode = new Timecode(0);
        var tsBuffer = new byte[Constants.TS_PACKET_SIZE];

        while (Input.Read(tsBuffer, 0, Constants.TS_PACKET_SIZE) == Constants.TS_PACKET_SIZE)
        {
            // Validate sync byte
            if (tsBuffer[0] != Constants.TS_SYNC_BYTE)
            {
                // Try to resync - look for next sync byte
                continue;
            }

            // Parse TS header
            var pid = ((tsBuffer[1] & 0x1F) << 8) | tsBuffer[2];
            var payloadStart = (tsBuffer[1] & Constants.TS_PAYLOAD_START_INDICATOR) != 0;
            var hasAdaptation = (tsBuffer[3] & Constants.TS_ADAPTATION_FIELD_FLAG) != 0;
            var hasPayload = (tsBuffer[3] & Constants.TS_PAYLOAD_FLAG) != 0;
            var continuityCounter = tsBuffer[3] & 0x0F;

            if (!hasPayload)
                continue;

            // Calculate payload offset
            int payloadOffset = Constants.TS_HEADER_SIZE;
            if (hasAdaptation)
            {
                int adaptationLength = tsBuffer[4];
                payloadOffset += 1 + adaptationLength;
            }

            if (payloadOffset >= Constants.TS_PACKET_SIZE)
                continue;

            var payload = new byte[Constants.TS_PACKET_SIZE - payloadOffset];
            Array.Copy(tsBuffer, payloadOffset, payload, 0, payload.Length);

            // Process PAT (PID 0)
            if (pid == Constants.TS_PAT_PID && AutoDetect)
            {
                ProcessPAT(payload, payloadStart);
                continue;
            }

            // Process PMT
            if (_pmtPIDs.Contains(pid) && AutoDetect)
            {
                ProcessPMT(payload, payloadStart);
                continue;
            }

            // Process teletext PIDs
            if (_teletextPIDs.Contains(pid))
            {
                // Accumulate PES data
                AccumulatePES(pid, payload, payloadStart, continuityCounter);

                // Try to extract teletext lines
                foreach (var t42Data in ExtractTeletextData(pid))
                {
                    var line = CreateLineFromT42(t42Data, ref lineNumber, ref timecode);
                    if (line != null)
                    {
                        // Apply filtering
                        if (magazine.HasValue && line.Magazine != magazine.Value)
                            continue;

                        if (rows != null && !rows.Contains(line.Row))
                            continue;

                        yield return line;
                    }
                }
            }
        }

        // Flush any remaining data
        foreach (var pid in _teletextPIDs)
        {
            foreach (var t42Data in ExtractTeletextData(pid))
            {
                var line = CreateLineFromT42(t42Data, ref lineNumber, ref timecode);
                if (line != null)
                {
                    // Apply filtering
                    if (magazine.HasValue && line.Magazine != magazine.Value)
                        continue;

                    if (rows != null && !rows.Contains(line.Row))
                        continue;

                    yield return line;
                }
            }
        }
    }

    /// <summary>
    /// Asynchronously parses the TS file and returns an async enumerable of lines with optional filtering.
    /// Provides better performance for large files with non-blocking I/O operations.
    /// </summary>
    /// <param name="magazine">Optional magazine number filter for teletext data (default: all magazines)</param>
    /// <param name="rows">Optional array of row numbers to filter (default: all rows)</param>
    /// <param name="pids">Optional array of PIDs to filter (default: auto-detect or use class PIDs property)</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>An async enumerable of parsed lines matching the filter criteria</returns>
    public async IAsyncEnumerable<Line> ParseAsync(
        int? magazine = null,
        int[]? rows = null,
        int[]? pids = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Use default rows if not specified
        rows ??= Constants.DEFAULT_ROWS;

        // Determine PID filtering strategy
        var targetPIDs = pids ?? PIDs;
        if (targetPIDs != null)
        {
            _teletextPIDs = new HashSet<int>(targetPIDs);
            AutoDetect = false;
            if (Verbose)
                Console.Error.WriteLine($"TS: Using manual PID filtering: {string.Join(", ", targetPIDs)}");
        }
        else if (Verbose)
        {
            Console.Error.WriteLine("TS: Using auto-detection for teletext PIDs");
        }

        var outputFormat = OutputFormat ?? Format.T42;

        // Initialize RCWT state if needed
        if (outputFormat == Format.RCWT)
        {
            Functions.ResetRCWTHeader();
        }

        int lineNumber = 0;
        var timecode = new Timecode(0);

        // Use ArrayPool for better memory management
        var arrayPool = ArrayPool<byte>.Shared;
        var tsBuffer = arrayPool.Rent(Constants.TS_PACKET_SIZE);

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var bufferMemory = tsBuffer.AsMemory(0, Constants.TS_PACKET_SIZE);
                var bytesRead = await Input.ReadAsync(bufferMemory, cancellationToken);

                if (bytesRead != Constants.TS_PACKET_SIZE)
                    break;

                // Validate sync byte
                if (tsBuffer[0] != Constants.TS_SYNC_BYTE)
                {
                    continue;
                }

                // Parse TS header
                var pid = ((tsBuffer[1] & 0x1F) << 8) | tsBuffer[2];
                var payloadStart = (tsBuffer[1] & Constants.TS_PAYLOAD_START_INDICATOR) != 0;
                var hasAdaptation = (tsBuffer[3] & Constants.TS_ADAPTATION_FIELD_FLAG) != 0;
                var hasPayload = (tsBuffer[3] & Constants.TS_PAYLOAD_FLAG) != 0;
                var continuityCounter = tsBuffer[3] & 0x0F;

                if (!hasPayload)
                    continue;

                // Calculate payload offset
                int payloadOffset = Constants.TS_HEADER_SIZE;
                if (hasAdaptation)
                {
                    int adaptationLength = tsBuffer[4];
                    payloadOffset += 1 + adaptationLength;
                }

                if (payloadOffset >= Constants.TS_PACKET_SIZE)
                    continue;

                var payload = tsBuffer.AsSpan(payloadOffset, Constants.TS_PACKET_SIZE - payloadOffset).ToArray();

                // Process PAT
                if (pid == Constants.TS_PAT_PID && AutoDetect)
                {
                    ProcessPAT(payload, payloadStart);
                    continue;
                }

                // Process PMT
                if (_pmtPIDs.Contains(pid) && AutoDetect)
                {
                    ProcessPMT(payload, payloadStart);
                    continue;
                }

                // Process teletext PIDs
                if (_teletextPIDs.Contains(pid))
                {
                    AccumulatePES(pid, payload, payloadStart, continuityCounter);

                    foreach (var t42Data in ExtractTeletextData(pid))
                    {
                        var line = CreateLineFromT42(t42Data, ref lineNumber, ref timecode);
                        if (line != null)
                        {
                            // Apply filtering
                            if (magazine.HasValue && line.Magazine != magazine.Value)
                                continue;

                            if (rows != null && !rows.Contains(line.Row))
                                continue;

                            yield return line;
                        }
                    }
                }
            }

            // Flush remaining data
            foreach (var pid in _teletextPIDs)
            {
                foreach (var t42Data in ExtractTeletextData(pid))
                {
                    var line = CreateLineFromT42(t42Data, ref lineNumber, ref timecode);
                    if (line != null)
                    {
                        // Apply filtering
                        if (magazine.HasValue && line.Magazine != magazine.Value)
                            continue;

                        if (rows != null && !rows.Contains(line.Row))
                            continue;

                        yield return line;
                    }
                }
            }
        }
        finally
        {
            arrayPool.Return(tsBuffer);
        }
    }

    /// <summary>
    /// Processes PAT (Program Association Table) to find PMT PIDs.
    /// </summary>
    private void ProcessPAT(byte[] payload, bool payloadStart)
    {
        if (!payloadStart || payload.Length < 8)
            return;

        // Skip pointer field if present
        int offset = payload[0] + 1;

        if (offset + 8 > payload.Length)
            return;

        // Parse PAT
        int tableId = payload[offset];
        if (tableId != 0x00) // PAT table ID
            return;

        int sectionLength = ((payload[offset + 1] & 0x0F) << 8) | payload[offset + 2];
        int programInfoStart = offset + 8;
        int programInfoEnd = Math.Min(offset + 3 + sectionLength - 4, payload.Length); // -4 for CRC

        // Extract PMT PIDs
        for (int i = programInfoStart; i + 3 < programInfoEnd; i += 4)
        {
            int programNumber = (payload[i] << 8) | payload[i + 1];
            int pmtPid = ((payload[i + 2] & 0x1F) << 8) | payload[i + 3];

            if (programNumber != 0) // Ignore network PID
            {
                _pmtPIDs.Add(pmtPid);
                if (Verbose)
                    Console.Error.WriteLine($"TS: Found PMT PID {pmtPid} for program {programNumber}");
            }
        }
    }

    /// <summary>
    /// Processes PMT (Program Map Table) to find teletext stream PIDs.
    /// </summary>
    private void ProcessPMT(byte[] payload, bool payloadStart)
    {
        if (!payloadStart || payload.Length < 12)
            return;

        // Skip pointer field
        int offset = payload[0] + 1;

        if (offset + 12 > payload.Length)
            return;

        int tableId = payload[offset];
        if (tableId != 0x02) // PMT table ID
            return;

        int sectionLength = ((payload[offset + 1] & 0x0F) << 8) | payload[offset + 2];
        int programInfoLength = ((payload[offset + 10] & 0x0F) << 8) | payload[offset + 11];
        int streamInfoStart = offset + 12 + programInfoLength;
        int streamInfoEnd = Math.Min(offset + 3 + sectionLength - 4, payload.Length);

        // Parse stream information
        int i = streamInfoStart;
        while (i + 4 < streamInfoEnd)
        {
            int streamType = payload[i];
            int elementaryPid = ((payload[i + 1] & 0x1F) << 8) | payload[i + 2];
            int esInfoLength = ((payload[i + 3] & 0x0F) << 8) | payload[i + 4];

            if (Verbose)
                Console.Error.WriteLine($"TS: Found stream type 0x{streamType:X2} on PID {elementaryPid}");

            // Check if this is a teletext stream
            if (streamType == Constants.TS_STREAM_TYPE_TELETEXT)
            {
                _teletextPIDs.Add(elementaryPid);
                if (Verbose)
                    Console.Error.WriteLine($"TS: *** Found teletext stream on PID {elementaryPid} ***");
            }
            // TODO: Add DVB subtitle support (stream type 0x05)

            i += 5 + esInfoLength;
        }

        if (Verbose && _teletextPIDs.Count == 0)
        {
            Console.Error.WriteLine("TS: Warning - No teletext streams found in PMT");
        }
    }

    /// <summary>
    /// Accumulates PES packet data for a given PID.
    /// </summary>
    private void AccumulatePES(int pid, byte[] payload, bool payloadStart, int continuityCounter)
    {
        if (payloadStart)
        {
            // Start new PES packet
            _pesBuffers[pid] = [.. payload];
            _continuityCounters[pid] = continuityCounter;
        }
        else
        {
            // Append to existing PES buffer
            if (_pesBuffers.TryGetValue(pid, out var existing))
            {
                var combined = new byte[existing.Length + payload.Length];
                Array.Copy(existing, 0, combined, 0, existing.Length);
                Array.Copy(payload, 0, combined, existing.Length, payload.Length);
                _pesBuffers[pid] = combined;
            }
        }
    }

    /// <summary>
    /// Extracts T42 teletext data from accumulated PES data.
    /// </summary>
    private IEnumerable<byte[]> ExtractTeletextData(int pid)
    {
        if (!_pesBuffers.TryGetValue(pid, out var pesData) || pesData.Length == 0)
            yield break;

        if (Verbose && pesData.Length > 0)
            Console.Error.WriteLine($"TS: Extracting from PID {pid}, PES buffer size: {pesData.Length}");

        // Check for PES start code
        if (pesData.Length < 6 || pesData[0] != 0x00 || pesData[1] != 0x00 || pesData[2] != 0x01)
        {
            if (Verbose)
                Console.Error.WriteLine($"TS: PID {pid} - Invalid PES start code");
            _pesBuffers[pid] = [];
            yield break;
        }

        // Parse PES header
        int pesHeaderLength = pesData[8];
        int pesDataStart = 9 + pesHeaderLength;

        if (Verbose)
            Console.Error.WriteLine($"TS: PID {pid} - PES header length: {pesHeaderLength}, data start: {pesDataStart}");

        if (pesDataStart + 1 >= pesData.Length)
        {
            if (Verbose)
                Console.Error.WriteLine($"TS: PID {pid} - Not enough data after PES header");
            _pesBuffers[pid] = [];
            yield break;
        }

        // Check data identifier
        int dataIdentifier = pesData[pesDataStart];
        if (Verbose)
            Console.Error.WriteLine($"TS: PID {pid} - Data identifier: 0x{dataIdentifier:X2}");

        if (dataIdentifier != Constants.TS_DATA_IDENTIFIER_EBU_TELETEXT)
        {
            if (Verbose)
                Console.Error.WriteLine($"TS: PID {pid} - Wrong data identifier (expected 0x{Constants.TS_DATA_IDENTIFIER_EBU_TELETEXT:X2})");
            _pesBuffers[pid] = [];
            yield break;
        }

        // Parse teletext data units
        int offset = pesDataStart + 1;
        int dataUnitCount = 0;
        int validDataUnits = 0;

        while (offset + Constants.TS_TELETEXT_DATA_UNIT_SIZE <= pesData.Length)
        {
            int dataUnitId = pesData[offset];
            int dataUnitLength = pesData[offset + 1];
            dataUnitCount++;

            if (Verbose)
                Console.Error.WriteLine($"TS: PID {pid} - Data unit #{dataUnitCount}: ID=0x{dataUnitId:X2}, Length={dataUnitLength}");

            // Check for teletext data unit (0x02 or 0x03)
            // Note: data unit length is typically 44 (includes 2 framing bytes + 42 T42 bytes)
            if ((dataUnitId == Constants.TS_DATA_UNIT_ID_TELETEXT || dataUnitId == Constants.TS_DATA_UNIT_ID_TELETEXT_SUBTITLE)
                && (dataUnitLength == Constants.T42_LINE_SIZE || dataUnitLength == 44))
            {
                // Extract T42 data (skip 2-byte data unit header, then skip 2 framing bytes if length is 44)
                int dataStart = offset + 2;
                if (dataUnitLength == 44)
                {
                    // Skip the 2 framing bytes (field parity + line offset / framing code)
                    dataStart += 2;
                }

                if (Verbose)
                    Console.Error.WriteLine($"TS: PID {pid} - Copying {Constants.T42_LINE_SIZE} bytes from offset {dataStart}");

                var t42Data = new byte[Constants.T42_LINE_SIZE];
                Array.Copy(pesData, dataStart, t42Data, 0, Constants.T42_LINE_SIZE);

                // Reverse bits in each byte (LSB-first to MSB-first conversion)
                for (int i = 0; i < t42Data.Length; i++)
                {
                    t42Data[i] = ReverseBits(t42Data[i]);
                }

                validDataUnits++;
                if (Verbose)
                    Console.Error.WriteLine($"TS: PID {pid} - Extracted valid T42 data unit #{validDataUnits}");

                yield return t42Data;
            }

            offset += 2 + dataUnitLength;
        }

        if (Verbose)
            Console.Error.WriteLine($"TS: PID {pid} - Processed {dataUnitCount} data units, {validDataUnits} valid T42 units");

        // Clear processed data
        _pesBuffers[pid] = [];
    }

    /// <summary>
    /// Creates a Line object from T42 data.
    /// </summary>
    private Line? CreateLineFromT42(byte[] t42Data, ref int lineNumber, ref Timecode timecode)
    {
        if (t42Data.Length < Constants.T42_LINE_SIZE || Array.TrueForAll(t42Data, b => b == 0))
            return null;

        // Increment timecode if needed
        if (lineNumber % LineCount == 0 && lineNumber != 0)
        {
            timecode = timecode.GetNext();
        }

        var line = new Line
        {
            LineNumber = lineNumber,
            LineTimecode = timecode,
        };

        // Use ParseLine to handle format conversion
        var outputFormat = OutputFormat ?? Format.T42;
        line.ParseLine(t42Data, outputFormat);

        lineNumber++;
        return line;
    }

    /// <summary>
    /// Reverses the bits in a byte (LSB-first to MSB-first conversion).
    /// This is needed because teletext data in MPEG-TS is transmitted LSB-first.
    /// </summary>
    /// <param name="b">The byte to reverse</param>
    /// <returns>The byte with bits reversed</returns>
    private static byte ReverseBits(byte b)
    {
        // Reverse bits using lookup table approach (fastest method)
        b = (byte)((b * 0x0202020202UL & 0x010884422010UL) % 1023);
        return b;
    }

    /// <summary>
    /// Disposes the resources used by the TS parser.
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

        // Clear internal state
        _pesBuffers.Clear();
        _continuityCounters.Clear();
        _teletextPIDs.Clear();
        _pmtPIDs.Clear();
    }
}
