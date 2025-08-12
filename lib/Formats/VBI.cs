using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.libopx.Formats;

public class VBI : IDisposable
{
    public FileInfo? InputFile { get; set; } = null; // If null, read from stdin
    public FileInfo? OutputFile { get; set; } = null; // If null, write to stdout
    private Stream? _outputStream;
    public required Stream Input { get; set; }
    public Stream Output => _outputStream ??= OutputFile == null ? Console.OpenStandardOutput() : OutputFile.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
    public Format InputFormat { get; set; } = Format.VBI; // Default input format is VBI
    public Format? OutputFormat { get; set; } = Format.T42; // Default output format is T42
    public int LineLength => InputFormat == Format.VBI_DOUBLE ? Constants.VBI_DOUBLE_LINE_SIZE : Constants.VBI_LINE_SIZE; // Length of the line based on input format
    public static readonly Format[] ValidOutputs = [Format.VBI, Format.VBI_DOUBLE, Format.T42];
    public Function Function { get; set; } = Function.Filter; // Default function is Filter
    public int LineCount { get; set; } = 2; // For Timecode incementation, default is 2 lines

    /// <summary>
    /// Constructor for VBI format from file
    /// </summary>
    /// <param name="inputFile"></param>
    /// <param name="vbiType"></param>
    /// <exception cref="FileNotFoundException"></exception>
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
    }

    /// <summary>
    /// Constructor for VBI format from stdin
    /// </summary>
    [SetsRequiredMembers]
    public VBI()
    {
        InputFile = null;
        Input = Console.OpenStandardInput();
    }
    
    /// <summary>
    /// Constructor for VBI format with custom stream
    /// </summary>
    /// <param name="inputStream">The input stream to read from</param>
    /// <param name="vbiType">The VBI format type</param>
    [SetsRequiredMembers]
    public VBI(Stream inputStream, Format? vbiType = Format.VBI)
    {
        InputFile = null;
        Input = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
        InputFormat = vbiType ?? Format.VBI;
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

    public IEnumerable<Line> Parse(int? magazine = 8, int[]? rows = null)
    {
        // Use default rows if not specified
        rows ??= Constants.DEFAULT_ROWS;

        // If OutputFormat is not set, use the provided outputFormat
        var outputFormat = OutputFormat ?? Format.T42;

        int lineNumber = 0;
        var timecode = new Timecode(0);
        var vbiBuffer = new byte[LineLength];

        while (Input.Read(vbiBuffer, 0, LineLength) == LineLength)
        {

            // Increment timecode if LineCount is reached
            if (lineNumber % LineCount == 0 && lineNumber != 0)
            {
                timecode = timecode.GetNext();
            }

            // Create a basic Line object for VBI data
            var line = new Line()
            {
                LineNumber = lineNumber,
                Data = [.. vbiBuffer],
                Length = LineLength,
                SampleCoding = InputFormat == Format.VBI_DOUBLE ? 0x32 : 0x31,
                SampleCount = LineLength,
                LineTimecode = timecode,
            };

            // Process the VBI data based on output format
            if (outputFormat == Format.T42)
            {
                try
                {
                    // Convert VBI to T42 using the same approach as BIN/MXF parsers
                    var t42Data = ToT42(vbiBuffer);

                    // Update line properties for T42
                    line.Data = t42Data;
                    line.Length = t42Data.Length;
                    line.SampleCoding = 0x31; // T42 sample coding
                    line.SampleCount = t42Data.Length;

                    // Extract T42 metadata if conversion successful
                    if (t42Data.Length >= Constants.T42_LINE_SIZE && t42Data.Any(b => b != 0))
                    {
                        line.Magazine = T42.GetMagazine(t42Data[0]);
                        line.Row = T42.GetRow([.. t42Data.Take(2)]);
                        line.Text = T42.GetText([.. t42Data.Skip(2)]);
                    }
                    else
                    {
                        // Conversion resulted in blank data, skip this line
                        lineNumber++;
                        continue;
                    }
                }
                catch
                {
                    // If conversion fails, skip this line
                    lineNumber++;
                    continue;
                }
            }
            else if (outputFormat == Format.VBI_DOUBLE && LineLength == Constants.VBI_LINE_SIZE)
            {
                line.Data = Functions.Double(vbiBuffer);
                line.Magazine = -1; // No magazine for VBI
                line.Row = -1; // No row for VBI
                line.Text = Constants.T42_BLANK_LINE; // Default blank line text
            }
            else
            {
                // For VBI output, keep original data
                line.Magazine = -1;
                line.Row = -1;
                line.Text = Constants.T42_BLANK_LINE;
            }

            // Apply filtering if specified and conversion was successful
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

    /// <summary>
    /// Disposes the resources used by the VBI parser.
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

    // ToT42 method
    public static byte[] ToT42(byte[] lineData, bool debug = false)
    {
        if (lineData.Length != Constants.VBI_LINE_SIZE && lineData.Length != Constants.VBI_DOUBLE_LINE_SIZE)
        {
            throw new ArgumentException($"Line data must be {Constants.VBI_LINE_SIZE} or {Constants.VBI_DOUBLE_LINE_SIZE} bytes long.");
        }
        // Double the line data
        var newLine = lineData.Length == Constants.VBI_DOUBLE_LINE_SIZE ? lineData : Functions.Double(lineData);

        // Normalise the line data
        var normalised = Functions.Normalise(newLine);

        // Create a BitArray from the normalised line
        var bits = Functions.GetBits(normalised);

        // Get the offset of the line
        var offset = Functions.GetOffset(bits);

        // If the offset is not within valid range, return a blank byte array
        if (offset is <= -1 or >= Constants.VBI_MAX_OFFSET_RANGE)
        {
            // Return a blank byte array
            return new byte[Constants.T42_LINE_SIZE];
        }

        // Get the T42 bytes from the line
        var t42 = T42.Get(bits, offset, debug);

        return t42;
    }
}
