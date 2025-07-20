using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Formats;
using nathanbutlerDEV.libopx.Enums;
using System.Reflection;
using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Parsing;

namespace filter;

class Program
{
    static int Main(string[] args)
    {
        // TODO: Change this to object that either reads from a file or stdin
        var inputArgument = new Argument<FileInfo?>("input")
        {
            Description = "Input file path (reads from stdin if not specified)",
        };

        var magazineOption = new Option<int?>("-m")
        {
            Description = $"Filter by magazine number",
            DefaultValueFactory = parseResult => Constants.DEFAULT_MAGAZINE
        };
        magazineOption.Aliases.Add("--magazine");

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
            Description = $"Filter by number of rows (comma-separated)",
            DefaultValueFactory = parseResult => Constants.DEFAULT_ROWS
        };
        rowsOption.Aliases.Add("--rows");

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
            if (inputFile != null && !inputFile.Exists && !inputFile.Name.Equals("-", StringComparison.OrdinalIgnoreCase) && !inputFile.Name.Equals("stdin", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Error: Input file '{inputFile.FullName}' does not exist.");
                return Task.FromResult(1);
            }
            int magazine = parseResult.GetValue(magazineOption) ?? Constants.DEFAULT_MAGAZINE;
            int[] rows = parseResult.GetValue(rowsOption) ?? Constants.DEFAULT_ROWS;
            string inputFormatString = parseResult.GetValue(inputFormatOption) ?? "vbi";
            int lineCount = parseResult.GetValue(lineCountOption) ?? 2;
            bool versionMode = parseResult.GetValue(versionOption);
            bool verbose = parseResult.GetValue(verboseOption);

            LineFormat inputFormat = inputFile != null && inputFile.Exists
                ? Functions.ParseInputFormat(Path.GetExtension(inputFile.Name).ToLowerInvariant())
                : Functions.ParseInputFormat(inputFormatString);

            if (versionMode)
            {
                PrintVersion();
                return Task.FromResult(0);
            }

            return Task.FromResult(Functions.Filter(inputFile, magazine, rows, lineCount, inputFormat, verbose));
        });

        ParseResult parseResult = rootCommand.Parse(args);
        return parseResult.Invoke();
    }

    private static void PrintVersion()
    {
        Console.WriteLine($"filter version {Assembly.GetExecutingAssembly().GetName().Version}");
    }
}
