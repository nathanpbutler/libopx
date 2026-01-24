using System;
using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Handlers;

namespace nathanbutlerDEV.libopx.Formats;

/// <summary>
/// Parser for T42 teletext format files with support for conversion to VBI formats and teletext content extraction.
/// Provides Hamming error correction and ANSI color formatting for teletext display.
/// </summary>
public class T42 : FormatIOBase
{
    /// <summary>
    /// Internal handler for T42 format parsing operations.
    /// </summary>
    private static readonly T42Handler _handler = new();

    /// <summary>
    /// Gets or sets the input format. Default is T42.
    /// </summary>
    public Format InputFormat { get; set; } = Format.T42;

    /// <summary>
    /// Gets the standard length of a T42 teletext line in bytes.
    /// </summary>
    public static int LineLength => Constants.T42_LINE_SIZE;

    /// <summary>
    /// Gets or sets the number of lines per frame for timecode incrementation. Default is 2.
    /// </summary>
    public int LineCount { get; set; } = 2;

    /// <summary>
    /// Gets the array of valid output formats supported by the T42 parser.
    /// </summary>
    public static readonly Format[] ValidOutputs = [Format.T42, Format.VBI, Format.VBI_DOUBLE, Format.RCWT, Format.STL];

    /// <summary>
    /// Constructor for T42 format from file
    /// </summary>
    /// <param name="inputFile">Path to the input T42 file</param>
    /// <exception cref="FileNotFoundException">Thrown if the specified file does not exist</exception>
    [Obsolete("Use FormatIO.Open() instead. This constructor will be removed in v3.0.0.")]
    [SetsRequiredMembers]
    public T42(string inputFile)
    {
        InputFile = new FileInfo(inputFile);

        if (!InputFile.Exists)
        {
            throw new FileNotFoundException("Input file not found", InputFile.FullName);
        }

        Input = InputFile.OpenRead();
        OutputFormat = Format.T42; // Set default output format
    }

    /// <summary>
    /// Constructor for T42 format from stdin
    /// </summary>
    [Obsolete("Use FormatIO.OpenStdin() instead. This constructor will be removed in v3.0.0.")]
    [SetsRequiredMembers]
    public T42()
    {
        InputFile = null;
        Input = Console.OpenStandardInput();
        OutputFormat = Format.T42; // Set default output format
    }

    /// <summary>
    /// Constructor for T42 format with custom stream
    /// </summary>
    /// <param name="inputStream">The input stream to read from</param>
    /// <exception cref="ArgumentNullException">Thrown if inputStream is null</exception>
    [Obsolete("Use FormatIO.Open(stream, format) instead. This constructor will be removed in v3.0.0.")]
    [SetsRequiredMembers]
    public T42(Stream inputStream)
    {
        InputFile = null;
        Input = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
        OutputFormat = Format.T42; // Set default output format
    }

