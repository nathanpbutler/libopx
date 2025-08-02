using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.libopx.Formats;

public class T42 : IDisposable
{
    public FileInfo? InputFile { get; set; } = null; // If null, read from stdin
    public FileInfo? OutputFile { get; set; } = null; // If null, write to stdout
    private Stream? _outputStream;
    public required Stream Input { get; set; }
    public Stream Output => _outputStream ??= OutputFile == null ? Console.OpenStandardOutput() : OutputFile.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
    public Format InputFormat { get; set; } = Format.T42; // Default input format is T42
    public Format? OutputFormat { get; set; } = Format.T42; // Default output format is T42
    public int LineLength => Constants.T42_LINE_SIZE; // Length of the T42 line
    public Function Function { get; set; } = Function.Filter; // Default function is Filter
    public int LineCount { get; set; } = 2; // For Timecode incementation, default is 2 lines

    /// <summary>
    /// Valid outputs: t42/vbi/vbi_double
    /// </summary>
    public static readonly Format[] ValidOutputs = [Format.T42, Format.VBI, Format.VBI_DOUBLE];

    /// <summary>
    /// Constructor for T42 format from file
    /// </summary>
    /// <param name="inputFile">Path to the input T42 file</param>
    /// <exception cref="FileNotFoundException">Thrown if the specified file does not exist</exception>
    [SetsRequiredMembers]
    public T42(string inputFile)
    {
        InputFile = new FileInfo(inputFile);

        if (!InputFile.Exists)
        {
            throw new FileNotFoundException("Input file not found", InputFile.FullName);
        }

        Input = InputFile.OpenRead();
        Input.Seek(0, SeekOrigin.Begin); // Reset stream position to the beginning
    }

    /// <summary>
    /// Constructor for T42 format from stdin
    /// </summary>
    [SetsRequiredMembers]
    public T42()
    {
        InputFile = null;
        Input = Console.OpenStandardInput();
    }

    /// <summary>
    /// Constructor for T42 format with custom stream
    /// </summary>
    [SetsRequiredMembers]
    public T42(Stream inputStream)
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

