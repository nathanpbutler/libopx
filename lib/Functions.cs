using System.Collections;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;

namespace nathanbutlerDEV.libopx;

/// <summary>
/// Contains general utility functions for processing teletext data.
/// </summary>
public class Functions
{
    #region General Functions

    /// <summary>
    /// Filter function to process input files based on specified parameters.
    /// </summary>
    /// <param name="input">The input file to process, or null to read from stdin.</param>
    /// <param name="magazine">The magazine number to filter by (null for all magazines).</param>
    /// <param name="rows">The number of rows to filter by.</param>
    /// <param name="lineCount">The number of lines per frame for timecode incrementation.</param>
    /// <param name="inputFormat">The input format to use (e.g., BIN, VBI, T42).</param>
    /// <param name="verbose">Whether to enable verbose output.</param>
    /// <returns>An integer indicating the result of the filtering operation.</returns>
    /// <remarks>
    /// This function reads the specified input file or stdin, processes it according to the provided parameters,
    /// and outputs the filtered lines to stdout. The input format determines how the data is parsed and processed.
    /// Supported formats include BIN, VBI, VBI_DOUBLE, and T42.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown if an unsupported input format is specified.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the specified input file does not exist.</exception>
    /// <exception cref="IOException">Thrown if there is an error reading the input file or stdin.</exception>
    public static int Filter(FileInfo? input, int? magazine, int[] rows, int lineCount, Format inputFormat, bool verbose)
    {
        try
        {
            if (verbose)
            {
                if (input != null && input.Exists)
                    Console.WriteLine($"  Input file: {input.FullName}");
                else
                    Console.WriteLine("Reading from stdin");
                Console.WriteLine($"    Magazine: {magazine?.ToString() ?? "all"}");
                Console.WriteLine($"        Rows: [{string.Join(", ", rows)}]");
                Console.WriteLine($"Input format: {inputFormat}");
                Console.WriteLine($"  Line count: {lineCount}");
            }

            switch (inputFormat)
            {
                case Format.BIN:
                    var bin = input is FileInfo inputBIN && inputBIN.Exists
                        ? new BIN(inputBIN.FullName)
                        : new BIN(Console.OpenStandardInput());
                    foreach (var line in bin.Parse(magazine, rows))
                    {
                        Console.WriteLine(line);
                    }
                    return 0;
                case Format.VBI:
                case Format.VBI_DOUBLE:
                    var vbi = input is FileInfo inputVBI && inputVBI.Exists
                        ? new VBI(inputVBI.FullName)
                        : new VBI(Console.OpenStandardInput());
                    vbi.LineCount = lineCount;
                    foreach (var line in vbi.Parse(magazine, rows))
                    {
                        Console.WriteLine(line);
                    }
                    return 0;
                case Format.T42:
                    var t42 = input is FileInfo inputT42 && inputT42.Exists
                        ? new T42(inputT42.FullName)
                        : new T42(Console.OpenStandardInput());
                    t42.LineCount = lineCount;
                    foreach (var line in t42.Parse(magazine, rows))
                    {
                        Console.WriteLine(line);
                    }
                    return 0;
                case Format.MXF:
                    // Only Filter if file exists, otherwise return 1
                    if (input is FileInfo inputMXF && inputMXF.Exists)
                    {
                        // Implement MXF processing logic
                        var mxf = new MXF(inputMXF.FullName)
                        {
                            Function = Function.Filter, // Set function to Filter
                            Verbose = verbose
                        };
                        mxf.AddRequiredKey(KeyType.Data); // Add Data key to process data packets
                        foreach (var packet in mxf.Parse(magazine, rows))
                        {
                            if (verbose) Console.WriteLine($"Debug: Found packet with {packet.Lines.Count} lines");
                            Console.WriteLine(packet);
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("Error: Input file does not exist or is not specified for MXF format.");
                        return 1;
                    }
                    return 0;
                default:
                    Console.WriteLine($"Unsupported input format: {inputFormat}");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Asynchronously filters teletext data by magazine and rows with progress reporting and cancellation support.
    /// This method provides the same functionality as Filter but with async processing capabilities.
    /// </summary>
    /// <param name="input">The input file to process, or null to read from stdin.</param>
    /// <param name="magazine">The magazine number to filter by (null for all magazines).</param>
    /// <param name="rows">The number of rows to filter by.</param>
    /// <param name="lineCount">The number of lines per frame for timecode incrementation.</param>
    /// <param name="inputFormat">The input format to use (e.g., BIN, VBI, T42).</param>
    /// <param name="verbose">Whether to enable verbose output.</param>
    /// <param name="cancellationToken">Cancellation token for operation cancellation</param>
    /// <returns>Exit code: 0 for success, 1 for failure, 130 for cancellation</returns>
    /// <remarks>
    /// This function reads the specified input file or stdin, processes it according to the provided parameters,
    /// and outputs the filtered lines to stdout. The input format determines how the data is parsed and processed.
    /// Supported formats include BIN, VBI, VBI_DOUBLE, T42, and MXF.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown if an unsupported input format is specified.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the specified input file does not exist.</exception>
    /// <exception cref="IOException">Thrown if there is an error reading the input file or stdin.</exception>
    public static async Task<int> FilterAsync(FileInfo? input, int? magazine, int[] rows, int lineCount, Format inputFormat, bool verbose, CancellationToken cancellationToken = default)
    {
        try
        {
            if (verbose)
            {
                Console.WriteLine(input is { Exists: true } ? $"  Input file: {input.FullName}" : "Reading from stdin");
                Console.WriteLine($"    Magazine: {magazine?.ToString() ?? "all"}");
                Console.WriteLine($"        Rows: [{string.Join(", ", rows)}]");
                Console.WriteLine($"Input format: {inputFormat}");
                Console.WriteLine($"  Line count: {lineCount}");
            }

            switch (inputFormat)
            {
                case Format.BIN:
                    var bin = input is { Exists: true } inputBIN
                        ? new BIN(inputBIN.FullName)
                        : new BIN(Console.OpenStandardInput());
                    await foreach (var packet in bin.ParseAsync(magazine, rows, cancellationToken: cancellationToken))
                    {
                        Console.WriteLine(packet);
                    }
                    return 0;
                case Format.VBI:
                case Format.VBI_DOUBLE:
                    var vbi = input is { Exists: true } inputVBI
                        ? new VBI(inputVBI.FullName)
                        : new VBI(Console.OpenStandardInput());
                    vbi.LineCount = lineCount;
                    await foreach (var line in vbi.ParseAsync(magazine, rows, cancellationToken))
                    {
                        Console.WriteLine(line);
                    }
                    return 0;
                case Format.T42:
                    var t42 = input is { Exists: true } inputT42
                        ? new T42(inputT42.FullName)
                        : new T42(Console.OpenStandardInput());
                    t42.LineCount = lineCount;
                    await foreach (var line in t42.ParseAsync(magazine, rows, cancellationToken))
                    {
                        Console.WriteLine(line);
                    }
                    return 0;
                case Format.MXF:
                    // Only Filter if file exists, otherwise return 1
                    if (input is { Exists: true } inputMXF)
                    {
                        // Implement MXF processing logic
                        var mxf = new MXF(inputMXF.FullName)
                        {
                            Function = Function.Filter, // Set function to Filter
                            Verbose = verbose
                        };
                        mxf.AddRequiredKey(KeyType.Data); // Add Data key to process data packets
                        await foreach (var packet in mxf.ParseAsync(magazine, rows, startTimecode: null, cancellationToken: cancellationToken))
                        {
                            if (verbose) Console.WriteLine($"Debug: Found packet with {packet.Lines.Count} lines");
                            Console.WriteLine(packet);
                        }
                    }
                    else
                    {
                        await Console.Error.WriteLineAsync("Error: Input file does not exist or is not specified for MXF format.");
                        return 1;
                    }
                    return 0;
                case Format.RCWT:
                    // RCWT input format not currently supported for filtering
                    await Console.Error.WriteLineAsync("Error: RCWT input format is not supported for filtering.");
                    return 1;
                case Format.Unknown:
                    await Console.Error.WriteLineAsync("Error: Unknown input format specified.");
                    return 1;
                default:
                    Console.WriteLine($"Unsupported input format: {inputFormat}");
                    return 1;
            }
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("Operation was cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Parses the input format string and returns the corresponding Format enum value.
    /// </summary>
    /// <param name="format"></param>
    /// <returns></returns>
    public static Format ParseFormat(string format)
    {
        return format.TrimStart('.').ToLowerInvariant() switch
        {
            "bin" => Format.BIN,
            "vbi" => Format.VBI,
            "vbid" => Format.VBI_DOUBLE,
            "t42" => Format.T42,
            "mxf" => Format.MXF,
            "rcwt" => Format.RCWT,
            "stl" => Format.STL,
            _ => Format.VBI // Default to VBI if unknown format
        };
    }

    /// <summary>
    /// Extracts essence from an MXF file based on specified parameters.
    /// </summary>
    /// <param name="inputFile">The MXF file to extract essence from.</param>
    /// <param name="outputBasePath">The base path for output files. If null, defaults to the input file's path without extension.</param>
    /// <param name="keyString">A string of keys to filter the essence. If null or empty, defaults to extracting all essence.</param>
    /// <param name="demuxMode">If true, extracts all keys in demux mode. If false, extracts only specified keys.</param>
    /// <param name="useNames">If true, uses key names instead of hex keys for output files.</param>
    /// <param name="klvMode">If true, includes key and length bytes in output files.</param>
    /// <param name="verbose">If true, enables verbose output during processing.</param>
    /// <returns>An integer indicating the result of the extraction operation (0 for success, 1 for failure).</returns>
    public static int Extract(FileInfo inputFile, string? outputBasePath, string? keyString, bool demuxMode, bool useNames, bool klvMode, bool verbose)
    {
        outputBasePath ??= Path.ChangeExtension(inputFile.FullName, null);

        if (!string.IsNullOrEmpty(outputBasePath))
        {
            Console.WriteLine($"Output base path specified: {outputBasePath}");
        }

        try
        {
            using var mxf = new MXF(inputFile.FullName);

            // Configure extraction settings
            mxf.OutputBasePath = outputBasePath;
            mxf.DemuxMode = demuxMode;
            mxf.UseKeyNames = useNames && demuxMode; // Only use names in demux mode
            mxf.KlvMode = klvMode;

            // Parse keys if specified
            if (!demuxMode && !string.IsNullOrEmpty(keyString))
            {
                var targetKeys = ParseKeys(keyString, verbose);
                if (targetKeys.Count > 0)
                {
                    mxf.ClearRequiredKeys();
                    foreach (var key in targetKeys)
                    {
                        mxf.AddRequiredKey(key);
                    }
                }
            }
            else if (!demuxMode)
            {
                // Default to Data if no keys specified
                Console.WriteLine("No keys specified, defaulting to Data.");
                mxf.ClearRequiredKeys();
                mxf.AddRequiredKey(KeyType.Data);
            }

            // Print active modes
            if (klvMode)
            {
                Console.WriteLine("KLV mode enabled - key and length bytes will be included in output files.");
            }

            if (demuxMode)
            {
                Console.WriteLine("Demux mode enabled - all keys will be extracted.");
                if (mxf.UseKeyNames)
                {
                    Console.WriteLine("Name mode enabled - using Key/Essence names instead of hex keys.");
                }
                else
                {
                    Console.WriteLine("Using hex key names for output files.");
                }
            }

            Console.WriteLine($"Processing MXF file: {inputFile.FullName}");

            // Extract the essence
            mxf.ExtractEssence();

            Console.WriteLine($"Finished processing MXF file: {inputFile.FullName}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing file: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Asynchronously extracts/demuxes streams from MXF files with progress reporting and cancellation support.
    /// This method provides the same functionality as Extract but with async processing capabilities.
    /// </summary>
    /// <param name="inputFile">The input MXF file to process</param>
    /// <param name="outputBasePath">Output base path - files will be created as &lt;base&gt;_d.raw, &lt;base&gt;_v.raw, etc</param>
    /// <param name="keyString">Specify keys to extract</param>
    /// <param name="demuxMode">Extract all keys found, output as &lt;base&gt;_&lt;hexkey&gt;.raw</param>
    /// <param name="useNames">Use Key/Essence names instead of hex keys (use with demuxMode)</param>
    /// <param name="klvMode">Include key and length bytes in output files, use .klv extension</param>
    /// <param name="verbose">Enable verbose output</param>
    /// <param name="cancellationToken">Cancellation token for operation cancellation</param>
    /// <returns>Exit code: 0 for success, 1 for failure, 130 for cancellation</returns>
    public static async Task<int> ExtractAsync(FileInfo inputFile, string? outputBasePath, string? keyString, bool demuxMode, bool useNames, bool klvMode, bool verbose, CancellationToken cancellationToken = default)
    {
        outputBasePath ??= Path.ChangeExtension(inputFile.FullName, null);

        if (!string.IsNullOrEmpty(outputBasePath))
        {
            Console.WriteLine($"Output base path specified: {outputBasePath}");
        }

        try
        {
            using var mxf = new MXF(inputFile.FullName);

            // Configure extraction settings
            mxf.OutputBasePath = outputBasePath;
            mxf.DemuxMode = demuxMode;
            mxf.UseKeyNames = useNames && demuxMode; // Only use names in demux mode
            mxf.KlvMode = klvMode;

            switch (demuxMode)
            {
                // Parse keys if specified
                case false when !string.IsNullOrEmpty(keyString):
                {
                    var targetKeys = ParseKeys(keyString, verbose);
                    if (targetKeys.Count > 0)
                    {
                        mxf.ClearRequiredKeys();
                        foreach (var key in targetKeys)
                        {
                            mxf.AddRequiredKey(key);
                        }
                    }

                    break;
                }
                case false:
                    // Default to Data if no keys specified
                    Console.WriteLine("No keys specified, defaulting to Data.");
                    mxf.ClearRequiredKeys();
                    mxf.AddRequiredKey(KeyType.Data);
                    break;
            }

            // Print active modes
            if (klvMode)
            {
                Console.WriteLine("KLV mode enabled - key and length bytes will be included in output files.");
            }

            if (demuxMode)
            {
                Console.WriteLine("Demux mode enabled - all keys will be extracted.");
                Console.WriteLine(mxf.UseKeyNames
                    ? "Name mode enabled - using Key/Essence names instead of hex keys."
                    : "Using hex key names for output files.");
            }

            Console.WriteLine($"Processing MXF file: {inputFile.FullName}");

            // Extract the essence with cancellation support
            // Since ExtractEssence is sync, we'll run it on a task with cancellation
            await Task.Run(() => mxf.ExtractEssence(), cancellationToken);

            Console.WriteLine($"Finished processing MXF file: {inputFile.FullName}");
            return 0;
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("Operation was cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing file: {ex.Message}");
            return 1;
        }
    }

    private static List<KeyType> ParseKeys(string arg, bool verbose = false)
    {
        var keys = new List<KeyType>();
        var keyStrings = arg.Split(',').Select(k => k.Trim().ToLowerInvariant());
        foreach (var keyString in keyStrings)
        {
            switch (keyString)
            {
                case "d":
                    keys.Add(KeyType.Data);
                    if (verbose) Console.WriteLine("Data key specified.");
                    break;
                case "v":
                    keys.Add(KeyType.Video);
                    if (verbose) Console.WriteLine("Video key specified.");
                    break;
                case "s":
                    keys.Add(KeyType.System);
                    if (verbose) Console.WriteLine("System key specified.");
                    break;
                case "t":
                    keys.Add(KeyType.TimecodeComponent);
                    if (verbose) Console.WriteLine("TimecodeComponent key specified.");
                    break;
                case "a":
                    keys.Add(KeyType.Audio);
                    if (verbose) Console.WriteLine("Audio key specified.");
                    break;
                default:
                    Console.WriteLine($"Unknown key type: {keyString}");
                    break;
            }
        }
        return keys;
    }

    /// <summary>
    /// Restripes an MXF file with a new start timecode.
    /// </summary>
    /// <param name="inputFileInfo">The input MXF file to restripe.</param>
    /// <param name="timecodeString">The new start timecode in HH:MM:SS:FF format.</param>
    /// <param name="verbose">If true, enables verbose output during processing.</param>
    /// <param name="printProgress">If true, prints progress information during processing.</param>
    /// <returns>An integer indicating the result of the restripe operation (0 for success, 1 for failure).</returns>
    public static int Restripe(FileInfo inputFileInfo, string timecodeString, bool verbose, bool printProgress = false)
    {
        try
        {

            if (verbose)
            {
                Console.WriteLine($"        Input file: {inputFileInfo.FullName}");
                Console.WriteLine($"New start timecode: {timecodeString}");
            }

            // Create a FileStream that can read and write to the input file
            if (!inputFileInfo.Exists)
            {
                Console.Error.WriteLine($"Error: Input file '{inputFileInfo.FullName}' does not exist.");
                return 1;
            }

            // Create MXF instance and configure for restriping
            using var mxf = new MXF(inputFileInfo)
            {
                Function = Function.Restripe,
                Verbose = verbose,
                PrintProgress = printProgress // Set progress printing option
            };

            // Run the parse method which will handle restriping
            var packets = mxf.Parse(startTimecode: timecodeString);
            foreach (var _ in packets)
            {
                // The Parse method handles all the restriping internally
                // We just need to iterate through to execute it
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error restriping file: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Asynchronously restripes an MXF file with a new start timecode, providing cancellation support and progress reporting.
    /// This method provides the same functionality as Restripe but with async processing capabilities.
    /// </summary>
    /// <param name="inputFileInfo">The input MXF file to restripe</param>
    /// <param name="timecodeString">New start timecode in HH:MM:SS:FF format</param>
    /// <param name="verbose">Enable verbose output</param>
    /// <param name="printProgress">Print progress during parsing</param>
    /// <param name="cancellationToken">Cancellation token for operation cancellation</param>
    /// <returns>Exit code: 0 for success, 1 for failure, 130 for cancellation</returns>
    public static async Task<int> RestripeAsync(FileInfo inputFileInfo, string timecodeString, bool verbose, bool printProgress = false, CancellationToken cancellationToken = default)
    {
        try
        {
            if (verbose)
            {
                Console.WriteLine($"        Input file: {inputFileInfo.FullName}");
                Console.WriteLine($"New start timecode: {timecodeString}");
            }

            // Create a FileStream that can read and write to the input file
            if (!inputFileInfo.Exists)
            {
                await Console.Error.WriteLineAsync($"Error: Input file '{inputFileInfo.FullName}' does not exist.");
                return 1;
            }

            // Create MXF instance and configure for restriping
            using var mxf = new MXF(inputFileInfo)
            {
                Function = Function.Restripe,
                Verbose = verbose,
                PrintProgress = printProgress // Set progress printing option
            };

            var packets = mxf.ParseAsync(startTimecode: timecodeString, cancellationToken: cancellationToken);

            // Run the async parse method which will handle restriping
            await foreach (var _ in packets)
            {
                // The ParseAsync method handles all the restriping internally
                // We just need to iterate through to execute it
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("Operation was cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error restriping file: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Convert function to transform input files between different teletext formats.
    /// </summary>
    /// <param name="input">The input file to convert, or null to read from stdin.</param>
    /// <param name="inputFormat">The input format (BIN, VBI, VBI_DOUBLE, T42, MXF).</param>
    /// <param name="outputFormat">The output format (VBI, VBI_DOUBLE, T42 only).</param>
    /// <param name="output">The output file to write to, or null to write to stdout.</param>
    /// <param name="magazine">The magazine number to filter by (null for all magazines).</param>
    /// <param name="rows">The rows to filter by.</param>
    /// <param name="lineCount">The number of lines per frame for timecode incrementation.</param>
    /// <param name="verbose">Whether to enable verbose output.</param>
    /// <param name="keepBlanks">Whether to keep blank lines in the output.</param>
    /// <returns>An integer indicating the result of the conversion operation.</returns>
    /// <remarks>
    /// This function reads from the specified input file or stdin, converts the data from the input format
    /// to the output format, applies filtering if specified, and writes the result to the output file or stdout.
    /// Only VBI, VBI_DOUBLE, and T42 are supported as output formats (data formats only, not containers).
    /// TODO: Add support for RCWT output format in future update.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown if an unsupported input or output format is specified.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the specified input file does not exist.</exception>
    /// <exception cref="IOException">Thrown if there is an error reading the input file or writing the output file.</exception>
    public static int Convert(FileInfo? input, Format inputFormat, Format outputFormat, FileInfo? output, int? magazine, int[] rows, int lineCount, bool verbose, bool keepBlanks = false)
    {
        try
        {
            // Validate output format - only data formats allowed
            if (outputFormat != Format.VBI && outputFormat != Format.VBI_DOUBLE && outputFormat != Format.T42)
            {
                Console.Error.WriteLine($"Error: Unsupported output format '{outputFormat}'. Supported formats: VBI, VBI_DOUBLE, T42");
                return 1;
            }

            if (verbose)
            {
                if (input != null && input.Exists)
                    Console.WriteLine($"   Input file: {input.FullName}");
                else
                    Console.WriteLine("Reading from stdin");
                Console.WriteLine($" Input format: {inputFormat}");
                Console.WriteLine($"Output format: {outputFormat}");
                if (output != null && output.Exists)
                    Console.WriteLine($"  Output file: {output.FullName}");
                else
                    Console.WriteLine("Writing to stdout");
                Console.WriteLine($"     Magazine: {magazine?.ToString() ?? "all"}");
                Console.WriteLine($"         Rows: [{string.Join(", ", rows)}]");
                Console.WriteLine($"   Line count: {lineCount}");
            }

            // Set up output stream
            Stream outputStream = output != null
                ? new FileStream(output.FullName, FileMode.Create, FileAccess.Write, FileShare.None)
                : Console.OpenStandardOutput();

            try
            {
                switch (inputFormat)
                {
                    case Format.BIN:
                        var bin = input is FileInfo inputBIN && inputBIN.Exists
                            ? new BIN(inputBIN.FullName)
                            : new BIN(Console.OpenStandardInput());
                        bin.OutputFormat = outputFormat;
                        bin.SetOutput(outputStream);
                        foreach (var packet in bin.Parse(magazine, keepBlanks ? Constants.DEFAULT_ROWS : rows))
                        {
                            foreach (var line in packet.Lines.Where(l => l.Type != Format.Unknown))
                            {
                                if (keepBlanks && ((magazine.HasValue && line.Magazine != magazine.Value) || !rows.Contains(line.Row)))
                                {
                                    // Write blank line with same format/length
                                    var blankData = new byte[line.Data.Length];
                                    bin.Output.Write(blankData);
                                }
                                else if (!keepBlanks || ((!magazine.HasValue || line.Magazine == magazine.Value) && rows.Contains(line.Row)))
                                {
                                    bin.Output.Write(line.Data);
                                }
                            }
                        }
                        return 0;

                    case Format.VBI:
                    case Format.VBI_DOUBLE:
                        var vbi = input is FileInfo inputVBI && inputVBI.Exists
                            ? new VBI(inputVBI.FullName, inputFormat)
                            : new VBI(Console.OpenStandardInput(), inputFormat);
                        vbi.OutputFormat = outputFormat;
                        vbi.LineCount = lineCount;
                        vbi.SetOutput(outputStream);
                        foreach (var line in vbi.Parse(magazine, keepBlanks ? Constants.DEFAULT_ROWS : rows))
                        {
                            if (keepBlanks && ((magazine.HasValue && line.Magazine != magazine.Value) || !rows.Contains(line.Row)))
                            {
                                // Write blank line with same format/length
                                var blankData = new byte[line.Data.Length];
                                vbi.Output.Write(blankData);
                            }
                            else if (!keepBlanks || ((!magazine.HasValue || line.Magazine == magazine.Value) && rows.Contains(line.Row)))
                            {
                                vbi.Output.Write(line.Data);
                            }
                        }
                        return 0;

                    case Format.T42:
                        var t42 = input is FileInfo inputT42 && inputT42.Exists
                            ? new T42(inputT42.FullName)
                            : new T42(Console.OpenStandardInput());
                        t42.OutputFormat = outputFormat;
                        t42.LineCount = lineCount;
                        t42.SetOutput(outputStream);
                        foreach (var line in t42.Parse(magazine, keepBlanks ? Constants.DEFAULT_ROWS : rows))
                        {
                            if (keepBlanks && ((magazine.HasValue && line.Magazine != magazine.Value) || !rows.Contains(line.Row)))
                            {
                                // Write blank line with same format/length
                                var blankData = new byte[line.Data.Length];
                                t42.Output.Write(blankData);
                            }
                            else if (!keepBlanks || ((!magazine.HasValue || line.Magazine == magazine.Value) && rows.Contains(line.Row)))
                            {
                                t42.Output.Write(line.Data);
                            }
                        }
                        return 0;

                    case Format.MXF:
                        if (input == null || !input.Exists)
                        {
                            Console.Error.WriteLine("Error: Input file must be specified and exist for MXF format conversion.");
                            return 1;
                        }
                        var mxf = new MXF(input.FullName)
                        {
                            OutputFormat = outputFormat,
                            Verbose = verbose
                        };
                        mxf.OutputFormat = outputFormat;
                        mxf.SetOutput(outputStream);
                        foreach (var packet in mxf.Parse(magazine, keepBlanks ? Constants.DEFAULT_ROWS : rows))
                        {
                            foreach (var line in packet.Lines.Where(l => l.Type != Format.Unknown))
                            {
                                if (keepBlanks && ((magazine.HasValue && line.Magazine != magazine.Value) || !rows.Contains(line.Row)))
                                {
                                    // Write blank line with same format/length
                                    var blankData = new byte[line.Data.Length];
                                    mxf.Output.Write(blankData);
                                }
                                else if (!keepBlanks || ((!magazine.HasValue || line.Magazine == magazine.Value) && rows.Contains(line.Row)))
                                {
                                    mxf.Output.Write(line.Data);
                                }
                            }
                        }
                        return 0;

                    default:
                        Console.Error.WriteLine($"Error: Unsupported input format '{inputFormat}'.");
                        return 1;
                }
            }
            finally
            {
                if (output != null)
                {
                    outputStream?.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Asynchronously converts files between different teletext formats with progress reporting and cancellation support.
    /// This method provides the same functionality as Convert but with async processing capabilities.
    /// </summary>
    /// <param name="input">The input file to convert, or null to read from stdin.</param>
    /// <param name="inputFormat">The input format (BIN, VBI, VBI_DOUBLE, T42, MXF).</param>
    /// <param name="outputFormat">The output format (VBI, VBI_DOUBLE, T42 only).</param>
    /// <param name="output">The output file to write to, or null to write to stdout.</param>
    /// <param name="magazine">The magazine number to filter by (null for all magazines).</param>
    /// <param name="rows">The rows to filter by.</param>
    /// <param name="lineCount">The number of lines per frame for timecode incrementation.</param>
    /// <param name="verbose">Whether to enable verbose output.</param>
    /// <param name="keepBlanks">Whether to keep blank lines in the output.</param>
    /// <param name="cancellationToken">Cancellation token for operation cancellation</param>
    /// <returns>Exit code: 0 for success, 1 for failure, 130 for cancellation</returns>
    public static async Task<int> ConvertAsync(FileInfo? input, Format inputFormat, Format outputFormat, FileInfo? output, int? magazine, int[] rows, int lineCount, bool verbose, bool keepBlanks = false, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate output format - only data formats allowed
            if (outputFormat != Format.VBI && outputFormat != Format.VBI_DOUBLE && outputFormat != Format.T42 && outputFormat != Format.RCWT && outputFormat != Format.STL)
            {
                await Console.Error.WriteLineAsync($"Error: Unsupported output format '{outputFormat}'. Supported formats: VBI, VBI_DOUBLE, T42, RCWT, STL");
                return 1;
            }

            if (verbose)
            {
                Console.WriteLine(input is { Exists: true }
                    ? $"   Input file: {input.FullName}"
                    : "Reading from stdin");
                Console.WriteLine($" Input format: {inputFormat}");
                Console.WriteLine($"Output format: {outputFormat}");
                Console.WriteLine(output is { Exists: true }
                    ? $"  Output file: {output.FullName}"
                    : "Writing to stdout");
                Console.WriteLine($"     Magazine: {magazine?.ToString() ?? "all"}");
                Console.WriteLine($"         Rows: [{string.Join(", ", rows)}]");
                Console.WriteLine($"   Line count: {lineCount}");
            }
            

            // Set up output stream
            var outputStream = output != null
                ? new FileStream(output.FullName, FileMode.Create, FileAccess.Write, FileShare.None)
                : Console.OpenStandardOutput();

            try
            {
                // Reset RCWT state for new conversion session
                if (outputFormat == Format.RCWT)
                {
                    ResetRCWTHeader();
                }
                
                // Write headers for formats that require them
                if (outputFormat == Format.RCWT || outputFormat == Format.STL)
                {
                    await WriteHeaderAsync(outputStream, outputFormat, cancellationToken);
                }
                
                switch (inputFormat)
                {
                    case Format.BIN:
                        var bin = input is { Exists: true } inputBIN
                            ? new BIN(inputBIN.FullName)
                            : new BIN(Console.OpenStandardInput());
                        bin.OutputFormat = outputFormat;
                        bin.SetOutput(outputStream);
                        await foreach (var packet in bin.ParseAsync(magazine, keepBlanks ? Constants.DEFAULT_ROWS : rows, cancellationToken: cancellationToken))
                        {
                            foreach (var line in packet.Lines)
                            {
                                await WriteOutputAsync(line, bin.Output, outputFormat, magazine, rows, keepBlanks, cancellationToken);
                                
                                /*if (keepBlanks && ((magazine.HasValue && line.Magazine != magazine.Value) || !rows.Contains(line.Row)))
                                {
                                    // Write blank line with same format/length
                                    var blankData = new byte[line.Data.Length];
                                    bin.Output.Write(blankData);
                                }
                                else if (!keepBlanks || ((!magazine.HasValue || line.Magazine == magazine.Value) && rows.Contains(line.Row)))
                                {
                                    bin.Output.Write(line.Data);
                                }*/
                            }
                        }
                        return 0;

                    case Format.VBI:
                    case Format.VBI_DOUBLE:
                        var vbi = input is { Exists: true } inputVBI
                            ? new VBI(inputVBI.FullName, inputFormat)
                            : new VBI(Console.OpenStandardInput(), inputFormat);
                        vbi.OutputFormat = outputFormat;
                        vbi.LineCount = lineCount;
                        vbi.SetOutput(outputStream);
                        await foreach (var line in vbi.ParseAsync(magazine, keepBlanks ? Constants.DEFAULT_ROWS : rows, cancellationToken))
                        {
                            await WriteOutputAsync(line, vbi.Output, outputFormat, magazine, rows, keepBlanks, cancellationToken);  
                            
                            /*if (keepBlanks && ((magazine.HasValue && line.Magazine != magazine.Value) || !rows.Contains(line.Row)))
                            {
                                // Write blank line with same format/length
                                var blankData = new byte[line.Data.Length];
                                vbi.Output.Write(blankData);
                            }
                            else if (!keepBlanks || ((!magazine.HasValue || line.Magazine == magazine.Value) && rows.Contains(line.Row)))
                            {
                                vbi.Output.Write(line.Data);
                            }*/
                        }
                        return 0;

                    case Format.T42:
                        var t42 = input is { Exists: true } inputT42
                            ? new T42(inputT42.FullName)
                            : new T42(Console.OpenStandardInput());
                        t42.OutputFormat = outputFormat;
                        t42.LineCount = lineCount;
                        t42.SetOutput(outputStream);
                        await foreach (var line in t42.ParseAsync(magazine, keepBlanks ? Constants.DEFAULT_ROWS : rows, cancellationToken))
                        {
                            await WriteOutputAsync(line, t42.Output, outputFormat, magazine, rows, keepBlanks, cancellationToken);
                            
                            /*if (keepBlanks && ((magazine.HasValue && line.Magazine != magazine.Value) || !rows.Contains(line.Row)))
                            {
                                // Write blank line with same format/length
                                var blankData = new byte[line.Data.Length];
                                t42.Output.Write(blankData);
                            }
                            else if (!keepBlanks || ((!magazine.HasValue || line.Magazine == magazine.Value) && rows.Contains(line.Row)))
                            {
                                t42.Output.Write(line.Data);
                            }*/
                        }
                        return 0;

                    case Format.MXF:
                        if (input == null || !input.Exists)
                        {
                            await Console.Error.WriteLineAsync("Error: Input file must be specified and exist for MXF format conversion.");
                            return 1;
                        }
                        var mxf = new MXF(input.FullName)
                        {
                            OutputFormat = outputFormat,
                            Verbose = verbose
                        };
                        mxf.OutputFormat = outputFormat;
                        mxf.SetOutput(outputStream);
                        await foreach (var packet in mxf.ParseAsync(magazine, keepBlanks ? Constants.DEFAULT_ROWS : rows, cancellationToken: cancellationToken))
                        {
                            foreach (var line in packet.Lines)
                            {
                                await WriteOutputAsync(line, mxf.Output, outputFormat, magazine, rows, keepBlanks, cancellationToken);
                                
                                /*if (keepBlanks && ((magazine.HasValue && line.Magazine != magazine.Value) || !rows.Contains(line.Row)))
                                {
                                    // Write blank line with same format/length
                                    var blankData = new byte[line.Data.Length];
                                    mxf.Output.Write(blankData);
                                }
                                else if (!keepBlanks || ((!magazine.HasValue || line.Magazine == magazine.Value) && rows.Contains(line.Row)))
                                {
                                    mxf.Output.Write(line.Data);
                                }*/
                            }
                        }
                        return 0;

                    case Format.RCWT: // Not supported as input format yet
                    case Format.STL:  // Not supported as input format yet
                    case Format.Unknown:
                    default:
                        await Console.Error.WriteLineAsync($"Error: Unsupported input format '{inputFormat}'.");
                        return 1;
                }
            }
            finally
            {
                if (output != null)
                {
                    await outputStream.DisposeAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("Operation was cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            return 1;
        }
    }

    #endregion
    
    #region Output Format Helpers
    
    /// <summary>
    /// Combined output writing logic for ConvertAsync to avoid code duplication.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="output"></param>
    /// <param name="outputFormat"></param>
    /// <param name="magazine"></param>
    /// <param name="rows"></param>
    /// <param name="keepBlanks"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private static async Task WriteOutputAsync(Line input, Stream output, Format outputFormat, int? magazine, int[] rows, bool keepBlanks, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (input.Type == Format.Unknown)
        {
            return;
        }
        
        byte[] dataToWrite;
        
        // Handle different output formats
        switch (outputFormat)
        {
            case Format.RCWT:
                // For RCWT, convert to RCWT packet format
                var (fts, fieldNumber) = GetAndIncrementRCWTState();
                dataToWrite = input.ToRCWT(fts, fieldNumber);
                break;
                
            default:
                // For other formats, use the line's data directly
                dataToWrite = input.Data;
                break;
        }
        
        if (keepBlanks && ((magazine.HasValue && input.Magazine != magazine.Value) || !rows.Contains(input.Row)))
        {
            // Write blank line with same format/length
            var blankData = new byte[dataToWrite.Length];
            await output.WriteAsync(blankData, cancellationToken);
        }
        else if (!keepBlanks || ((!magazine.HasValue || input.Magazine == magazine.Value) && rows.Contains(input.Row)))
        {
            await output.WriteAsync(dataToWrite, cancellationToken);
        }
    }
    
    /// <summary>
    /// Writes format-specific headers to the output stream.
    /// </summary>
    /// <param name="output">The output stream to write to</param>
    /// <param name="outputFormat">The target output format</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    private static async Task WriteHeaderAsync(Stream output, Format outputFormat, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        switch (outputFormat)
        {
            case Format.RCWT:
                await WriteRCWTHeaderAsync(output, cancellationToken);
                break;
            case Format.STL:
                await WriteSTLHeaderAsync(output, cancellationToken);
                break;
            // Other formats (VBI, VBI_DOUBLE, T42) don't require headers
            case Format.VBI:
            case Format.VBI_DOUBLE:
            case Format.T42:
            case Format.BIN:
            case Format.MXF:
            case Format.Unknown:
            default:
                // No header needed for this format
                break;
        }
    }
    
    // Static flag to track if RCWT header has been written (thread-safe)
    private static volatile bool _rcwtHeaderWritten = false;
    private static readonly object _rcwtHeaderLock = new();
    
    // RCWT state management for FTS and field numbers
    private static int _rcwtFts = 0;
    private static int _rcwtFieldNumber = 0;
    private static readonly object _rcwtStateLock = new();

    /// <summary>
    /// Writes an RCWT (Raw Caption With Timing) header to the output stream.
    /// The header is only written once per conversion session.
    /// </summary>
    /// <param name="output">The output stream to write to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    private static async Task WriteRCWTHeaderAsync(Stream output, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        // Thread-safe check and write of RCWT header
        lock (_rcwtHeaderLock)
        {
            if (_rcwtHeaderWritten)
                return;
            _rcwtHeaderWritten = true;
        }
        
        // Write the RCWT header (11 bytes)
        await output.WriteAsync(Constants.RCWT_HEADER, cancellationToken);
    }

    /// <summary>
    /// Resets the RCWT header flag and state for a new conversion session.
    /// Call this when starting a new RCWT conversion.
    /// </summary>
    public static void ResetRCWTHeader()
    {
        lock (_rcwtHeaderLock)
        {
            _rcwtHeaderWritten = false;
        }
        
        lock (_rcwtStateLock)
        {
            _rcwtFts = 0;
            _rcwtFieldNumber = 0;
        }
    }

    /// <summary>
    /// Gets the current RCWT state (FTS and field number) and increments them for the next line.
    /// This method is thread-safe.
    /// </summary>
    /// <returns>Tuple containing current FTS and field number</returns>
    public static (int fts, int fieldNumber) GetAndIncrementRCWTState()
    {
        lock (_rcwtStateLock)
        {
            var currentState = (_rcwtFts, _rcwtFieldNumber);
            
            // Increment FTS (time in milliseconds) - you may want to adjust this increment
            _rcwtFts += 40; // Assuming 25 fps (40ms per frame), adjust as needed
            
            // Alternate field number (0, 1, 0, 1, ...)
            _rcwtFieldNumber = (_rcwtFieldNumber + 1) % 2;
            
            return currentState;
        }
    }

    /// <summary>
    /// Sets the RCWT FTS (Frame Time Stamp) value.
    /// </summary>
    /// <param name="fts">The FTS value in milliseconds</param>
    public static void SetRCWTFts(int fts)
    {
        lock (_rcwtStateLock)
        {
            _rcwtFts = fts;
        }
    }

    /// <summary>
    /// Sets the RCWT field number.
    /// </summary>
    /// <param name="fieldNumber">The field number (0 or 1)</param>
    public static void SetRCWTFieldNumber(int fieldNumber)
    {
        lock (_rcwtStateLock)
        {
            _rcwtFieldNumber = fieldNumber;
        }
    }
    
    /// <summary>
    /// Writes an EBU STL (EBU-t3264) header to the output stream.
    /// </summary>
    /// <param name="output">The output stream to write to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    private static async Task WriteSTLHeaderAsync(Stream output, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        // TODO: Implement STL header structure
        // STL headers include:
        // - GSI (General Subtitle Information) block (1024 bytes)
        // - Contains metadata like program title, timecode start/end
        // - Character set information
        // - Subtitle count and timing information
        
        // Placeholder implementation - will be implemented later
        await Task.CompletedTask;
    }
    
    #endregion

    #region VBI
    /// <summary>
    /// Double the length of a byte array
    /// </summary>
    /// <param name="bytes">The byte array to double.</param>
    /// <returns>The doubled byte array.</returns>
    public static byte[] Double(byte[] bytes)
    {
        // Create a new byte array with double the length of the input
        var doubled = new byte[bytes.Length * 2];
        // For each byte in the input...
        for (var i = 0; i < bytes.Length; i++)
        {
            // If the byte is not the last byte in the input...
            if (i != bytes.Length - 1)
            {
                // Set the byte at i * 2 to the byte at i
                doubled[i * 2] = bytes[i];
                // Set the byte at i * 2 + 1 to the average of the byte at i and the byte at i + 1
                doubled[i * 2 + 1] = (byte)Math.Floor((bytes[i] + bytes[i + 1]) / 2.0);
            }
            // Otherwise if the byte is the last byte in the input...
            else
            {
                // Set the byte at i * 2 to the byte at i
                doubled[i * 2] = bytes[i];
                // Set the byte at i * 2 + 1 to the byte at i
                doubled[i * 2 + 1] = bytes[i];
            }
        }
        // Return the doubled bytes
        return doubled;
    }

    /// <summary>
    /// Normalise a byte array
    /// </summary>
    /// <param name="line">The byte array to normalise.</param>
    /// <returns>The normalised byte array.</returns>
    public static float[] Normalise(byte[] line)
    {
        // Get minimum and maximum values from line
        var min = line.Min();
        var max = line.Max();

        // Get range
        float range = max - min;

        // If range is 0, set range to 1
        if (range == 0)
        {
            range = 1;
        }

        // Create a new float array with the same length as the line
        var normalised = new float[line.Length];

        // For each byte in the line...
        for (var i = 0; i < line.Length; i++)
        {
            // Normalise the byte and set it in the normalised array
            normalised[i] = (line[i] - min) / range;
        }

        // Return the normalised line
        return normalised;
    }

    /// <summary>
    /// Get the bits from a normalised byte array
    /// </summary>
    /// <param name="normalised">The normalised byte array to get the bits from.</param>
    /// <param name="threshold">The threshold for bit detection (default 0.40f)</param>
    /// <returns>The bits from the normalised byte array.</returns>
    public static BitArray GetBits(float[] normalised, float threshold = Constants.VBI_DEFAULT_THRESHOLD)
    {
        // Create a new BitArray with the same length as the normalised line
        BitArray bits = new(normalised.Length + 16);
        // For each byte in the normalised line...
        for (var i = 0; i < normalised.Length; i++)
        {
            // Set the bit in the BitArray to true if the normalised byte is greater than or equal to the threshold
            // Default threshold of 0.40 has so far produced the best results
            bits[i] = normalised[i] >= threshold;
        }
        // Return the BitArray
        return bits;
    }

    /// <summary>
    /// Check if a byte has odd parity
    /// </summary>
    /// <param name="value">The byte to check.</param>
    /// <returns>True if the byte has odd parity, false otherwise.</returns>
    public static bool HasOddParity(byte value)
    {
        // Set count to 0
        var count = 0;

        // For each bit in the byte...
        for (var i = 0; i < 8; i++)
        {
            // If the bit is 1, increment the count
            if ((value & (1 << i)) != 0)
            {
                count++;
            }
        }

        // Return true if the count of '1' bits is odd
        return count % 2 != 0;
    }

    /// <summary>
    /// Get a byte from a bit array
    /// </summary>
    /// <param name="bits">The bit array to get the byte from.</param>
    /// <param name="offset">The offset to get the byte from.</param>
    /// <param name="dataBits">Whether to use data bits.</param>
    /// <returns>The byte from the bit array.</returns>
    public static byte GetByte(BitArray bits, int offset, bool dataBits)
    {
        // Create a new bit array with 8 bits
        BitArray b = new(8);

        // If the offset is negative or greater than the length of the bits, return 0
        if (offset < 0 || offset + 28 > bits.Length)
        {
            return 0;
        }

        // Collect bits from the offset (Every 4 bytes has so far produced the best results)
        b[0] = bits[offset + 0];
        b[1] = bits[offset + 4];
        b[2] = bits[offset + 8];
        b[3] = bits[offset + 12];
        b[4] = bits[offset + 16];
        b[5] = bits[offset + 20];
        b[6] = bits[offset + 24];
        b[7] = bits[offset + 28];

        // Convert the bit array to a byte
        var byteArr = new byte[1];
        b.CopyTo(byteArr, 0);

        // If the parity is even, flip the MSB to ensure odd parity
        if (dataBits && !HasOddParity(byteArr[0]))
        {
            byteArr[0] ^= Constants.VBI_PARITY_FLIP_MASK; // XOR with 0x80 to flip the MSB
        }

        // Return the modified byte
        return byteArr[0];
    }

    /// <summary>
    /// Find the clock run-in and framing code from a byte array.
    /// </summary>
    /// <param name="bytes">The byte array to search.</param>
    /// <returns>The offset of the clock run-in and framing code. If not found, returns -1.</returns>
    public static int GetCrifc(byte[] bytes)
    {
        for (var i = 0; i < bytes.Length - 2; i++)
        {
            if (bytes[i] == Constants.T42_CLOCK_BYTE && bytes[i + 1] == Constants.T42_CLOCK_BYTE && bytes[i + 2] == Constants.T42_FRAMING_CODE)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Find the clock run-in and framing code from a span of bytes.
    /// </summary>
    /// <param name="bytes">The span of bytes to search.</param>
    /// <returns>The offset of the clock run-in and framing code. If not found, returns -1.</returns>
    public static int GetCrifc(ReadOnlySpan<byte> bytes)
    {
        for (var i = 0; i < bytes.Length - 2; i++)
        {
            if (bytes[i] == Constants.T42_CLOCK_BYTE && bytes[i + 1] == Constants.T42_CLOCK_BYTE && bytes[i + 2] == Constants.T42_FRAMING_CODE)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Get the offset of the line
    /// </summary>
    /// <param name="bits">The bits to get the offset from.</param>
    /// <returns>The offset of the line.</returns>
    public static int GetOffset(BitArray bits)
    {
        // Set lineOffset to -1
        var lineOffset = -1;
        // Create byte array to store bytes
        var byteArray = new byte[4];
        // For each offset from 0 to MAX_OFFSET_SEARCH...
        for (var o = 0; o < Constants.VBI_MAX_OFFSET_SEARCH; o++)
        {
            // Look for clock (0x55) and framing code (0x27) at specific offsets
            // Clock:
            byteArray[0] = GetByte(bits, o, false);
            byteArray[1] = GetByte(bits, o + Constants.VBI_CLOCK_OFFSET_1, false);
            // Framing Code:
            byteArray[2] = GetByte(bits, o + Constants.VBI_FRAMING_OFFSET_1, false);
            byteArray[3] = GetByte(bits, o + Constants.VBI_FRAMING_OFFSET_2, false);

            // If clock is 0x55 and framing code is 0x27, continue
            if (byteArray[0] != Constants.T42_CLOCK_BYTE || byteArray[1] != Constants.T42_CLOCK_BYTE || (byteArray[2] != Constants.T42_FRAMING_CODE && byteArray[3] != Constants.T42_FRAMING_CODE)) continue;

            // If framing code found at offset 39, return offset + 39
            if (byteArray[2] == Constants.T42_FRAMING_CODE)
            {
                lineOffset = o + Constants.VBI_FRAMING_OFFSET_1;
            }

            // Otherwise if framing code found at offset 40, return offset + 40
            else if (byteArray[3] == Constants.T42_FRAMING_CODE)
            {
                lineOffset = o + Constants.VBI_FRAMING_OFFSET_2;
            }
        }

        // Return the offset
        return lineOffset;
    }

    #endregion

    #region BIN

    /// <summary>
    /// Get the count of the bytes
    /// </summary>
    /// <param name="bytes">The byte array to get the count of.</param>
    /// <returns>The count of the bytes.</returns>
    public static int GetCount(byte[] bytes)
    {
        // Return the first byte shifted left by 8 bits OR the second byte
        return bytes[0] << 8 | bytes[1];
    }

    #endregion
}