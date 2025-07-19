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
    public LineFormat InputFormat { get; set; } = LineFormat.VBI; // Default input format is VBI
    public LineFormat? OutputFormat { get; set; } = LineFormat.T42; // Default output format is T42
    public int LineLength => InputFormat == LineFormat.VBI_DOUBLE ? Constants.VBI_DOUBLE_LINE_SIZE : Constants.VBI_LINE_SIZE; // Length of the line based on input format
    public static readonly LineFormat[] ValidOutputs = [LineFormat.VBI, LineFormat.VBI_DOUBLE, LineFormat.T42];
    public Function Function { get; set; } = Function.Filter; // Default function is Filter
    public int LineCount { get; set; } = 2; // For Timecode incementation, default is 2 lines

    /// <summary>
    /// Constructor for VBI format from file
    /// </summary>
    /// <param name="inputFile"></param>
    /// <param name="vbiType"></param>
    /// <exception cref="FileNotFoundException"></exception>
    [SetsRequiredMembers]
    public VBI(string inputFile, LineFormat? vbiType = LineFormat.VBI)
    {
        InputFile = new FileInfo(inputFile);

        if (!InputFile.Exists)
        {
            throw new FileNotFoundException("The specified VBI file does not exist.", inputFile);
        }
        // If vbiType is not specified, default determine from file extension
        InputFormat = vbiType ?? (InputFile.Extension.ToLower() switch
        {
            ".vbi" => LineFormat.VBI,
            ".vbid" => LineFormat.VBI_DOUBLE,
            _ => LineFormat.VBI // Default to VBI if unknown
        });

        Input = InputFile.OpenRead();
        Input.Seek(0, SeekOrigin.Begin); // Reset stream position to the beginning
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
    public VBI(Stream inputStream, LineFormat? vbiType = LineFormat.VBI)
    {
        InputFile = null;
        Input = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
        InputFormat = vbiType ?? LineFormat.VBI;
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
        var outputFormat = OutputFormat ?? LineFormat.T42;

        int lineNumber = 0;
        var timecode = new Timecode(0);
        var vbiBuffer = new byte[LineLength];

        while (Input.Read(vbiBuffer, 0, LineLength) == LineLength)
        {
            // Increment timecode if LineCount is reached
            if (lineNumber % LineCount == 0)
            {
                timecode = timecode.GetNext();
            }

            // Create a basic Line object for VBI data
            var line = new Line()
            {
                LineNumber = lineNumber,
                Data = [.. vbiBuffer],
                Length = LineLength,
                SampleCoding = InputFormat == LineFormat.VBI_DOUBLE ? 0x32 : 0x31,
                SampleCount = LineLength,
                LineTimecode = timecode,
            };

            // Process the VBI data based on output format
            if (outputFormat == LineFormat.T42)
            {
                try
                {
                    // Convert VBI to T42
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
            else
            {
                // For VBI output, keep original data
                line.Magazine = -1;
                line.Row = -1;
                line.Text = Constants.T42_BLANK_LINE;
            }

            // Apply filtering if specified and conversion was successful
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

            yield return line;
            lineNumber++;
        }
    }

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

    #region Legacy Methods
    /// <summary>
    /// Process VBI data from a file
    /// </summary>
    /// <param name="input">The input file to process.</param>
    /// <param name="output">The output file to write the processed data to.</param>
    /// <param name="inputFormat">The format of the input file.</param>
    /// <param name="outputFormat">The format of the output file.</param>
    /// <param name="magazine">The magazine number.</param>
    /// <param name="rows">The rows to process.</param>
    /// <param name="debug">Whether to print debug information.</param>
    /// <returns>The exit code of the operation.</returns>
    public static async Task<int> Process(Stream input, Stream output, LineFormat inputFormat, LineFormat outputFormat, int magazine, int[] rows, bool debug = false)
    {
        // Validate output format
        if (!ValidOutputs.Contains(outputFormat))
        {
            throw new ArgumentException($"Invalid output format: {outputFormat}. Valid formats are: {string.Join(", ", ValidOutputs)}");
        }

        // Read chunks of up to VBI_LINE_SIZE (or VBI_DOUBLE_LINE_SIZE if VBI_DOUBLE) bytes from input until EOF
        byte[] buffer = inputFormat == LineFormat.VBI_DOUBLE ? new byte[Constants.VBI_DOUBLE_LINE_SIZE] : new byte[Constants.VBI_LINE_SIZE];
        int bytesRead;
        while ((bytesRead = await input.ReadAsync(buffer)) > 0)
        {
            // Only process the actual bytes read, not the full buffer
            byte[] actualData = new byte[bytesRead];
            Array.Copy(buffer, actualData, bytesRead);
            await Line(actualData, output, inputFormat, outputFormat, magazine, rows);
        }
        return 0;
    }

    /// <summary>
    /// Process a line of VBI data
    /// </summary>
    /// <param name="lineData">The input data to process.</param>
    /// <param name="inputFormat">The format of the input data.</param>
    /// <param name="outputFormat">The format of the output data.</param>
    /// <param name="magazine">The magazine number.</param>
    /// <param name="rows">The rows to process.</param>
    /// <param name="debug">Whether to print debug information.</param>
    /// <returns>The processed line data.</returns>
    public static async Task<int> Line(byte[] lineData, Stream output, LineFormat inputFormat, LineFormat outputFormat, int magazine, int[] rows, bool debug = false)
    {
        // If outputFormat == LineFormat.VBI, pass through the data
        if (outputFormat == LineFormat.VBI)
        {
            await output.WriteAsync(lineData);
        }
        // If outputFormat == LineFormat.VBI_DOUBLE, double the data if needed and pass through
        else if (outputFormat == LineFormat.VBI_DOUBLE)
        {
            var doubledData = inputFormat == LineFormat.VBI_DOUBLE ? lineData : Functions.Double(lineData);
            await output.WriteAsync(doubledData);
        }
        // If outputFormat == LineFormat.T42, decode the data
        else if (outputFormat is LineFormat.T42 or LineFormat.RCWT)
        {
            // Double the line data
            var newLine = inputFormat == LineFormat.VBI_DOUBLE ? lineData : Functions.Double(lineData);

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
                // output.Write(new byte[42]);
                return 0;
            }
            // Get the T42 bytes from the line
            var t42 = T42.Get(bits, offset, debug);
            // Check if the T42 bytes are valid
            if (T42.Check(t42, magazine, rows))
            {
                // Return the T42 bytes or send to RCWT.Process()
                if (outputFormat == LineFormat.T42)
                {
                    await output.WriteAsync(t42);
                }
                else if (outputFormat == LineFormat.RCWT)
                {
                    // TODO: Implement RCWT processing
                    // await RCWT.Process(t42, output);
                }
            }
        }
        return 0;
    }
    #endregion
}
