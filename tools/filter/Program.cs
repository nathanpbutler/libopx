using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Formats;
using nathanbutlerDEV.libopx.Enums;
using System.Reflection;
using System.CommandLine;

namespace filter;

class Program
{
    static int Main(string[] args)
    {
        var inputArgument = new Argument<FileInfo?>("input")
        {
            Description = "Input file path (reads from stdin if not specified)"
        };

        var magazineOption = new Option<int?>("-m")
        {
            Description = $"Filter by magazine number (default: {Constants.DEFAULT_MAGAZINE})"
        };
        magazineOption.Aliases.Add("--magazine");

        var rowsOption = new Option<int[]?>("-r")
        {
            Description = "Filter by number of rows (comma-separated, default: caption rows)"
        };
        rowsOption.Aliases.Add("--rows");

        var inputFormatOption = new Option<string?>("-f")
        {
            Description = "Input format override: bin, vbi, vbid, t42 (default: vbi)"
        };
        inputFormatOption.Aliases.Add("--format");

        var lineCountOption = new Option<int?>("-l")
        {
            Description = "Number of lines per frame for timecode incrementation (default: 2)"
        };
        lineCountOption.Aliases.Add("--line-count");

        var versionOption = new Option<bool>("-v")
        {
            Description = "Show version information",
        };
        versionOption.Aliases.Add("--version");

        var verboseOption = new Option<bool>("--verbose")
        {
            Description = "Enable verbose output"
        };
        verboseOption.Aliases.Add("-V");

        var rootCommand = new RootCommand("Teletext filtering tool")
        {
            inputArgument,
            magazineOption,
            rowsOption,
            inputFormatOption,
            lineCountOption,
            versionOption,
            verboseOption
        };

        rootCommand.SetAction(parseResult =>
        {
            FileInfo? inputFile = parseResult.GetValue(inputArgument);
            int magazine = parseResult.GetValue(magazineOption) ?? Constants.DEFAULT_MAGAZINE;
            int[] rows = parseResult.GetValue(rowsOption) ?? Constants.CAPTION_ROWS;
            string inputFormatString = parseResult.GetValue(inputFormatOption) ?? "vbi";
            int lineCount = parseResult.GetValue(lineCountOption) ?? 2;
            bool versionMode = parseResult.GetValue(versionOption);
            bool verbose = parseResult.GetValue(verboseOption);

            rows = rows.Length == 0 ? Constants.CAPTION_ROWS : rows;

            LineFormat inputFormat = inputFile != null && inputFile.Exists
                ? ParseInputFormat(Path.GetExtension(inputFile.Name).TrimStart('.').ToLowerInvariant())
                : ParseInputFormat(inputFormatString);

            if (versionMode)
            {
                PrintVersion();
                return Task.FromResult(0);
            }

            return Task.FromResult(FilterInput(inputFile, magazine, rows, lineCount, inputFormat, verbose));
        });

        ParseResult parseResult = rootCommand.Parse(args);
        return parseResult.Invoke();
    }

    private static int FilterInput(FileInfo? inputFile, int magazine, int[] rows, int lineCount, LineFormat inputFormat, bool verbose)
    {
        try
        {
            
            if (verbose)
            {
                Console.WriteLine($"Magazine: {magazine}");
                Console.WriteLine($"Rows: [{string.Join(", ", rows)}]");
                Console.WriteLine($"Input format: {inputFormat}");
                if (inputFile != null && inputFile.Exists)
                    Console.WriteLine($"Input file: {inputFile.FullName}");
                else
                    Console.WriteLine("Reading from stdin");
            }

            return Filter(inputFile, magazine, rows, lineCount, inputFormat, verbose);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int Filter(FileInfo? inputFile, int magazine, int[] rows, int lineCount, LineFormat inputFormat, bool verbose)
    {
        switch (inputFormat)
        {
            case LineFormat.BIN:
                var bin = inputFile != null && inputFile.Exists
                    ? new BIN(inputFile.FullName)
                    : new BIN(Console.OpenStandardInput());
                foreach (var line in bin.Parse(magazine, rows))
                {
                    Console.WriteLine(line);
                }
                return 0; // Implement BIN processing logic here
            case LineFormat.VBI:
            case LineFormat.VBI_DOUBLE:
                var vbi = inputFile != null && inputFile.Exists
                    ? new VBI(inputFile.FullName)
                    : new VBI(Console.OpenStandardInput());
                vbi.LineCount = lineCount;
                foreach (var line in vbi.Parse(magazine, rows))
                {
                    Console.WriteLine(line);
                }
                return 0;
            case LineFormat.T42:
                var t42 = inputFile != null && inputFile.Exists
                    ? new T42(inputFile.FullName)
                    : new T42(Console.OpenStandardInput());
                t42.LineCount = lineCount;
                foreach (var line in t42.Parse(magazine, rows))
                {
                    Console.WriteLine(line);
                }
                return 0;
            default:
                Console.WriteLine($"Unsupported input format: {inputFormat}");
                return 1;
        }
    }

    private static LineFormat ParseInputFormat(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "bin" => LineFormat.BIN,
            "vbi" => LineFormat.VBI,
            "vbid" => LineFormat.VBI_DOUBLE,
            "t42" => LineFormat.T42,
            _ => LineFormat.VBI // Default to VBI if unknown format
        };
    }

    private static void PrintVersion()
    {
        Console.WriteLine($"filter version {Assembly.GetExecutingAssembly().GetName().Version}");
    }
}
