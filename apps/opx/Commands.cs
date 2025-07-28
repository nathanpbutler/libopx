using System.CommandLine;
using System.CommandLine.Completions;
using System.Reflection;
using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.opx;

public class Commands
{
    public static async Task<Command> CreateFilterCommand()
    {
        var filterCommand = new Command("filter", "Filter teletext data by magazine and rows");

        var inputOption = new Argument<FileInfo?>("input")
        {
            Description = "Input file path (reads from stdin if not specified)",
            DefaultValueFactory = null
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
                    label: i.ToString(),
                    sortText: i.ToString("D2")));
            }
            return magazines;
        });
        var rowsOption = new Option<int[]?>("-r")
        {
            Aliases = { "--rows" },
            Description = "Filter by number of rows (comma-separated)",
            DefaultValueFactory = _ => Constants.DEFAULT_ROWS,
            Required = false
        };
        rowsOption.CompletionSources.Add(ctx =>
        {
            List<CompletionItem> rows = [];
            for (int i = 0; i <= 24; i++)
            {
                rows.Add(new CompletionItem(
                    label: i.ToString(),
                    sortText: i.ToString("D2")));
            }
            return rows;
        });

        var inputFormatOption = new Option<string?>("-f")
        {
            Aliases = { "--format" },
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
            Description = "Number of lines per frame for timecode incrementation",
            Required = false,
            DefaultValueFactory = _ => 2
        };

        var verboseOption = new Option<bool>("-V")
        {
            Aliases = { "--verbose" },
            Description = "Enable verbose output",
            Required = false,
            DefaultValueFactory = _ => false
        };

        filterCommand.Arguments.Add(inputOption);
        filterCommand.Options.Add(magazineOption);
        filterCommand.Options.Add(rowsOption);
        filterCommand.Options.Add(inputFormatOption);
        filterCommand.Options.Add(lineCountOption);
        filterCommand.Options.Add(verboseOption);

        filterCommand.SetAction(async (parseResult) =>
        {
            FileInfo? inputFile = parseResult.GetValue(inputOption);
            if (inputFile != null && !inputFile.Exists && !inputFile.Name.Equals("-", StringComparison.OrdinalIgnoreCase) && !inputFile.Name.Equals("stdin", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Error: Input file '{inputFile.FullName}' does not exist.");
                await Task.FromResult(1);
                return;
            }
            int magazine = parseResult.GetValue(magazineOption) ?? Constants.DEFAULT_MAGAZINE;
            int[] rows = parseResult.GetValue(rowsOption) ?? Constants.DEFAULT_ROWS;
            string inputFormatString = parseResult.GetValue(inputFormatOption) ?? "vbi";
            int lineCount = parseResult.GetValue(lineCountOption) ?? 2;
            bool verbose = parseResult.GetValue(verboseOption);

            Format inputFormat = inputFile != null && inputFile.Exists
                ? Functions.ParseInputFormat(Path.GetExtension(inputFile.Name).ToLowerInvariant())
                : Functions.ParseInputFormat(inputFormatString);

            await Task.FromResult(Functions.Filter(inputFile, magazine, rows, lineCount, inputFormat, verbose));
        });

        return await Task.FromResult(filterCommand);
    }

    public static async Task<Command> CreateExtractCommand()
    {
        var extractCommand = new Command("extract", "Extract/demux streams from MXF files");

        var inputOption = new Argument<FileInfo>("input")
        {
            Description = "Input file path"
        };

        var outputOption = new Option<string?>("-o")
        {
            Aliases = { "--output" },
            Description = "Output base path - files will be created as <base>_d.raw, <base>_v.raw, etc",
            Required = false,
            DefaultValueFactory = _ => null
        };

        var keyOption = new Option<string?>("-k")
        {
            Aliases = { "--key" },
            Description = "Specify keys to extract",
            Required = false,
            DefaultValueFactory = _ => null
        };
        keyOption.CompletionSources.Add(ctx =>
        {
            return
            [
                new CompletionItem("d", "Data stream"),
                new CompletionItem("v", "Video stream"),
                new CompletionItem("s", "System stream"),
                new CompletionItem("t", "TimecodeComponent stream"),
                new CompletionItem("a", "Audio stream")
            ];
        });

        var demuxOption = new Option<bool>("-d")
        {
            Aliases = { "--demux" },
            Description = "Extract all keys found, output as <base>_<hexkey>.raw",
            Required = false,
            DefaultValueFactory = _ => false
        };

        var nameOption = new Option<bool>("-n")
        {
            Description = "Use Key/Essence names instead of hex keys (use with -d)",
            Required = false,
            DefaultValueFactory = _ => false
        };

        var klvOption = new Option<bool>("--klv")
        {
            Description = "Include key and length bytes in output files, use .klv extension",
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

    public static async Task<Command> CreateRestripeCommand()
    {
        var restripeCommand = new Command("restripe", "Restripe MXF file with new start timecode");

        var inputOption = new Argument<FileInfo>("input")
        {
            Description = "Input MXF file path"
        };

        var timecodeOption = new Option<string>("-t")
        {
            Aliases = { "--timecode" },
            Description = "New start timecode (HH:MM:SS:FF)",
            Required = true
        };

        var verboseOption = new Option<bool>("-V")
        {
            Aliases = { "--verbose" },
            Description = "Enable verbose output",
            Required = false,
            DefaultValueFactory = _ => false
        };

        var printProgressOption = new Option<bool>("-pp")
        {
            Aliases = { "--print-progress" },
            Description = "Print progress during parsing",
            Required = false,
            DefaultValueFactory = _ => false
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
}