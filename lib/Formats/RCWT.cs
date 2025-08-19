using System.Diagnostics.CodeAnalysis;
using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.libopx.Formats;

/// <summary>
/// Parser and writer for RCWT (Raw Caption With Timing) format files.
/// RCWT is essentially T42 bytes with additional timing, header and control bytes.
/// </summary>
public partial class RCWT : IDisposable
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
    /// Gets or sets the input stream for reading RCWT data.
    /// </summary>
    public required Stream Input { get; set; }
    /// <summary>
    /// Gets the output stream for writing processed data.
    /// </summary>
    public Stream Output => _outputStream ??= OutputFile == null ? Console.OpenStandardOutput() : OutputFile.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
    /// <summary>
    /// Gets or sets the input format. Default is RCWT.
    /// </summary>
    public Format InputFormat { get; set; } = Format.RCWT;
    /// <summary>
    /// Gets or sets the output format for processed data. Default is RCWT.
    /// </summary>
    public Format? OutputFormat { get; set; } = Format.RCWT;
    /// <summary>
    /// Gets or sets the function mode for processing. Default is Filter.
    /// </summary>
    public Function Function { get; set; } = Function.Filter;
    /// <summary>
    /// Gets or sets the number of lines per frame for timecode incrementation. Default is 2.
    /// </summary>
    public int LineCount { get; set; } = 2;

    private static bool _headerSet = false;

    /// <summary>
    /// Indicates whether the RCWT header has been set.
    /// </summary>
    public static bool HeaderSet
    {
        get => _headerSet;
        set => _headerSet = value; // Set if we start outputting RCWT
    }

    /// <summary>
    /// The header for RCWT
    /// </summary>
    public static readonly byte[] Header = Constants.RCWT_HEADER;

    private static int _fts = 0;

    /// <summary>
    /// FTS value (Time in ms)
    /// </summary>
    public static int FTS
    {
        get => _fts;
        set => _fts = value; // Set the FTS value
    }

    private static int _fieldNumber = 0;

    /// <summary>
    /// Field number
    /// If 0 then 0xAF, otherwise 0xAB
    /// </summary>
    public static int FieldNumber
    {
        get => _fieldNumber;
        set => _fieldNumber = value; // Set the field number
    }

    /// <summary>
    /// Valid outputs: t42/vbi/vbi_double/rcwt
    /// </summary>
    public static readonly Format[] ValidOutputs = [Format.T42, Format.VBI, Format.VBI_DOUBLE, Format.RCWT];

    /// <summary>
    /// Constructor for RCWT format from file
    /// </summary>
    /// <param name="inputFile">Path to the input RCWT file</param>
    /// <exception cref="FileNotFoundException">Thrown if the specified file does not exist</exception>
    [SetsRequiredMembers]
    public RCWT(string inputFile)
    {
        InputFile = new FileInfo(inputFile);

        if (!InputFile.Exists)
        {
            throw new FileNotFoundException("Input file not found", InputFile.FullName);
        }

        Input = InputFile.OpenRead();
    }

    /// <summary>
    /// Constructor for RCWT format from stdin
    /// </summary>
    [SetsRequiredMembers]
    public RCWT()
    {
        InputFile = null;
        Input = Console.OpenStandardInput();
    }

    /// <summary>
    /// Constructor for RCWT format with custom stream
    /// </summary>
    [SetsRequiredMembers]
    public RCWT(Stream inputStream)
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
    /// Initialise the RCWT
    /// </summary>
    public static void Initialise()
    {
        HeaderSet = true;
    }

    /// <summary>
    /// Set the FTS value
    /// </summary>
    /// <param name="fts">The FTS value.</param>
    public static void SetFTS(int fts)
    {
        FTS = fts;
    }

    /// <summary>
    /// Set the field number
    /// </summary>
    /// <param name="fieldNumber">The field number.</param>
    public static void SetFieldNumber(int fieldNumber)
    {
        FieldNumber = fieldNumber;
    }

    /// <summary>
    /// Parses the RCWT file and returns an enumerable of lines with optional filtering.
    /// </summary>
    /// <param name="magazine">Optional magazine number filter (default: all magazines)</param>
    /// <param name="rows">Optional array of row numbers to filter (default: all rows)</param>
    /// <returns>An enumerable of parsed lines matching the filter criteria</returns>
    public IEnumerable<Line> Parse(int? magazine = null, int[]? rows = null)
    {
        // Use default rows if not specified
        rows ??= Constants.DEFAULT_ROWS;

        var buffer = new byte[1024];
        int lineNumber = 0;
        var timecode = new Timecode(0);
        bool headerRead = false;

        while (true)
        {
            // Read header if not already read
            if (!headerRead)
            {
                var headerBuffer = new byte[Header.Length];
                var headerBytesRead = Input.Read(headerBuffer, 0, Header.Length);
                if (headerBytesRead < Header.Length)
                {
                    yield break; // End of stream or invalid file
                }
                
                // Verify header matches expected
                if (!headerBuffer.SequenceEqual(Header))
                {
                    // If header doesn't match, might not be valid RCWT format
                    yield break;
                }
                headerRead = true;
            }

            // Read packet type
            var packetTypeByte = Input.ReadByte();
            if (packetTypeByte == -1) yield break; // End of stream

            // Read FTS bytes (8 bytes)
            var ftsBuffer = new byte[Constants.RCWT_FTS_BYTE_SIZE];
            var ftsBytesRead = Input.Read(ftsBuffer, 0, Constants.RCWT_FTS_BYTE_SIZE);
            if (ftsBytesRead < Constants.RCWT_FTS_BYTE_SIZE) yield break;

            // Parse FTS value
            var ftsValue = BitConverter.ToInt32(ftsBuffer, 0);

            // Read field number byte
            var fieldByte = Input.ReadByte();
            if (fieldByte == -1) yield break;

            var field = fieldByte == Constants.RCWT_FIELD_0_MARKER ? 0 : 1;

            // Read framing code
            var framingByte = Input.ReadByte();
            if (framingByte == -1 || framingByte != Constants.RCWT_FRAMING_CODE) yield break;

            // Read T42 data (42 bytes after framing code)
            var t42Buffer = new byte[Constants.T42_LINE_SIZE];
            var t42BytesRead = Input.Read(t42Buffer, 0, Constants.T42_LINE_SIZE);
            if (t42BytesRead == 0) yield break;

            // Increment timecode if LineCount is reached
            if (lineNumber % LineCount == 0 && lineNumber != 0)
            {
                timecode = timecode.GetNext();
            }

            // Create Line object with RCWT data
            var line = new Line()
            {
                LineNumber = lineNumber,
                Data = t42Buffer,
                Length = t42BytesRead,
                SampleCoding = 0x31, // T42 sample coding
                SampleCount = t42BytesRead,
                LineTimecode = timecode,
                Magazine = -1, // No magazine decoding for RCWT
                Row = -1, // No row decoding for RCWT
                Text = string.Empty // No text conversion for raw data
            };

            yield return line;

            lineNumber++;
        }
    }

    /// <summary>
    /// Process RCWT data and write the encoded packet to a stream.
    /// </summary>
    /// <param name="data">The RCWT packet payload.</param>
    /// <param name="output">Stream that receives the encoded packet.</param>
    /// <returns>0 when successful.</returns>
    public static async Task<int> Process(byte[] data, Stream output)
    {
        // If header is not set, set it
        if (!HeaderSet)
        {
            // Write the header to the output stream
            await output.WriteAsync(Header);
            HeaderSet = true;
        }

        // Write packet type (unknown purpose)
        await output.WriteAsync(new byte[] { Constants.RCWT_PACKET_TYPE_UNKNOWN });

        // Write the FTS value to the output stream (this should be FTS_BYTE_SIZE bytes length)
        await output.WriteAsync(GetFTSBytes());

        // Write the field number to the output stream (this should be 1 byte)
        await output.WriteAsync(new byte[] { GetFieldNumberByte() });

        // Write the framing code
        await output.WriteAsync(new byte[] { Constants.RCWT_FRAMING_CODE });

        // Write the data
        await output.WriteAsync(data);
        
        return 0;
    }

    /// <summary>
    /// Writes teletext data as RCWT format
    /// </summary>
    /// <param name="lines">Enumerable of teletext lines to convert</param>
    public async Task WriteTeletext(IEnumerable<Line> lines)
    {
        // Write header if not already set
        if (!HeaderSet)
        {
            await Output.WriteAsync(Header);
            HeaderSet = true;
        }

        foreach (var line in lines)
        {
            if (line.Data != null && line.Data.Length > 0)
            {
                // Set FTS value from line's timecode (convert to milliseconds)
                if (line.LineTimecode != null)
                {
                    SetFTS(line.LineTimecode.TotalMilliseconds());
                }
                
                await Process(line.Data, Output);
            }
        }
    }

    /// <summary>
    /// Disposes the resources used by the RCWT parser.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _outputStream?.Dispose();
        Input?.Dispose();
    }

    #region Private Methods


    /// <summary>
    /// Get FTS bytes and return with padded to FTS_BYTE_SIZE bytes
    /// </summary>
    private static byte[] GetFTSBytes()
    {
        var ftsBytes = BitConverter.GetBytes(FTS);
        return ftsBytes.Length == Constants.RCWT_FTS_BYTE_SIZE ? ftsBytes : [.. ftsBytes, .. new byte[Constants.RCWT_FTS_BYTE_SIZE - ftsBytes.Length]];
    }

    /// <summary>
    /// Get the field number byte
    /// </summary>
    /// <returns>The field number byte.</returns>
    private static byte GetFieldNumberByte()
    {
        return FieldNumber == 0 ? Constants.RCWT_FIELD_0_MARKER : Constants.RCWT_FIELD_1_MARKER;
    }

    #endregion
}