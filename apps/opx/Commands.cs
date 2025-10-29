using System.CommandLine;
using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.opx;

public class Commands
{
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
    public static Command CreateFilterCommand()
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
            Description = "Filter by magazine number (default: all magazines)"
        };

        var rowsOption = new Option<string?>("-r")
        {
            Aliases = { "--rows" },
            Description = "Filter by number of rows (comma-separated or hyphen ranges, e.g., 1,2,5-8,15)"
        };

        var inputFormatOption = new Option<string?>("-if")
        {
            Aliases = { "--input-format" },
            Description = "Input format"
        };
        inputFormatOption.CompletionSources.Add(CommandHelpers.CreateInputFormatCompletionSource());

        var lineCountOption = new Option<int?>("-l")
        {
            Aliases = { "--line-count" },
            Description = "Number of lines per frame for timecode incrementation",
            DefaultValueFactory = _ => 2
        };

        var capsOption = new Option<bool>("-c")
        {
            Aliases = { "--caps" },
            Description = "Use caption rows (1-24) instead of default rows (0-31)"
        };

        var verboseOption = new Option<bool>("-V")
        {
            Aliases = { "--verbose" },
            Description = "Enable verbose output"
        };

        filterCommand.Arguments.Add(inputOption);
        filterCommand.Options.Add(inputFormatOption);
        filterCommand.Options.Add(magazineOption);
        filterCommand.Options.Add(rowsOption);
        filterCommand.Options.Add(lineCountOption);
        filterCommand.Options.Add(capsOption);
        filterCommand.Options.Add(verboseOption);

        filterCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            try
            {
                string? inputFilePath = parseResult.GetValue(inputOption);
                FileInfo? inputFile = CommandHelpers.ValidateInputFile(inputFilePath);
                
                int? magazine = parseResult.GetValue(magazineOption);
                string? rowsString = parseResult.GetValue(rowsOption);
                bool useCaps = parseResult.GetValue(capsOption);
                string? inputFormatString = parseResult.GetValue(inputFormatOption);
                int lineCount = parseResult.GetValue(lineCountOption) ?? 2;
                bool verbose = parseResult.GetValue(verboseOption);

                int[] rows = CommandHelpers.DetermineRows(rowsString, useCaps);
                Format inputFormat = CommandHelpers.DetermineFormatFromFile(inputFile, inputFormatString, Format.VBI);

                return await Functions.FilterAsync(inputFile, magazine, rows, lineCount, inputFormat, verbose, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Operation was cancelled.");
                return 130;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        return filterCommand;
    }

    /// <summary>
    /// Creates the extract command for extracting/demuxing streams from MXF files.
    /// This command allows specifying input files, output paths, keys to extract, and options for
    /// demuxing and naming conventions. It also supports verbose output and KLV mode.
    /// </summary>
    /// <returns>A Command object representing the extract command.</returns>
    /// <exception cref="ArgumentException">Thrown if the input file does not exist or is invalid.</exception>
    public static Command CreateExtractCommand()
    {
        var extractCommand = new Command("extract", "Extract/demux streams from MXF files");

        var inputOption = new Argument<FileInfo>("input")
        {
            Description = "Input file path",
            Arity = ArgumentArity.ExactlyOne
        };

        var outputOption = new Option<string?>("-o")
        {
            Aliases = { "--output" },
            Description = "Output base path - files will be created as <base>_d.raw, <base>_v.raw, etc",
            Arity = ArgumentArity.ZeroOrOne
        };

        var keyOption = new Option<string?>("-k")
        {
            Aliases = { "--key" },
            Description = "Specify keys to extract",
            Arity = ArgumentArity.ZeroOrOne
        };
        keyOption.CompletionSources.Add(CommandHelpers.CreateKeyCompletionSource());

        var demuxOption = new Option<bool>("-d")
        {
            Aliases = { "--demux" },
            Description = "Extract all keys found, output as <base>_<hexkey>.raw"
        };

        var nameOption = new Option<bool>("-n")
        {
            Description = "Use Key/Essence names instead of hex keys (use with -d)"
        };

        var klvOption = new Option<bool>("--klv")
        {
            Description = "Include key and length bytes in output files, use .klv extension"
        };

        var verboseOption = new Option<bool>("-V")
        {
            Aliases = { "--verbose" },
            Description = "Enable verbose output"
        };

        extractCommand.Arguments.Add(inputOption);
        extractCommand.Options.Add(outputOption);
        extractCommand.Options.Add(keyOption);
        extractCommand.Options.Add(demuxOption);
        extractCommand.Options.Add(nameOption);
        extractCommand.Options.Add(klvOption);
        extractCommand.Options.Add(verboseOption);

        extractCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            try
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
                    Console.Error.WriteLine("Error: Input file is required and must exist.");
                    return 1;
                }

                return await Functions.ExtractAsync(inputFile, outputBasePath, keyString, demuxMode, useNames, klvMode, verbose, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Operation was cancelled.");
                return 130;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        return extractCommand;
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
    public static Command CreateRestripeCommand()
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
            Description = "Enable verbose output"
        };

        var printProgressOption = new Option<bool>("-pp")
        {
            Aliases = { "--print-progress" },
            Description = "Print progress during parsing"
        };

        restripeCommand.Arguments.Add(inputOption);
        restripeCommand.Options.Add(timecodeOption);
        restripeCommand.Options.Add(verboseOption);
        restripeCommand.Options.Add(printProgressOption);

        restripeCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            try
            {
                FileInfo? inputFile = parseResult.GetValue(inputOption);
                string? timecodeString = parseResult.GetValue(timecodeOption);
                bool verbose = parseResult.GetValue(verboseOption);
                bool printProgress = parseResult.GetValue(printProgressOption);

                if (inputFile == null || !inputFile.Exists)
                {
                    Console.Error.WriteLine($"Error: Input file '{inputFile?.FullName ?? "null"}' does not exist.");
                    return 1;
                }

                if (string.IsNullOrEmpty(timecodeString))
                {
                    Console.Error.WriteLine("Error: Timecode is required.");
                    return 1;
                }

                return await Functions.RestripeAsync(inputFile, timecodeString, verbose, printProgress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Operation was cancelled.");
                return 130;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        return restripeCommand;
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
    public static Command CreateConvertCommand()
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
            Description = "Input format (auto-detected from file extension if not specified)"
        };
        inputFormatOption.CompletionSources.Add(CommandHelpers.CreateInputFormatCompletionSource());

        var outputFormatOption = new Option<string?>("-of")
        {
            Aliases = { "--output-format" },
            Description = "Output format"
        };
        outputFormatOption.CompletionSources.Add(CommandHelpers.CreateOutputFormatCompletionSource());

        var magazineOption = new Option<int?>("-m")
        {
            Aliases = { "--magazine" },
            Description = "Filter by magazine number (default: all magazines)"
        };

        var rowsOption = new Option<string?>("-r")
        {
            Aliases = { "--rows" },
            Description = "Filter by number of rows (comma-separated or hyphen ranges, e.g., 1,2,5-8,15)"
        };

        var lineCountOption = new Option<int?>("-l")
        {
            Aliases = { "--line-count" },
            Description = "Number of lines per frame for timecode incrementation",
            DefaultValueFactory = _ => 2
        };

        var capsOption = new Option<bool>("-c")
        {
            Aliases = { "--caps" },
            Description = "Use caption rows (1-24) instead of default rows (0-31)"
        };

        var keepOption = new Option<bool>("--keep")
        {
            Description = "Write blank bytes if rows or magazine doesn't match"
        };

        var verboseOption = new Option<bool>("-V")
        {
            Aliases = { "--verbose" },
            Description = "Enable verbose output"
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

        convertCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            try
            {
                string? inputFilePath = parseResult.GetValue(inputOption);
                string? outputFilePath = parseResult.GetValue(outputOption);
                
                FileInfo? inputFile = CommandHelpers.ValidateInputFile(inputFilePath);
                FileInfo? outputFile = CommandHelpers.ParseOutputFile(outputFilePath);
                
                string? inputFormatString = parseResult.GetValue(inputFormatOption);
                string? outputFormatString = parseResult.GetValue(outputFormatOption);
                int? magazine = parseResult.GetValue(magazineOption);
                string? rowsString = parseResult.GetValue(rowsOption);
                bool useCaps = parseResult.GetValue(capsOption);
                bool keepBlanks = parseResult.GetValue(keepOption);
                int lineCount = parseResult.GetValue(lineCountOption) ?? 2;
                bool verbose = parseResult.GetValue(verboseOption);

                int[] rows = CommandHelpers.DetermineRows(rowsString, useCaps);
                
                Format inputFormat = CommandHelpers.DetermineFormat(inputFormatString, inputFile, Format.VBI);
                Format outputFormat = CommandHelpers.DetermineFormat(outputFormatString, outputFile, Format.T42);

                return await Functions.ConvertAsync(inputFile, inputFormat, outputFormat, outputFile, magazine, rows, lineCount, verbose, keepBlanks, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Operation was cancelled.");
                return 130;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        return convertCommand;
    }
}