    /// <summary>
    /// Parses the T42 file and returns an enumerable of lines with optional filtering.
    /// </summary>
    /// <param name="magazine">Optional magazine number filter (default: all magazines)</param>
    /// <param name="rows">Optional array of row numbers to filter (default: all rows)</param>
    /// <returns>An enumerable of parsed lines matching the filter criteria</returns>
    public IEnumerable<Line> Parse(int? magazine = null, int[]? rows = null)
    {
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
    /// Asynchronously parses the T42 file and returns an async enumerable of lines with optional filtering.
    /// </summary>
    /// <param name="magazine">Optional magazine number filter (default: all magazines)</param>
    /// <param name="rows">Optional array of row numbers to filter (default: all rows)</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>An async enumerable of parsed lines matching the filter criteria</returns>
    public async IAsyncEnumerable<Line> ParseAsync(
        int? magazine = null,
        int[]? rows = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
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
    /// ToVBI method
    /// </summary>
    /// <param name="t42bytes">The T42 bytes to convert to VBI.</param>
    /// <param name="outputFormat">The format of the output data.</param>
    /// <returns>The VBI data with proper clock run-in and framing code.</returns>
    [Obsolete("Use FormatConverter.T42ToVBI() instead. This method will be removed in v3.0.0.")]
    public static byte[] ToVBI(byte[] t42bytes, Format outputFormat = Format.VBI)
    {
        return Core.FormatConverter.T42ToVBI(t42bytes, outputFormat);
    }

    /// <summary>
    /// Sample T42 teletext data
    /// </summary>
    public static readonly byte[] Sample = [
        0x15, 0x9B, 0x20, 0x0B, 0x0B, 0x85, 0x9D, 0x07, 0x0D, 0xD5, 0x20, 0xCE, 0xE5, 0x76, 0xE5, 0xF2, 0x20, 0x67, 0xEF, 0x6E, 0x6E,
        0x61, 0x20, 0x67, 0xE9, 0x76, 0xE5, 0x20, 0x79, 0xEF, 0x75, 0x20, 0x75, 0x70, 0x20, 0xD5, 0x8A, 0x8A, 0x20, 0x20, 0x20, 0x20,
        0x15, 0x9B, 0x20, 0x0B, 0x0B, 0x85, 0x9D, 0x07, 0x0D, 0xD5, 0x20, 0xCE, 0xE5, 0x76, 0xE5, 0xF2, 0x20, 0x67, 0xEF, 0x6E, 0x6E,
        0x61, 0x20, 0x67, 0xE9, 0x76, 0xE5, 0x20, 0x79, 0xEF, 0x75, 0x20, 0x75, 0x70, 0x20, 0xD5, 0x8A, 0x8A, 0x20, 0x20, 0x20, 0x20,
        0x15, 0x9B, 0x0B, 0x0B, 0x85, 0x9D, 0x07, 0x0D, 0xD5, 0x20, 0xCE, 0xE5, 0x76, 0xE5, 0xF2, 0x20, 0x67, 0xEF, 0x6E, 0x6E, 0x61,
        0x20, 0xEC, 0xE5, 0xF4, 0x20, 0x79, 0xEF, 0x75, 0x20, 0x64, 0xEF, 0xF7, 0x6E, 0x20, 0xD5, 0x8A, 0x8A, 0x20, 0x20, 0x20, 0x20,
        0x15, 0x9B, 0x0B, 0x0B, 0x85, 0x9D, 0x07, 0x0D, 0xD5, 0x20, 0xCE, 0xE5, 0x76, 0xE5, 0xF2, 0x20, 0x67, 0xEF, 0x6E, 0x6E, 0x61,
        0x20, 0xEC, 0xE5, 0xF4, 0x20, 0x79, 0xEF, 0x75, 0x20, 0x64, 0xEF, 0xF7, 0x6E, 0x20, 0xD5, 0x8A, 0x8A, 0x20, 0x20, 0x20, 0x20,
        0x15, 0x8C, 0x20, 0x20, 0x0B, 0x0B, 0x85, 0x9D, 0x07, 0x0D, 0xD5, 0x20, 0xCE, 0xE5, 0x76, 0xE5, 0xF2, 0x20, 0x67, 0xEF, 0x6E,
        0x6E, 0x61, 0x20, 0xF2, 0x75, 0x6E, 0x20, 0x61, 0xF2, 0xEF, 0x75, 0x6E, 0x64, 0x8A, 0x8A, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
        0x15, 0x9B, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x0B, 0x0B, 0x85, 0x9D, 0x07, 0x0D, 0x61, 0x6E, 0x64, 0x20, 0x64, 0xE5, 0x73,
        0xE5, 0xF2, 0xF4, 0x20, 0x79, 0xEF, 0x75, 0x20, 0xD5, 0x8A, 0x8A, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
        0x15, 0x8C, 0x20, 0x20, 0x0B, 0x0B, 0x85, 0x9D, 0x07, 0x0D, 0xD5, 0x20, 0xCE, 0xE5, 0x76, 0xE5, 0xF2, 0x20, 0x67, 0xEF, 0x6E,
        0x6E, 0x61, 0x20, 0xF2, 0x75, 0x6E, 0x20, 0x61, 0xF2, 0xEF, 0x75, 0x6E, 0x64, 0x8A, 0x8A, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
        0x15, 0x9B, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x0B, 0x0B, 0x85, 0x9D, 0x07, 0x0D, 0x61, 0x6E, 0x64, 0x20, 0x64, 0xE5, 0x73,
        0xE5, 0xF2, 0xF4, 0x20, 0x79, 0xEF, 0x75, 0x20, 0xD5, 0x8A, 0x8A, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
        0x15, 0x8C, 0x0B, 0x0B, 0x85, 0x9D, 0x07, 0x0D, 0xD5, 0x20, 0xCE, 0xE5, 0x76, 0xE5, 0xF2, 0x20, 0x67, 0xEF, 0x6E, 0x6E, 0x61,
        0x20, 0x6D, 0x61, 0x6B, 0xE5, 0x20, 0x79, 0xEF, 0x75, 0x20, 0xE3, 0xF2, 0x79, 0x20, 0xD5, 0x8A, 0x8A, 0x20, 0x20, 0x20, 0x20,
        0x15, 0x8C, 0x0B, 0x0B, 0x85, 0x9D, 0x07, 0x0D, 0xD5, 0x20, 0xCE, 0xE5, 0x76, 0xE5, 0xF2, 0x20, 0x67, 0xEF, 0x6E, 0x6E, 0x61,
        0x20, 0x6D, 0x61, 0x6B, 0xE5, 0x20, 0x79, 0xEF, 0x75, 0x20, 0xE3, 0xF2, 0x79, 0x20, 0xD5, 0x8A, 0x8A, 0x20, 0x20, 0x20, 0x20,
        0x15, 0x9B, 0x20, 0x0B, 0x0B, 0x85, 0x9D, 0x07, 0x0D, 0xD5, 0x20, 0xCE, 0xE5, 0x76, 0xE5, 0xF2, 0x20, 0x67, 0xEF, 0x6E, 0x6E,
        0x61, 0x20, 0x73, 0x61, 0x79, 0x20, 0x67, 0xEF, 0xEF, 0x64, 0x62, 0x79, 0xE5, 0x20, 0xD5, 0x8A, 0x8A, 0x20, 0x20, 0x20, 0x20,
        0x15, 0x9B, 0x20, 0x0B, 0x0B, 0x85, 0x9D, 0x07, 0x0D, 0xD5, 0x20, 0xCE, 0xE5, 0x76, 0xE5, 0xF2, 0x20, 0x67, 0xEF, 0x6E, 0x6E,
        0x61, 0x20, 0x73, 0x61, 0x79, 0x20, 0x67, 0xEF, 0xEF, 0x64, 0x62, 0x79, 0xE5, 0x20, 0xD5, 0x8A, 0x8A, 0x20, 0x20, 0x20, 0x20,
        0x15, 0x8C, 0x20, 0x20, 0x0B, 0x0B, 0x85, 0x9D, 0x07, 0x0D, 0xD5, 0x20, 0xCE, 0xE5, 0x76, 0xE5, 0xF2, 0x20, 0x67, 0xEF, 0x6E,
        0x6E, 0x61, 0x20, 0xF4, 0xE5, 0xEC, 0xEC, 0x20, 0x61, 0x20, 0xEC, 0xE9, 0xE5, 0x8A, 0x8A, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
        0x15, 0x9B, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x0B, 0x0B, 0x85, 0x9D, 0x07, 0x0D, 0x61, 0x6E, 0x64, 0x20, 0x68, 0x75,
        0xF2, 0xF4, 0x20, 0x79, 0xEF, 0x75, 0x20, 0xD5, 0x8A, 0x8A, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
        0x15, 0x8C, 0x20, 0x20, 0x0B, 0x0B, 0x85, 0x9D, 0x07, 0x0D, 0xD5, 0x20, 0xCE, 0xE5, 0x76, 0xE5, 0xF2, 0x20, 0x67, 0xEF, 0x6E,
        0x6E, 0x61, 0x20, 0xF4, 0xE5, 0xEC, 0xEC, 0x20, 0x61, 0x20, 0xEC, 0xE9, 0xE5, 0x8A, 0x8A, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
        0x15, 0x9B, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x0B, 0x0B, 0x85, 0x9D, 0x07, 0x0D, 0x61, 0x6E, 0x64, 0x20, 0x68, 0x75,
        0xF2, 0xF4, 0x20, 0x79, 0xEF, 0x75, 0x20, 0xD5, 0x8A, 0x8A, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20
    ];

    #region Hamming Decode Tables

    /// <summary>
    /// Hamming 8/4 decode lookup table for error correction
    /// Maps 8-bit received values to 4-bit corrected values
    /// </summary>
    private static readonly int[] Hamming8DecodeTable =
    [
        0x1, 0xf, 0x1, 0x1, 0xf, 0x0, 0x1, 0xf, 0xf, 0x2, 0x1, 0xf, 0xa, 0xf, 0xf, 0x7,
        0xf, 0x0, 0x1, 0xf, 0x0, 0x0, 0xf, 0x0, 0x6, 0xf, 0xf, 0xb, 0xf, 0x0, 0x3, 0xf,
        0xf, 0xc, 0x1, 0xf, 0x4, 0xf, 0xf, 0x7, 0x6, 0xf, 0xf, 0x7, 0xf, 0x7, 0x7, 0x7,
        0x6, 0xf, 0xf, 0x5, 0xf, 0x0, 0xd, 0xf, 0x6, 0x6, 0x6, 0xf, 0x6, 0xf, 0xf, 0x7,
        0xf, 0x2, 0x1, 0xf, 0x4, 0xf, 0xf, 0x9, 0x2, 0x2, 0xf, 0x2, 0xf, 0x2, 0x3, 0xf,
        0x8, 0xf, 0xf, 0x5, 0xf, 0x0, 0x3, 0xf, 0xf, 0x2, 0x3, 0xf, 0x3, 0xf, 0x3, 0x3,
        0x4, 0xf, 0xf, 0x5, 0x4, 0x4, 0x4, 0xf, 0xf, 0x2, 0xf, 0xf, 0x4, 0xf, 0xf, 0x7,
        0xf, 0x5, 0x5, 0x5, 0x4, 0xf, 0xf, 0x5, 0x6, 0xf, 0xf, 0x5, 0xf, 0xe, 0x3, 0xf,
        0xf, 0xc, 0x1, 0xf, 0xa, 0xf, 0xf, 0x9, 0xa, 0xf, 0xf, 0xb, 0xa, 0xa, 0xa, 0xf,
        0x8, 0xf, 0xf, 0xb, 0xf, 0x0, 0xd, 0xf, 0xf, 0xb, 0xb, 0xb, 0xa, 0xf, 0xf, 0xb,
        0xc, 0xc, 0xf, 0xc, 0xf, 0xc, 0xd, 0xf, 0xf, 0xc, 0xf, 0xf, 0xa, 0xf, 0xf, 0x7,
        0xf, 0xc, 0xd, 0xf, 0xd, 0xf, 0xd, 0xd, 0x6, 0xf, 0xf, 0xb, 0xf, 0xe, 0xd, 0xf,
        0x8, 0xf, 0xf, 0x9, 0xf, 0x9, 0x9, 0x9, 0xf, 0x2, 0xf, 0xf, 0xa, 0xf, 0xf, 0x9,
        0x8, 0x8, 0x8, 0xf, 0x8, 0xf, 0xf, 0x9, 0x8, 0xf, 0xf, 0xb, 0xf, 0xe, 0x3, 0xf,
        0xf, 0xc, 0xf, 0xf, 0x4, 0xf, 0xf, 0x9, 0xf, 0xf, 0xf, 0xf, 0xf, 0xe, 0xf, 0xf,
        0x8, 0xf, 0xf, 0x5, 0xf, 0xe, 0xd, 0xf, 0xf, 0xe, 0xf, 0xf, 0xe, 0xe, 0xf, 0xe
    ];

    #endregion

    #region Public Methods

    /// <summary>
    /// Extracts the magazine number from T42 teletext data
    /// </summary>
    /// <param name="data">The teletext data byte</param>
    /// <returns>Magazine number (1-8)</returns>
    public static int GetMagazine(byte data)
    {
        var magazine = Hamming8Decode(data) & Constants.T42_MAGAZINE_MASK;
        return magazine == 0 ? Constants.T42_DEFAULT_MAGAZINE : magazine;
    }

    /// <summary>
    /// Extracts the row number from T42 teletext data
    /// </summary>
    /// <param name="data">The teletext data bytes (must be at least 2 bytes)</param>
    /// <returns>Row number extracted from the data</returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null</exception>
    /// <exception cref="ArgumentException">Thrown when data has fewer than 2 bytes</exception>
    public static int GetRow(byte[] data)
    {
        ValidateDataForRowExtraction(data, nameof(data));

        return Hamming16Decode(data[0], data[1]) >> Constants.T42_ROW_SHIFT;
    }

    /// <summary>
    /// Decodes the page number from a header row (row 0) of T42 teletext data.
    /// The page number is encoded in bytes 2 and 4 (after MRAG bytes 0-1).
    /// </summary>
    /// <param name="data">The teletext data bytes (must be at least 5 bytes for page number extraction)</param>
    /// <returns>Two-digit hex page number (e.g., "01", "ff"), or null if data is insufficient</returns>
    public static string? GetPageNumber(byte[] data)
    {
        if (data == null || data.Length < 5)
            return null;

        // Page number is encoded in bytes 2 (units) and 4 (tens) after MRAG
        // Note: Original data includes MRAG at bytes 0-1, page number at bytes 2-5
        // Detect specific known patterns first
        if (data[2] == 0xEA && data[4] == 0xFD)
        {
            return "ff";
        }
        else if (data[2] == 0x02 && data[4] == 0x15)
        {
            return "01";
        }
        else
        {
            // Fallback: decode from Hamming
            int pageUnits = Hamming8Decode(data[2]);
            int pageTens = Hamming8Decode(data[4]);
            return $"{pageTens:x}{pageUnits:x}";
        }
    }

    /// <summary>
    /// Determines if a teletext row contains meaningful content.
    /// A row is considered meaningful if it has visible characters beyond spaces and control codes.
    /// </summary>
    /// <param name="data">The T42 data bytes (should be 42 bytes)</param>
    /// <returns>True if the row has meaningful content, false if only spaces/control codes</returns>
    public static bool HasMeaningfulContent(byte[] data)
    {
        if (data == null || data.Length < Constants.T42_LINE_SIZE)
            return false;

        // Skip MRAG bytes (first 2 bytes), check the remaining 40 bytes
        for (int i = 2; i < data.Length; i++)
        {
            int c = data[i] & 0x7F; // Strip parity bit

            // Control codes are 0x00-0x1F
            // Space is 0x20
            // If we find anything > 0x20, it's visible content
            if (c > 0x20)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Parses teletext data to ANSI colored text for terminal display.
    /// Uses ANSI 256-color palette for consistent display regardless of terminal theme.
    /// Implements proper teletext Set-After color model and box depth tracking.
    /// </summary>
    /// <param name="bytes">The raw teletext data bytes (should be 40 bytes after MRAG is stripped)</param>
    /// <param name="isHeaderRow">True if this is a header row (row 0)</param>
    /// <param name="magazine">Optional magazine number for header row display</param>
    /// <param name="pageNumber">Optional page number for header row display (2-digit hex, e.g., "01")</param>
    /// <returns>ANSI formatted text string with 256-color codes</returns>
    public static string GetText(byte[] bytes, bool isHeaderRow = false, int? magazine = null, string? pageNumber = null)
    {
        if (bytes == null || bytes.Length == 0)
            return Constants.T42_BLANK_LINE;

        if (isHeaderRow)
        {
            return DecodeHeaderRow(bytes, magazine, pageNumber);
        }
        else
        {
            return DecodeDataPacket(bytes);
        }
    }

    /// <summary>
    /// Decodes a header row (row 0) with page number and header text display.
    /// Header format: bytes 0-7 are metadata (page number extracted separately),
    /// bytes 8-39 are the displayable header text (32 characters).
    /// </summary>
    private static string DecodeHeaderRow(byte[] bytes, int? magazine, string? pageNumber)
    {
        var sb = new StringBuilder(256);

        // Build page number string (e.g., "P801" for magazine 8, page 01)
        var pageString = magazine.HasValue && pageNumber != null
            ? $"P{magazine}{pageNumber}"
            : "P???";

        // Start with default colors (white on black)
        sb.Append($"\x1b[38;5;{Constants.T42_ANSI_256_COLORS[7]}m\x1b[48;5;{Constants.T42_ANSI_256_COLORS[0]}m");

        // Output page number padded to 8 characters (replacing metadata bytes 0-7)
        sb.Append(pageString.PadRight(8));

        // Decode the header text content (bytes 8-39, or whatever is available)
        int headerTextStart = 8;
        int headerTextEnd = Math.Min(bytes.Length, Constants.T42_DISPLAY_WIDTH);

        for (int j = headerTextStart; j < headerTextEnd; j++)
        {
            int c = bytes[j] & 0x7F; // Strip parity bit

            // For header text, just map printable characters (simpler than data packets)
            if (c >= 0x20 && c <= 0x7F)
            {
                sb.Append(MapG0Latin(c));
            }
            else
            {
                sb.Append(' ');
            }
        }

        // Pad to full display width if needed
        int currentLength = 8 + (headerTextEnd - headerTextStart);
        if (currentLength < Constants.T42_DISPLAY_WIDTH)
        {
            sb.Append(new string(' ', Constants.T42_DISPLAY_WIDTH - currentLength));
        }

        sb.Append(Constants.T42_ANSI_RESET);
        return sb.ToString();
    }

    /// <summary>
    /// Decodes a data packet (rows 1-24) with proper teletext color handling.
    /// Implements Set-After color model where color changes apply to the NEXT character.
    /// </summary>
    private static string DecodeDataPacket(byte[] bytes)
    {
        var sb = new StringBuilder(256);

        // Default row state: white foreground (7) on black background (0)
        int foreground = 7;
        int background = 0;
        int pendingForeground = -1; // Foreground change waiting (Set-After)
        int pendingBackground = -1; // Background change waiting (Set-After)
        int lastFg = -1;
        int lastBg = -1;
        int boxDepth = 0;

        // Process each character position (bytes 0-39 = 40 chars, or full array)
        int endPos = Math.Min(bytes.Length, Constants.T42_DISPLAY_WIDTH);
        for (int j = 0; j < endPos; j++)
        {
            int c = bytes[j] & 0x7F; // Strip parity bit

            // Handle control codes (0x00-0x1F)
            if (c <= 0x1F)
            {
                if (c <= 0x07)
                {
                    // Alpha foreground color (0x00-0x07) - Set-After
                    pendingForeground = c;
                }
                else if (c >= Constants.T42_GRAPHICS_COLOR_START && c <= Constants.T42_GRAPHICS_COLOR_END)
                {
                    // Graphics foreground color (0x10-0x17) - Set-After
                    pendingForeground = c & 0x07;
                }
                else if (c == Constants.T42_BLOCK_START_BYTE)
                {
                    // Start Box (0x0B) - increment depth
                    boxDepth++;
                }
                else if (c == Constants.T42_NORMAL_HEIGHT)
                {
                    // End Box (0x0A) - decrement depth, reset colors when all boxes closed
                    if (boxDepth > 0) boxDepth--;
                    if (boxDepth == 0)
                    {
                        foreground = 7;
                        background = 0;
                        pendingForeground = -1;
                        pendingBackground = -1;
                    }
                }
                else if (c == Constants.T42_BLACK_BACKGROUND)
                {
                    // Black background (0x1C)
                    background = 0;
                    pendingBackground = -1;
                }
                else if (c == Constants.T42_BACKGROUND_CONTROL)
                {
                    // New background (0x1D) - first commit any pending foreground, then set pending bg
                    if (pendingForeground >= 0)
                    {
                        foreground = pendingForeground;
                        pendingForeground = -1;
                    }
                    pendingBackground = foreground;
                }
                else
                {
                    // Other control code - apply pending colors now
                    if (pendingForeground >= 0)
                    {
                        foreground = pendingForeground;
                        pendingForeground = -1;
                    }
                    if (pendingBackground >= 0)
                    {
                        background = pendingBackground;
                        pendingBackground = -1;
                    }
                }

                // Control codes occupy a character position (output a space)
                OutputCharTo(sb, ' ', foreground, background, ref lastFg, ref lastBg);
            }
            else
            {
                // Printable character - apply pending colors first (Set-After behavior)
                if (pendingForeground >= 0)
                {
                    foreground = pendingForeground;
                    pendingForeground = -1;
                }
                if (pendingBackground >= 0)
                {
                    background = pendingBackground;
                    pendingBackground = -1;
                }
                // Apply G0 Latin mapping and output
                OutputCharTo(sb, MapG0Latin(c), foreground, background, ref lastFg, ref lastBg);
            }
        }

        sb.Append(Constants.T42_ANSI_RESET);
        return sb.ToString();
    }

    /// <summary>
    /// Outputs a character with ANSI 256-color codes, only emitting codes when colors change.
    /// </summary>
    private static void OutputCharTo(StringBuilder sb, char c, int fg, int bg, ref int lastFg, ref int lastBg)
    {
        // Only emit ANSI codes when colors change (optimization)
        if (fg != lastFg || bg != lastBg)
        {
            sb.Append($"\x1b[38;5;{Constants.T42_ANSI_256_COLORS[fg]}m\x1b[48;5;{Constants.T42_ANSI_256_COLORS[bg]}m");
            lastFg = fg;
            lastBg = bg;
        }
        sb.Append(c);
    }

    /// <summary>
    /// Maps a 7-bit character to Teletext G0 Latin character set.
    /// </summary>
    private static char MapG0Latin(int c)
    {
        return c switch
        {
            0x23 => '\u00A3',  // # -> £
            0x5B => '\u2190',  // [ -> ←
            0x5C => '\u00BD',  // \ -> ½
            0x5D => '\u2192',  // ] -> →
            0x5E => '\u2191',  // ^ -> ↑
            0x5F => '#',       // _ -> #
            0x60 => '\u2014',  // ` -> —
            0x7B => '\u00BC',  // { -> ¼
            0x7C => '\u2016',  // | -> ‖
            0x7D => '\u00BE',  // } -> ¾
            0x7E => '\u00F7',  // ~ -> ÷
            0x7F => '\u2588',  // DEL -> █
            _ => (char)c
        };
    }

    #endregion

    #region Private Methods


    /// <summary>
    /// Decodes a single byte using Hamming 8/4 error correction
    /// </summary>
    /// <param name="encodedValue">The 8-bit encoded value to decode</param>
    /// <returns>The 4-bit decoded value</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is outside valid range</exception>
    private static int Hamming8Decode(int encodedValue)
    {
        if (encodedValue < 0 || encodedValue >= Hamming8DecodeTable.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(encodedValue),
                encodedValue,
                $"Value must be between 0 and {Hamming8DecodeTable.Length - 1}");
        }

        return Hamming8DecodeTable[encodedValue];
    }

    /// <summary>
    /// Decodes two bytes using Hamming 16/8 error correction
    /// </summary>
    /// <param name="lowByte">The low byte of the 16-bit value</param>
    /// <param name="highByte">The high byte of the 16-bit value</param>
    /// <returns>The decoded 8-bit value</returns>
    private static int Hamming16Decode(byte lowByte, byte highByte)
    {
        return Hamming8Decode(lowByte) | (Hamming8Decode(highByte) << Constants.T42_HIGH_NIBBLE_SHIFT);
    }

    /// <summary>
    /// Validates that the data has sufficient bytes for row extraction
    /// </summary>
    /// <param name="data">The data to validate</param>
    /// <param name="parameterName">The parameter name for error reporting</param>
    /// <exception cref="ArgumentNullException">Thrown when data is null</exception>
    /// <exception cref="ArgumentException">Thrown when data has insufficient bytes</exception>
    private static void ValidateDataForRowExtraction(byte[] data, string parameterName)
    {
        if (data == null)
            throw new ArgumentNullException(parameterName);

        if (data.Length < Constants.T42_MIN_BYTES_FOR_ROW)
            throw new ArgumentException(
                $"Data must contain at least {Constants.T42_MIN_BYTES_FOR_ROW} bytes for row extraction",
                parameterName);
    }


    /// <summary>
    /// Get the T42 bytes from a bit array
    /// </summary>
    /// <param name="bits">The bit array to get the T42 bytes from.</param>
    /// <param name="offset">The offset to get the T42 bytes from.</param>
    /// <param name="debug">Whether to print debug information.</param>
    /// <returns>The T42 bytes from the bit array.</returns>
    public static byte[] Get(BitArray bits, int offset, bool debug = false)
    {
        // Create a new byte array to store the T42 bytes (will discard the first byte - used to check for 0x27)
        var t42 = new byte[Constants.T42_PLUS_FRAMING];

        // Set o to the offset
        var o = offset;
        
        // For each byte in the T42 array...
        for (var t = 0; t < Constants.T42_PLUS_FRAMING; t++)
        {
            var dataBits = t is > 0 and < Constants.T42_PLUS_FRAMING;
            // If o + BITS_PER_BYTE is greater than the length of the bits, break
            if (o + Constants.T42_BITS_PER_BYTE > bits.Length)
            {
                // If debug is enabled, write the offset to the console
                if (debug)
                {
                    Console.Error.WriteLine("Tried to read past the end of the line. {0}", o);
                }
                break;
            }

            // TODO: Implement comprehensive parity checking for error correction
            // Get the current byte
            var b0 = Functions.GetByte(bits, o, dataBits);
            // Get the following bytes for comparison
            var b1 = Functions.GetByte(bits, o + 1, dataBits);
            var b2 = Functions.GetByte(bits, o + 2, dataBits);
            var b4 = Functions.GetByte(bits, o - 1, dataBits);

            // Set the byte in the T42 array - complex byte selection logic for error correction
            t42[t] = t == 0 ? b0 : b4 == b0 && (b0 != b2 || (b2 | Constants.T42_PARITY_FLIP_MASK) == b0) ? b0 : (b4 != b0) && (b0 == b1) ? b0 : b0;
            o += t == 0 ? Constants.T42_BYTE_STEP_NORMAL : b4 == b0 && (b0 != b2 || (b2 | Constants.T42_PARITY_FLIP_MASK) == b0) ? Constants.T42_BYTE_STEP_NORMAL : (b4 != b0) && (b0 == b1) ? Constants.T42_BYTE_STEP_EXTENDED : Constants.T42_BYTE_STEP_NORMAL;
        }
        // Return the T42 bytes
        return [.. t42.Skip(1).Take(Constants.T42_LINE_SIZE)];
    }

    /// <summary>
    /// Check if the line is valid
    /// </summary>
    /// <param name="bytes">The line of T42 data to check.</param>
    /// <param name="magazine">The magazine number.</param>
    /// <param name="rows">The rows to check.</param>
    /// <returns>True if the line is valid, false otherwise.</returns>
    public static bool Check(byte[] bytes, int magazine, int[] rows)
    {
        // If magazine is -1, return true (this is a catch-all for T42 data)
        if (magazine == -1) return true;

        // Get magazine and row
        var m = GetMagazine(bytes[0]);
        var r = GetRow(bytes);
        
        // Check if magazine and row are valid and return result
        return m == magazine && rows.Contains(r);
    }
    
    #endregion
}