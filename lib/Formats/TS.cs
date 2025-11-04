using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Handlers;

namespace nathanbutlerDEV.libopx.Formats;

/// <summary>
/// Parser for MPEG Transport Stream (TS) format files with support for extracting teletext data.
/// Handles PAT/PMT parsing, PID filtering, and DVB teletext extraction with automatic stream detection.
/// </summary>
public class TS : FormatIOBase
{
    private TSHandler? _handler;

    /// <summary>
    /// Gets or sets the input format. Default is TS.
    /// </summary>
    public Format InputFormat { get; set; } = Format.TS;

    /// <summary>
    /// Gets the array of valid output formats supported by the TS parser.
    /// </summary>
    public static readonly Format[] ValidOutputs = [Format.T42, Format.VBI, Format.VBI_DOUBLE, Format.RCWT, Format.STL];

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

    /// <summary>
    /// Gets or sets the frame rate/timebase for PTS-to-timecode conversion. Default is 25.
    /// This is used when converting PTS timestamps to timecodes. Common values: 24, 25, 30, 48, 50, 60.
    /// </summary>
    public int FrameRate { get; set; } = 25;

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
        OutputFormat = Format.T42; // Set default output format
    }

    /// <summary>
    /// Constructor for TS format from stdin.
    /// </summary>
    [SetsRequiredMembers]
    public TS()
    {
        InputFile = null;
        Input = Console.OpenStandardInput();
        OutputFormat = Format.T42; // Set default output format
    }

    /// <summary>
    /// Constructor for TS format with custom stream.
    /// </summary>
    /// <param name="inputStream">The input stream to read from</param>
    /// <exception cref="ArgumentNullException">Thrown if inputStream is null</exception>
    [SetsRequiredMembers]
    public TS(Stream inputStream)
    {
        InputFile = null;
        Input = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
        OutputFormat = Format.T42; // Set default output format
    }

    /// <summary>
    /// Gets or creates the handler instance with current configuration.
    /// </summary>
    private TSHandler GetHandler()
    {
        // Create a new handler with current configuration
        _handler = new TSHandler(PIDs, AutoDetect, Verbose, FrameRate);
        return _handler;
    }

    /// <summary>
    /// Parses the TS file and returns an enumerable of packets with optional filtering.
    /// Each packet represents a frame containing multiple teletext lines from a PES packet.
    /// </summary>
    /// <param name="magazine">Optional magazine number filter for teletext data (default: all magazines)</param>
    /// <param name="rows">Optional array of row numbers to filter (default: all rows)</param>
    /// <param name="pids">Optional array of PIDs to filter (default: auto-detect or use class PIDs property)</param>
    /// <returns>An enumerable of parsed packets matching the filter criteria</returns>
    public IEnumerable<Packet> Parse(int? magazine = null, int[]? rows = null, int[]? pids = null)
    {
        // Delegate to handler
        var handler = GetHandler();
        var options = new ParseOptions
        {
            Magazine = magazine,
            Rows = rows,
            PIDs = pids,
            OutputFormat = OutputFormat ?? Format.T42
        };

        return handler.Parse(Input, options);
    }

    /// <summary>
    /// Asynchronously parses the TS file and returns an async enumerable of packets with optional filtering.
    /// Provides better performance for large files with non-blocking I/O operations.
    /// Each packet represents a frame containing multiple teletext lines from a PES packet.
    /// </summary>
    /// <param name="magazine">Optional magazine number filter for teletext data (default: all magazines)</param>
    /// <param name="rows">Optional array of row numbers to filter (default: all rows)</param>
    /// <param name="pids">Optional array of PIDs to filter (default: auto-detect or use class PIDs property)</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>An async enumerable of parsed packets matching the filter criteria</returns>
    public async IAsyncEnumerable<Packet> ParseAsync(
        int? magazine = null,
        int[]? rows = null,
        int[]? pids = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Delegate to handler
        var handler = GetHandler();
        var options = new ParseOptions
        {
            Magazine = magazine,
            Rows = rows,
            PIDs = pids,
            OutputFormat = OutputFormat ?? Format.T42
        };

        await foreach (var packet in handler.ParseAsync(Input, options, cancellationToken))
        {
            yield return packet;
        }
    }

    /// <summary>
    /// Disposes the resources used by the TS parser.
    /// </summary>
    public override void Dispose()
    {
        // Call base class disposal for streams
        base.Dispose();
    }
}
