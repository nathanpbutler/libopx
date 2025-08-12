using System.CommandLine;
using System.CommandLine.Completions;
using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.opx;

public class Commands
{
    /// <summary>
    /// Parses a string of rows into an array of integers, handling both single numbers and ranges
    /// (e.g., "1,2,5-8,15" becomes [1, 2, 5, 6, 7, 8, 15]).
    /// </summary>
    /// <param name="rowsString">The string representation of the rows.</param>
    /// <returns>An array of integers representing the parsed rows.</returns>
    private static int[] ParseRowsString(string rowsString)
    {
        var rows = new List<int>();
        var parts = rowsString.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Contains('-'))
            {
                // Handle range like "5-8"
                var rangeParts = trimmed.Split('-');
                if (rangeParts.Length == 2 &&
                    int.TryParse(rangeParts[0], out int start) &&
                    int.TryParse(rangeParts[1], out int end))
                {
                    for (int i = start; i <= end; i++)
                    {
                        if (i >= 0 && i <= 24)
                            rows.Add(i);
                    }
                }
            }
            else if (int.TryParse(trimmed, out int row))
            {
                // Handle single number
                if (row >= 0 && row <= 24)
                    rows.Add(row);
            }
        }

        return [.. rows.Distinct().OrderBy(x => x)];
    }

    /// <summary>
    /// Creates the filter command for teletext data processing.
    /// This command allows filtering teletext data by magazine and rows, with options for input format,
    /// line count, and whether to use caption rows.
    /// It also supports reading from stdin if no input file is specified.
    /// The command will validate the input file, parse the specified options, and execute the filtering
    /// operation using the provided parameters.
    /// </summary>
    /// <returns>A Command object representing the filter command.</returns>
    /// <exception cref="ArgumentException">Thrown if the input file does not exist or is invalid.</exception>
    public static async Task<Command> CreateFilterCommand()
    {
        var filterCommand = new Command("filter", "Filter teletext data by magazine and rows");

        var inputOption = new Argument<string?>("input")
        {
            Description = "Input file path (reads from stdin if not specified)",
            Arity = ArgumentArity.ZeroOrOne
        };

        var magazineOption = new Option<int?>("-m")
        {
            Aliases = { "--magazine" },
            Description = "Filter by magazine number",
            DefaultValueFactory = _ => Constants.DEFAULT_MAGAZINE,
            Required = false
        };

        magazineOption.CompletionSources.Add(ctx =>
        {
            List<CompletionItem> magazines = [];
            for (int i = 1; i <= 8; i++)
            {
                magazines.Add(new CompletionItem(
                    label: i.ToString("D2"),
                    sortText: i.ToString("D2")));
            }
            return magazines;
        });

        var rowsOption = new Option<string?>("-r")
        {
            Aliases = { "--rows" },
            Description = "Filter by number of rows (comma-separated or hyphen ranges, e.g., 1,2,5-8,15)",
            Required = false
        };

        rowsOption.CompletionSources.Add(ctx =>
        {
            List<CompletionItem> rows = [];
            for (int i = 0; i <= 24; i++)
            {
                rows.Add(new CompletionItem(
                    label: i.ToString("D2"),
                    sortText: i.ToString("D2")));
            }
            return rows;
        });

        var inputFormatOption = new Option<string?>("-if")
        {
            Aliases = { "--input-format" },
            Description = "Input format",
            Required = false,
            DefaultValueFactory = _ => "vbi"
        };

        inputFormatOption.CompletionSources.Add(ctx =>
        {
            return
            [
                new CompletionItem("bin", "MXF data stream"),
                new CompletionItem("vbi", "VBI format"),
                new CompletionItem("vbid", "VBI format (double width)"),
                new CompletionItem("t42", "T42 format")
            ];
        });

        var lineCountOption = new Option<int?>("-l")
        {
            Aliases = { "--line-count" },
            Description = "Number of lines per frame for timecode incrementation",
            Required = false,
            DefaultValueFactory = _ => 2
        };

        var capsOption = new Option<bool>("-c")
        {
            Aliases = { "--caps" },
            Description = "Use caption rows (1-24) instead of default rows (0-24)",
            Required = false,
            DefaultValueFactory = _ => false
        };

        var verboseOption = new Option<bool>("-V")
        {
            Aliases = { "--verbose" },
            Description = "Enable verbose output",
            Required = false,
            DefaultValueFactory = _ => false
        };

        filterCommand.Arguments.Add(inputOption);
        filterCommand.Options.Add(inputFormatOption);
        filterCommand.Options.Add(magazineOption);
        filterCommand.Options.Add(rowsOption);
        filterCommand.Options.Add(lineCountOption);
        filterCommand.Options.Add(capsOption);
        filterCommand.Options.Add(verboseOption);

        filterCommand.SetAction(async (parseResult) =>
        {
            // Get input file and validate it
            string? inputFilePath = parseResult.GetValue(inputOption);
            if (string.IsNullOrEmpty(inputFilePath) || inputFilePath.Equals("-", StringComparison.OrdinalIgnoreCase) || inputFilePath.Equals("stdin", StringComparison.OrdinalIgnoreCase))
            {
                // If no input file is specified, read from stdin
                inputFilePath = null;
            }
            else
            {
                // If input file is specified, ensure it exists
                inputFilePath = Path.GetFullPath(inputFilePath);
            }
            FileInfo? inputFile = inputFilePath != null ? new FileInfo(inputFilePath) : null;
            if (inputFile != null && !inputFile.Exists && !inputFile.Name.Equals("-", StringComparison.OrdinalIgnoreCase) && !inputFile.Name.Equals("stdin", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Error: Input file '{inputFile.FullName}' does not exist.");
                await Task.FromResult(1);
                return;
            }
            int magazine = parseResult.GetValue(magazineOption) ?? Constants.DEFAULT_MAGAZINE;
            string? rowsString = parseResult.GetValue(rowsOption);
            bool useCaps = parseResult.GetValue(capsOption);
            string inputFormatString = parseResult.GetValue(inputFormatOption) ?? "vbi";

            // Determine which rows to use
            int[] rows;
            if (!string.IsNullOrEmpty(rowsString))
            {
                rows = ParseRowsString(rowsString);
            }
            else if (useCaps)
            {
                rows = Constants.CAPTION_ROWS;
            }
            else
            {
                rows = Constants.DEFAULT_ROWS;
            }
            int lineCount = parseResult.GetValue(lineCountOption) ?? 2;
            bool verbose = parseResult.GetValue(verboseOption);

            Format inputFormat = inputFile != null && inputFile.Exists
                ? Functions.ParseFormat(Path.GetExtension(inputFile.Name).ToLowerInvariant())
                : Functions.ParseFormat(inputFormatString);

            await Task.FromResult(Functions.Filter(inputFile, magazine, rows, lineCount, inputFormat, verbose));
        });

        return await Task.FromResult(filterCommand);
    }

    /// <summary>
    /// Creates the extract command for extracting/demuxing streams from MXF files.
    /// This command allows specifying input files, output paths, keys to extract, and options for
    /// demuxing and naming conventions. It also supports verbose output and KLV mode.
    /// </summary>
    /// <returns>A Command object representing the extract command.</returns>
    /// <exception cref="ArgumentException">Thrown if the input file does not exist or is invalid.</exception>
    public static async Task<Command> CreateExtractCommand()
    {
        var extractCommand = new Command("extract", "Extract/demux streams from MXF files");

        var inputOption = new Argument<FileInfo>("input") // Keeping this as an Argument<FileInfo> unlike filter/convert
        {
            Description = "Input file path",
            Arity = ArgumentArity.ExactlyOne
        };

        var outputOption = new Option<string?>("-o")
        {
            Aliases = { "--output" },
            Description = "Output base path - files will be created as <base>_d.raw, <base>_v.raw, etc",
            Required = false,
            DefaultValueFactory = _ => null,
            Arity = ArgumentArity.ZeroOrOne
        };

        var keyOption = new Option<string?>("-k")
        {
            Aliases = { "--key" },
            Description = "Specify keys to extract",
            Required = false,
            DefaultValueFactory = _ => null,
            Arity = ArgumentArity.ZeroOrOne
        };
        
        keyOption.CompletionSources.Add(ctx =>
        {
            return
            [
                new CompletionItem("v", "Video stream"),
                new CompletionItem("a", "Audio stream"),
                new CompletionItem("d", "Data stream"),
                new CompletionItem("s", "System stream"),
                new CompletionItem("t", "TimecodeComponent stream"),

            ];
        });

        var demuxOption = new Option<bool>("-d")
        {
            Aliases = { "--demux" },
            Description = "Extract all keys found, output as <base>_<hexkey>.raw",
            Required = false,
            DefaultValueFactory = _ => false,
            Arity = ArgumentArity.ZeroOrOne
        };

        var nameOption = new Option<bool>("-n")
        {
            Description = "Use Key/Essence names instead of hex keys (use with -d)",
            Required = false,
            DefaultValueFactory = _ => false,
            Arity = ArgumentArity.ZeroOrOne
        };

        var klvOption = new Option<bool>("--klv")
        {
            Description = "Include key and length bytes in output files, use .klv extension",
            Required = false,
            DefaultValueFactory = _ => false,
            Arity = ArgumentArity.ZeroOrOne
        };

        var verboseOption = new Option<bool>("-V")
        {
            Aliases = { "--verbose" },
            Description = "Enable verbose output",
            Required = false,
            DefaultValueFactory = _ => false,
            Arity = ArgumentArity.ZeroOrOne
        };

        extractCommand.Arguments.Add(inputOption);
        extractCommand.Options.Add(outputOption);
        extractCommand.Options.Add(keyOption);
        extractCommand.Options.Add(demuxOption);
        extractCommand.Options.Add(nameOption);
        extractCommand.Options.Add(klvOption);
        extractCommand.Options.Add(verboseOption);

        extractCommand.SetAction(async (parseResult) =>
        {
            FileInfo? inputFile = parseResult.GetValue(inputOption);
            string? outputBasePath = parseResult.GetValue(outputOption);
            string? keyString = parseResult.GetValue(keyOption);
            bool demuxMode = parseResult.GetValue(demuxOption);
            bool useNames = parseResult.GetValue(nameOption);
            bool klvMode = parseResult.GetValue(klvOption);
            bool verbose = parseResult.GetValue(verboseOption);

            if (inputFile == null || !inputFile.Exists)
            {
                Console.WriteLine("Input file is required and must exist.");
                await Task.FromResult(1);
                return;
            }

            await Task.FromResult(Functions.Extract(inputFile, outputBasePath, keyString, demuxMode, useNames, klvMode, verbose));
        });

        return await Task.FromResult(extractCommand);
    }

    /// <summary>
    /// Creates the restripe command for restriping MXF files with a new start timecode.
    /// This command allows specifying an input MXF file and a new start timecode in HH:MM:SS:FF format.
    /// It also supports options for verbose output and printing progress during parsing.
    /// The command will validate the input file and timecode, then execute the restriping operation
    /// using the provided parameters.
    /// </summary>
    /// <returns>A Command object representing the restripe command.</returns>
    /// <exception cref="ArgumentException">Thrown if the input file does not exist or is invalid, or if the timecode is not provided.</exception>
    public static async Task<Command> CreateRestripeCommand()
    {
        var restripeCommand = new Command("restripe", "Restripe MXF file with new start timecode");

        var inputOption = new Argument<FileInfo>("input")
        {
            Description = "Input MXF file path",
            Arity = ArgumentArity.ExactlyOne
        };

        // TODO: Change this to default to 00:00:00:00 if not provided
        var timecodeOption = new Option<string>("-t")
        {
            Aliases = { "--timecode" },
            Description = "New start timecode (HH:MM:SS:FF)",
            Required = true,
            Arity = ArgumentArity.ExactlyOne
        };

        var verboseOption = new Option<bool>("-V")
        {
            Aliases = { "--verbose" },
            Description = "Enable verbose output",
            Required = false,
            DefaultValueFactory = _ => false,
            Arity = ArgumentArity.ZeroOrOne
        };

        var printProgressOption = new Option<bool>("-pp")
        {
            Aliases = { "--print-progress" },
            Description = "Print progress during parsing",
            Required = false,
            DefaultValueFactory = _ => false,
            Arity = ArgumentArity.ZeroOrOne
        };

        restripeCommand.Arguments.Add(inputOption);
        restripeCommand.Options.Add(timecodeOption);
        restripeCommand.Options.Add(verboseOption);
        restripeCommand.Options.Add(printProgressOption);

        restripeCommand.SetAction(async (parseResult) =>
        {
            FileInfo? inputFile = parseResult.GetValue(inputOption);
            string? timecodeString = parseResult.GetValue(timecodeOption);
            bool verbose = parseResult.GetValue(verboseOption);
            bool printProgress = parseResult.GetValue(printProgressOption);

            if (inputFile == null || !inputFile.Exists)
            {
                Console.Error.WriteLine($"Error: Input file '{inputFile?.FullName ?? "null"}' does not exist.");
                await Task.FromResult(1);
                return;
            }

            if (string.IsNullOrEmpty(timecodeString))
            {
                Console.Error.WriteLine("Error: Timecode is required.");
                await Task.FromResult(1);
                return;
            }

            await Task.FromResult(Functions.Restripe(inputFile, timecodeString, verbose, printProgress));
        });

        return await Task.FromResult(restripeCommand);
    }

    /// <summary>
    /// Creates the convert command for converting between different teletext data formats.
    /// This command allows specifying input and output file paths, input and output formats,
    /// magazine numbers, rows to filter, line count for timecode incrementation, and options
    /// for using caption rows and keeping blank bytes.
    /// It also supports verbose output and reading from stdin if no input file is specified.
    /// The command will validate the input file, parse the specified options, and execute the
    /// conversion operation using the provided parameters.
    /// </summary>
    /// <returns>A Command object representing the convert command.</returns>
    /// <exception cref="ArgumentException">Thrown if the input file does not exist or is invalid.</exception>
    /// <exception cref="FormatException">Thrown if the input or output format is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the magazine number or rows are out of range.</exception>
    /// <exception cref="Exception">Thrown if an error occurs during the conversion process.</exception>
    /// <remarks>
    /// This command is designed to handle various teletext data formats, including VBI, T42, and MXF.
    /// It provides flexibility in specifying input and output formats, allowing for conversion between
    /// different representations of teletext data. The command also supports filtering by magazine and
    /// rows, enabling users to focus on specific parts of the teletext data.
    /// The command is intended for use in scenarios where teletext data needs to be converted or
    /// processed for further analysis or display. It is particularly useful for developers and users
    /// working with teletext data in various formats, providing a command-line interface for easy
    /// conversion and manipulation of teletext streams.
    /// </remarks>
    public static async Task<Command> CreateConvertCommand()
    {
        var convertCommand = new Command("convert", "Convert between different teletext data formats");

        var inputOption = new Argument<string?>("input")
        {
            Description = "Input file path",
            DefaultValueFactory = _ => "stdin",
            Arity = ArgumentArity.ZeroOrOne
        };

        var outputOption = new Argument<string?>("output")
        {
            Description = "Output file path",
            DefaultValueFactory = _ => "stdout",
            Arity = ArgumentArity.ZeroOrOne
        };

        var inputFormatOption = new Option<string?>("-if")
        {
            Aliases = { "--input-format" },
            Description = "Input format (auto-detected from file extension if not specified)",
            Required = false
        };

        inputFormatOption.CompletionSources.Add(ctx =>
        {
            return
            [
                new CompletionItem("bin", "MXF data stream"),
                new CompletionItem("vbi", "VBI format"),
                new CompletionItem("vbid", "VBI format (double width)"),
                new CompletionItem("t42", "T42 format"),
                new CompletionItem("mxf", "MXF container format")
            ];
        });

        var outputFormatOption = new Option<string>("-of")
        {
            Aliases = { "--output-format" },
            Description = "Output format",
            Required = false,
            DefaultValueFactory = _ => "t42"
        };

        outputFormatOption.CompletionSources.Add(ctx =>
        {
            return
            [
                new CompletionItem("vbi", "VBI format"),
                new CompletionItem("vbid", "VBI format (double width)"),
                new CompletionItem("t42", "T42 format")
            ];
        });

        var magazineOption = new Option<int?>("-m")
        {
            Aliases = { "--magazine" },
            Description = "Filter by magazine number",
            DefaultValueFactory = _ => Constants.DEFAULT_MAGAZINE,
            Required = false
        };

        magazineOption.CompletionSources.Add(ctx =>
        {
            List<CompletionItem> magazines = [];
            for (int i = 1; i <= 8; i++)
            {
                magazines.Add(new CompletionItem(
                    label: i.ToString("D2"),
                    sortText: i.ToString("D2")));
            }
            return magazines;
        });

        var rowsOption = new Option<string?>("-r")
        {
            Aliases = { "--rows" },
            Description = "Filter by number of rows (comma-separated or hyphen ranges, e.g., 1,2,5-8,15)",
            Required = false
        };

        rowsOption.CompletionSources.Add(ctx =>
        {
            List<CompletionItem> rows = [];
            for (int i = 0; i <= 24; i++)
            {
                rows.Add(new CompletionItem(
                    label: i.ToString("D2"),
                    sortText: i.ToString("D2")));
            }
            return rows;
        });

        var lineCountOption = new Option<int?>("-l")
        {
            Aliases = { "--line-count" },
            Description = "Number of lines per frame for timecode incrementation",
            Required = false,
            DefaultValueFactory = _ => 2
        };

        var capsOption = new Option<bool>("-c")
        {
            Aliases = { "--caps" },
            Description = "Use caption rows (1-24) instead of default rows (0-24)",
            Required = false,
            DefaultValueFactory = _ => false
        };

        var keepOption = new Option<bool>("-k")
        {
            Aliases = { "--keep" },
            Description = "Write blank bytes if rows or magazine doesn't match",
            Required = false,
            DefaultValueFactory = _ => false
        };

        var verboseOption = new Option<bool>("-V")
        {
            Aliases = { "--verbose" },
            Description = "Enable verbose output",
            Required = false,
            DefaultValueFactory = _ => false
        };

        convertCommand.Arguments.Add(inputOption);
        convertCommand.Arguments.Add(outputOption);
        convertCommand.Options.Add(inputFormatOption);
        convertCommand.Options.Add(outputFormatOption);
        convertCommand.Options.Add(magazineOption);
        convertCommand.Options.Add(rowsOption);
        convertCommand.Options.Add(lineCountOption);
        convertCommand.Options.Add(capsOption);
        convertCommand.Options.Add(keepOption);
        convertCommand.Options.Add(verboseOption);

        convertCommand.SetAction(async (parseResult) =>
        {
            // Get input file and validate it
            string? inputFilePath = parseResult.GetValue(inputOption);
            if (string.IsNullOrEmpty(inputFilePath) || inputFilePath.Equals("-", StringComparison.OrdinalIgnoreCase) || inputFilePath.Equals("stdin", StringComparison.OrdinalIgnoreCase))
            {
                // If no input file is specified, read from stdin
                inputFilePath = null;
            }
            else
            {
                // If input file is specified, ensure it exists
                inputFilePath = Path.GetFullPath(inputFilePath);
            }

            string? outputFilePath = parseResult.GetValue(outputOption);
            if (string.IsNullOrEmpty(outputFilePath) || outputFilePath.Equals("-", StringComparison.OrdinalIgnoreCase) || outputFilePath.Equals("stdout", StringComparison.OrdinalIgnoreCase))
            {
                // If no output file is specified, write to stdout
                outputFilePath = null;
            }
            else
            {
                // If output file is specified, ensure it exists
                outputFilePath = Path.GetFullPath(outputFilePath);
            }

            FileInfo? inputFile = inputFilePath != null ? new FileInfo(inputFilePath) : null;
            FileInfo? outputFile = outputFilePath != null ? new FileInfo(outputFilePath) : null;
            string? inputFormatString = parseResult.GetValue(inputFormatOption);
            string? outputFormatString = parseResult.GetValue(outputFormatOption);
            int magazine = parseResult.GetValue(magazineOption) ?? Constants.DEFAULT_MAGAZINE;
            string? rowsString = parseResult.GetValue(rowsOption);
            bool useCaps = parseResult.GetValue(capsOption);
            bool keepBlanks = parseResult.GetValue(keepOption);
            int lineCount = parseResult.GetValue(lineCountOption) ?? 2;

            // Determine which rows to use
            int[] rows;
            if (!string.IsNullOrEmpty(rowsString))
            {
                rows = ParseRowsString(rowsString);
            }
            else if (useCaps)
            {
                rows = Constants.CAPTION_ROWS;
            }
            else
            {
                rows = Constants.DEFAULT_ROWS;
            }
            bool verbose = parseResult.GetValue(verboseOption);

            // Validate input file
            if (inputFile != null && !inputFile.Exists && !inputFile.Name.Equals("-", StringComparison.OrdinalIgnoreCase) && !inputFile.Name.Equals("stdin", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Error: Input file '{inputFile.FullName}' does not exist.");
                await Task.FromResult(1);
                return;
            }

            // Determine input format
            Format inputFormat;
            if (!string.IsNullOrEmpty(inputFormatString))
            {
                inputFormat = Functions.ParseFormat(inputFormatString);
            }
            else if (inputFile != null && inputFile.Exists)
            {
                inputFormat = Functions.ParseFormat(Path.GetExtension(inputFile.Name).ToLowerInvariant());
            }
            else
            {
                inputFormat = Format.VBI; // Default to VBI for stdin
            }

            // Parse output format
            Format outputFormat;
            if (!string.IsNullOrEmpty(outputFormatString))
            {
                outputFormat = Functions.ParseFormat(outputFormatString);
            }
            else if (outputFile != null && outputFile.Exists)
            {
                outputFormat = Functions.ParseFormat(Path.GetExtension(outputFile.Name).ToLowerInvariant());
            }
            else
            {
                outputFormat = Format.T42; // Default to T42 if not specified
            }

            await Task.FromResult(Functions.Convert(inputFile, inputFormat, outputFormat, outputFile, magazine, rows, lineCount, verbose, keepBlanks));
        });

        return await Task.FromResult(convertCommand);
    }
}