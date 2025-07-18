using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Formats;
using System.Reflection;
using System.CommandLine;

namespace nathanbutlerDEV.Tools.mxfExtract;

class Program
{

    static int Main(string[] args)
    {
        var inputArgument = new Argument<FileInfo>("input")
        {
            Description = "Input MXF file path"
        };

        var outputOption = new Option<string?>("-o")
        {
            Description = "Specify output base path - files will be created as <base>_d.raw, <base>_v.raw, etc"
        };
        outputOption.Aliases.Add("--output");

        var keyOption = new Option<string?>("-k")
        {
            Description = "Specify keys to extract (d,v,s,t,a - comma-separated)"
        };
        keyOption.Aliases.Add("--key");

        var demuxOption = new Option<bool>("-d")
        {
            Description = "Extract all keys found, output as <base>_<hexkey>.raw"
        };
        demuxOption.Aliases.Add("--demux");

        var nameOption = new Option<bool>("-n")
        {
            Description = "Use Key/Essence names instead of hex keys (use with -d)"
        };

        var klvOption = new Option<bool>("--klv")
        {
            Description = "Include key and length bytes in output files, use .klv extension"
        };

        var versionOption = new Option<bool>("-v")
        {
            Description = "Show version information"
        };
        versionOption.Aliases.Add("--version");

        var verboseOption = new Option<bool>("--verbose")
        {
            Description = "Enable verbose output",
            Required = false
        };
        verboseOption.Aliases.Add("-V");

        var rootCommand = new RootCommand("MXF extraction tool")
        {
            inputArgument,
            outputOption,
            keyOption,
            demuxOption,
            nameOption,
            klvOption,
            versionOption,
            verboseOption
        };
        
        rootCommand.SetAction(parseResult =>
        {
            // Fill in these blanks with the actual logic
            FileInfo? inputFile = parseResult.GetValue(inputArgument);
            string? outputBasePath = parseResult.GetValue(outputOption);
            string? keyString = parseResult.GetValue(keyOption);
            bool demuxMode = parseResult.GetValue(demuxOption);
            bool useNames = parseResult.GetValue(nameOption);
            bool klvMode = parseResult.GetValue(klvOption);
            bool versionMode = parseResult.GetValue(versionOption);
            bool verbose = parseResult.GetValue(verboseOption);

            if (versionMode)
            {
                PrintVersion();
                return Task.FromResult(0);
            }

            if (inputFile == null || !inputFile.Exists)
            {
                Console.WriteLine("Input file is required and must exist.");
                return Task.FromResult(1);
            }

            return Task.FromResult(ExtractMxf(inputFile, outputBasePath, keyString, demuxMode, useNames, klvMode, verbose));
        });

        ParseResult parseResult = rootCommand.Parse(args);
        return parseResult.Invoke();
    }

    private static int ExtractMxf(FileInfo inputFile, string? outputBasePath, string? keyString, bool demuxMode, bool useNames, bool klvMode, bool verbose)
    {
        if (!inputFile.Exists)
        {
            Console.WriteLine($"File not found: {inputFile.FullName}");
            return 1;
        }

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

    private static void PrintVersion()
    {
        Console.WriteLine($"mxfExtract version {Assembly.GetExecutingAssembly().GetName().Version}");
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

}