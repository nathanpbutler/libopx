using System;
using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Handlers;

namespace nathanbutlerDEV.libopx.Formats;

/// <summary>
/// Parser for VBI (Vertical Blanking Interval) format files with support for conversion to T42 teletext format.
/// Handles both single and double-line VBI data with automatic format detection and filtering capabilities.
/// </summary>
public class VBI : FormatIOBase
{
    /// <summary>
    /// Internal handler for VBI format parsing operations.
    /// Handler is initialized with the current InputFormat when Parse is called.
    /// </summary>
    private VBIHandler? _handler;

    /// <summary>
    /// Gets or sets the input format. Default is VBI.
    /// </summary>
    public Format InputFormat { get; set; } = Format.VBI;

    /// <summary>
    /// Gets the length of the VBI line based on the input format (single or double).
    /// </summary>
    public int LineLength => InputFormat == Format.VBI_DOUBLE ? Constants.VBI_DOUBLE_LINE_SIZE : Constants.VBI_LINE_SIZE;

    /// <summary>
    /// Gets the array of valid output formats supported by the VBI parser.
    /// </summary>
    public static readonly Format[] ValidOutputs = [Format.VBI, Format.VBI_DOUBLE, Format.T42, Format.RCWT, Format.STL];

    /// <summary>
    /// Gets or sets the number of lines per frame for timecode incrementation. Default is 2.
    /// </summary>
    public int LineCount { get; set; } = 2;

    /// <summary>
    /// Constructor for VBI format from file
    /// </summary>
    /// <param name="inputFile">Path to the VBI file</param>
    /// <param name="vbiType">The VBI format type (VBI or VBI_DOUBLE)</param>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
    [Obsolete("Use FormatIO.Open() instead. This constructor will be removed in v3.0.0.")]
    [SetsRequiredMembers]
    public VBI(string inputFile, Format? vbiType = Format.VBI)
    {
        InputFile = new FileInfo(inputFile);

        if (!InputFile.Exists)
        {
            throw new FileNotFoundException("The specified VBI file does not exist.", inputFile);
        }
        // If vbiType is not specified, default determine from file extension
        InputFormat = vbiType ?? (InputFile.Extension.ToLower() switch
        {
            ".vbi" => Format.VBI,
            ".vbid" => Format.VBI_DOUBLE,
            _ => Format.VBI // Default to VBI if unknown
        });

        Input = InputFile.OpenRead();
        OutputFormat = Format.T42; // Set default output format
    }

    /// <summary>
    /// Constructor for VBI format from stdin
    /// </summary>
    [Obsolete("Use FormatIO.OpenStdin() instead. This constructor will be removed in v3.0.0.")]
    [SetsRequiredMembers]
    public VBI()
    {
        InputFile = null;
        Input = Console.OpenStandardInput();
        OutputFormat = Format.T42; // Set default output format
    }

    /// <summary>
    /// Constructor for VBI format with custom stream
    /// </summary>
    /// <param name="inputStream">The input stream to read from</param>
    /// <param name="vbiType">The VBI format type (VBI or VBI_DOUBLE)</param>
    /// <exception cref="ArgumentNullException">Thrown if inputStream is null</exception>
    [Obsolete("Use FormatIO.Open(stream, format) instead. This constructor will be removed in v3.0.0.")]
    [SetsRequiredMembers]
    public VBI(Stream inputStream, Format? vbiType = Format.VBI)
    {
        InputFile = null;
        Input = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
        InputFormat = vbiType ?? Format.VBI;
        OutputFormat = Format.T42; // Set default output format
    }

    /// <summary>
    /// Parses the VBI file and returns an enumerable of lines with optional filtering.
    /// </summary>
    /// <param name="magazine">Optional magazine number filter for teletext data (default: all magazines)</param>
    /// <param name="rows">Optional array of row numbers to filter (default: all rows)</param>
    /// <returns>An enumerable of parsed lines matching the filter criteria</returns>
    public IEnumerable<Line> Parse(int? magazine = null, int[]? rows = null)
    {
        // Create or update handler with current InputFormat
        _handler ??= new VBIHandler(InputFormat);
        _handler.VbiInputFormat = InputFormat;

        // Create options from parameters
        var options = new ParseOptions
        {
            Magazine = magazine,
            Rows = rows,
            OutputFormat = OutputFormat ?? Format.T42,
            LineCount = LineCount
        };

        // Delegate to handler
        return _handler.Parse(Input, options);
    }

    /// <summary>
    /// Asynchronously parses the VBI file and returns an async enumerable of lines with optional filtering.
    /// Provides better performance for large files with non-blocking I/O operations.
    /// </summary>
    /// <param name="magazine">Optional magazine number filter for teletext data (default: all magazines)</param>
    /// <param name="rows">Optional array of row numbers to filter (default: all rows)</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>An async enumerable of parsed lines matching the filter criteria</returns>
    public async IAsyncEnumerable<Line> ParseAsync(
        int? magazine = null,
        int[]? rows = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Create or update handler with current InputFormat
        _handler ??= new VBIHandler(InputFormat);
        _handler.VbiInputFormat = InputFormat;

        // Create options from parameters
        var options = new ParseOptions
        {
            Magazine = magazine,
            Rows = rows,
            OutputFormat = OutputFormat ?? Format.T42,
            LineCount = LineCount
        };

        // Delegate to handler
        await foreach (var line in _handler.ParseAsync(Input, options, cancellationToken))
        {
            yield return line;
        }
    }

    /// <summary>
    /// Converts VBI line data to T42 teletext format using signal processing and bit extraction.
    /// </summary>
    /// <param name="lineData">The VBI line data to convert (720 or 1440 bytes)</param>
    /// <param name="debug">Whether to enable debug output during conversion</param>
    /// <returns>A 42-byte T42 teletext line, or empty array if conversion fails</returns>
    /// <exception cref="ArgumentException">Thrown when line data is not the correct size</exception>
    [Obsolete("Use FormatConverter.VBIToT42() instead. This method will be removed in v3.0.0.")]
    public static byte[] ToT42(byte[] lineData, bool debug = false)
    {
        return Core.FormatConverter.VBIToT42(lineData, debug);
    }
}