    public IEnumerable<Line> Parse(int? magazine = 8, int[]? rows = null)
    {
        // Use default rows if not specified
        rows ??= Constants.DEFAULT_ROWS;

        // If OutputFormat is not set, use the provided outputFormat
        var outputFormat = OutputFormat ?? Format.T42;

        int lineNumber = 0;
        var timecode = new Timecode(0);
        var t42Buffer = new byte[LineLength];

        while (Input.Read(t42Buffer, 0, LineLength) == LineLength)
        {
            // Increment timecode if LineCount is reached
            if (lineNumber % LineCount == 0 && lineNumber != 0)
            {
                timecode = timecode.GetNext();
            }
            
            // Create a basic Line object for T42 data
            var line = new Line()
            {
                LineNumber = lineNumber,
                Data = [.. t42Buffer],
                Length = LineLength,
                SampleCoding = 0x31, // T42 sample coding
                SampleCount = LineLength,
                LineTimecode = timecode,
            };

            // Extract T42 metadata
            if (t42Buffer.Length >= Constants.T42_LINE_SIZE && t42Buffer.Any(b => b != 0))
            {
                line.Magazine = GetMagazine(t42Buffer[0]);
                line.Row = GetRow([.. t42Buffer.Take(2)]);
                line.Text = GetText([.. t42Buffer.Skip(2)]);
            }
            else
            {
                // Empty data, skip this line
                lineNumber++;
                continue;
            }

            // Process the T42 data based on output format
            if (outputFormat == Format.VBI || outputFormat == Format.VBI_DOUBLE)
            {
                try
                {
                    // Convert T42 to VBI using the corrected method
                    var vbiData = ToVBI(t42Buffer, outputFormat);

                    // Update line properties for VBI
                    line.Data = vbiData;
                    line.Length = vbiData.Length;
                    line.SampleCoding = outputFormat == Format.VBI_DOUBLE ? 0x32 : 0x31;
                    line.SampleCount = vbiData.Length;

                    // For VBI output, clear T42-specific metadata
                    line.Magazine = -1;
                    line.Row = -1;
                    line.Text = Constants.T42_BLANK_LINE;
                }
                catch
                {
                    // If conversion fails, skip this line
                    lineNumber++;
                    continue;
                }
            }

            // Apply filtering if specified
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

            yield return line;
            lineNumber++;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _outputStream?.Dispose();
        Input?.Dispose();
    }

    /// <summary>
    /// ToVBI method
    /// </summary>
    /// <param name="t42bytes">The T42 bytes to convert to VBI.</param>
    /// <param name="outputFormat">The format of the output data.</param>
    /// <returns>The VBI data with proper clock run-in and framing code.</returns>
    public static byte[] ToVBI(byte[] t42bytes, Format outputFormat = Format.VBI)
    {
        // TODO: Validate outputFormat?

        // If T42 data is not the correct size, return blank VBI
        if (t42bytes.Length != Constants.T42_LINE_SIZE)
        {
            return new byte[outputFormat == Format.VBI_DOUBLE ? Constants.VBI_DOUBLE_LINE_SIZE : Constants.VBI_LINE_SIZE];
        }

        // Prepend the clock run-in and framing code to the T42 data
        byte[] lineData = [0x55, 0x55, 0x27, .. t42bytes];

        // Create a new BitArray from the line data
        BitArray bits = new(lineData);

        // Create a new byte array with a length of 360 (45 * 8)
        var bytes = new byte[Constants.VBI_BITS_SIZE];

        // For each bit in the BitArray...
        for (var b = 0; b < Constants.VBI_BITS_SIZE; b++)
        {
            // Set the byte at the current index to the Constants.VBI_HIGH_VALUE or Constants.VBI_LOW_VALUE value
            bytes[b] = bits[b] ? Constants.VBI_HIGH_VALUE : Constants.VBI_LOW_VALUE;
        }

        // Create a new byte array with a length of Constants.VBI_RESIZE_BYTES
        var resized = Constants.VBI_RESIZE_BYTES;

        // For each byte in the Constants.VBI_RESIZE_BYTES...
        for (var i = 0; i < Constants.VBI_RESIZE_SIZE; i++)
        {
            // Calculate the original position
            var originalPosition = i * Constants.VBI_SCALE;
            // Calculate the left pixel
            var leftPixel = (int)originalPosition;
            // Calculate the right pixel
            var rightPixel = Math.Min(leftPixel + 1, Constants.VBI_BITS_SIZE - 1);
            // Calculate the right weight
            var rightWeight = originalPosition - leftPixel;
            // Calculate the left weight
            var leftWeight = 1f - rightWeight;

            // Calculate the resized byte
            resized[i] = (byte)(bytes[leftPixel] * leftWeight + bytes[rightPixel] * rightWeight);
        }

        //var resized = pad.Take(6).Concat(resized).Concat(pad).Take(720).ToArray();

        resized = Constants.VBI_PADDING_BYTES.Take(Constants.VBI_PAD_START).Concat(resized).Concat(Constants.VBI_PADDING_BYTES).Take(Constants.VBI_LINE_SIZE).ToArray();

        if (outputFormat == Format.VBI_DOUBLE)
        {
            resized = Functions.Double(resized);
        }

        return resized;
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
    /// Parses teletext data to ANSI colored text for terminal display
    /// </summary>
    /// <param name="bytes">The raw teletext data bytes</param>
    /// <returns>ANSI formatted text string, or null if no valid text block found</returns>
    public static string GetText(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return Constants.T42_BLANK_LINE;

        // Strip parity bits (keep only 7 bits)
        var processedBytes = bytes.Select(b => (byte)(b & 0x7F)).ToArray();

        // Find the start of the text block
        var blockStart = FindBlockStart(processedBytes);
        if (blockStart == -1)
            return Constants.T42_BLANK_LINE; // No valid block found

        // Create result array for processed characters
        var result = new string[processedBytes.Length];

        // Process the text content
        ProcessTextContent(processedBytes, result);

        // Apply teletext control characters and formatting
        ApplyTeletextFormatting(processedBytes, result, blockStart);

        // Combine with ANSI formatting
        return Constants.T42_DEFAULT_COLORS + string.Join("", result) + Constants.T42_ANSI_RESET;
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
    /// Finds the start of a teletext block by looking for the block start sequence
    /// </summary>
    /// <param name="bytes">The processed teletext data</param>
    /// <returns>Index of block start, or -1 if not found</returns>
    private static int FindBlockStart(byte[] bytes)
    {
        for (var i = 0; i < bytes.Length - 1; i++)
        {
            if (bytes[i] == Constants.T42_BLOCK_START_BYTE && bytes[i + 1] == Constants.T42_BLOCK_START_BYTE)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Finds the background control character position
    /// </summary>
    /// <param name="bytes">The processed teletext data</param>
    /// <returns>Index of background control, or -1 if not found</returns>
    private static int FindBackgroundControl(byte[] bytes)
    {
        for (var i = bytes.Length - 1; i >= 0; i--)
        {
            if (bytes[i] == Constants.T42_BACKGROUND_CONTROL)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Processes the basic text content, converting displayable characters
    /// </summary>
    /// <param name="bytes">The processed teletext data</param>
    /// <param name="result">The result array to populate</param>
    private static void ProcessTextContent(byte[] bytes, string[] result)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];

            // Handle displayable ASCII characters (32-126)
            if (b >= 32 && b <= 126)
            {
                // Use TeletextCharset.cs dictionary to map teletext characters
                result[i] = TeletextCharsets.GetUnicodeChar("G0", b).ToString();
            }
            else
            {
                result[i] = " ";
            }
        }
    }

    /// <summary>
    /// Applies teletext formatting including colors and control characters
    /// </summary>
    /// <param name="bytes">The processed teletext data</param>
    /// <param name="result">The result array to modify</param>
    /// <param name="blockStart">The index where the text block starts</param>
    private static void ApplyTeletextFormatting(byte[] bytes, string[] result, int blockStart)
    {
        var backgroundPos = FindBackgroundControl(bytes);

        // Apply background colors
        ApplyBackgroundColors(bytes, result, backgroundPos, blockStart);

        // Apply foreground colors
        ApplyForegroundColors(bytes, result, backgroundPos);

        // Handle normal height controls
        ApplyNormalHeightControls(bytes, result);
    }

    /// <summary>
    /// Applies background color formatting
    /// </summary>
    /// <param name="bytes">The processed teletext data</param>
    /// <param name="result">The result array to modify</param>
    /// <param name="backgroundPos">Position of background control character</param>
    /// <param name="blockStart">Position of block start</param>
    private static void ApplyBackgroundColors(byte[] bytes, string[] result, int backgroundPos, int blockStart)
    {
        if (backgroundPos <= -1) return;

        if (blockStart < backgroundPos && backgroundPos + 1 < bytes.Length && backgroundPos > 0)
        {
            // Set background and foreground colors
            var bgColor = GetAnsiColor(bytes[backgroundPos - 1], true);
            var fgColor = GetAnsiColor(bytes[backgroundPos + 1], false);

            if (backgroundPos + 2 < result.Length)
            {
                result[backgroundPos + 2] = bgColor + fgColor;
            }
        }
        else if (backgroundPos + 1 < bytes.Length)
        {
            var colorCode = backgroundPos == 0 ?
                GetAnsiColor(bytes[backgroundPos], true) :
                GetAnsiColor(bytes[backgroundPos - 1], true) + GetAnsiColor(bytes[backgroundPos + 1], false);

            if (blockStart < result.Length)
            {
                result[blockStart] = colorCode;
            }
        }
    }

    /// <summary>
    /// Applies foreground color formatting
    /// </summary>
    /// <param name="bytes">The processed teletext data</param>
    /// <param name="result">The result array to modify</param>
    /// <param name="backgroundPos">Position of background control character</param>
    private static void ApplyForegroundColors(byte[] bytes, string[] result, int backgroundPos)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            // Skip background control area
            if (i == backgroundPos)
            {
                i += 2;
                continue;
            }

            // Apply foreground colors for control codes 0-7
            if (bytes[i] <= 7 && i < result.Length)
            {
                result[i] = GetAnsiColor(bytes[i], false);
            }
        }
    }

    /// <summary>
    /// Applies normal height control formatting
    /// </summary>
    /// <param name="bytes">The processed teletext data</param>
    /// <param name="result">The result array to modify</param>
    private static void ApplyNormalHeightControls(byte[] bytes, string[] result)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == Constants.T42_NORMAL_HEIGHT)
            {
                // Handle double normal height control
                if (i + 1 < bytes.Length && bytes[i + 1] == Constants.T42_NORMAL_HEIGHT)
                {
                    if (i + 1 < result.Length)
                    {
                        result[i + 1] = Constants.T42_DEFAULT_COLORS + " ";
                    }
                    i++; // Skip next byte
                }
                else if (i < result.Length)
                {
                    result[i] = Constants.T42_DEFAULT_COLORS + " ";
                }
            }
        }
    }

    /// <summary>
    /// Generates ANSI color escape sequence
    /// </summary>
    /// <param name="colorCode">The color code (0-7)</param>
    /// <param name="isBackground">True for background color, false for foreground</param>
    /// <returns>ANSI color escape sequence</returns>
    private static string GetAnsiColor(int colorCode, bool isBackground)
    {
        var baseCode = isBackground ? 40 : 30;
        return $"\x1b[{colorCode + baseCode}m" + (isBackground ? "" : " ");
    }

    /// <summary>
    /// Checks if a byte represents a teletext special character
    /// </summary>
    /// <param name="b">The byte to check</param>
    /// <returns>True if it's a special character</returns>
    private static bool IsTeletextSpecialCharacter(byte b)
    {
        // Common teletext special characters that should be displayed as specific symbols
        return b switch
        {
            0x85 => true, // Start box
            0x8A => true, // End box  
            0x9B => true, // CSI
            0x9D => true, // Operating System Command
            0xD5 => true, // Special teletext character
            0xCE => true, // Special teletext character
            _ => false
        };
    }

    /// <summary>
    /// Gets the display representation of a teletext special character
    /// </summary>
    /// <param name="b">The special character byte</param>
    /// <returns>Display representation</returns>
    private static string GetTeletextSpecialCharacter(byte b)
    {
        return b switch
        {
            0x85 => "┌", // Start box - top-left corner
            0x8A => "┐", // End box - top-right corner
            0x9B => " ", // CSI - control sequence introducer (non-printable)
            0x9D => " ", // Operating System Command (non-printable)
            0xD5 => "█", // Block character
            0xCE => "N", // Could be a special N character
            _ => " "
        };
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