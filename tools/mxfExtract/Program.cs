using nathanbutlerDEV.libopx;

namespace mxfExtract;

class Program
{
    public const int KeySize = 16;
    private static readonly byte[] _keyBuffer = new byte[KeySize];
    private static readonly Dictionary<KeyType, string> KeyTypeToExtension = new()
    {
        { KeyType.Data, "_d.bin" },
        { KeyType.Video, "_v.bin" },
        { KeyType.System, "_s.bin" },
        { KeyType.TimecodeComponent, "_t.bin" },
        { KeyType.Audio, "_a.bin" }
    };

    static int Main(string[] args)
    {
        if (args.Length < 1) return PrintHelp(1);

        if (args.Contains("-h") || args.Contains("--help")) return PrintHelp(0);
        if (args.Contains("-v") || args.Contains("--version")) return PrintVersion();

        string inputFilePath = args[0];
        string outputBasePath = Path.ChangeExtension(inputFilePath, null);

        var keys = new List<KeyType>();
        var outputStreams = new Dictionary<KeyType, FileStream>();

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-o":
                case "--output":
                    if (i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]))
                    {
                        outputBasePath = args[++i];
                        Console.WriteLine($"Output base path specified: {outputBasePath}");
                    }
                    else
                    {
                        Console.WriteLine("Error: No output file specified after -o or --output option.");
                        return 1;
                    }
                    break;
                case "-k":
                case "--key":
                    if (i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]))
                    {
                        keys = ParseKeys(args[++i]);
                    }
                    else
                    {
                        Console.WriteLine("Error: No keys specified after -k or --key option.");
                        return 1;
                    }
                    break;
                default:
                    Console.WriteLine($"Unknown option: {args[i]}");
                    return 1;
            }
        }

        if (!File.Exists(inputFilePath))
        {
            Console.WriteLine($"File not found: {inputFilePath}");
            return 1;
        }
        try
        {
            using var reader = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read);
            // Assuming MXF file processing logic is implemented here
            Console.WriteLine($"Processing MXF file: {inputFilePath}");
            
            try
            {
                while (reader.Position < reader.Length)
                {
                    var keyBytesRead = reader.Read(_keyBuffer, 0, KeySize);
                    if (keyBytesRead != KeySize) throw new EndOfStreamException("Unexpected end of stream while reading key.");

                    var keyType = Keys.GetKeyType(_keyBuffer.AsSpan(0, KeySize));

                    // Read the next BER length
                    int length = ReadBerLength(reader);
                    if (length < 0) throw new EndOfStreamException("Unexpected end of stream while reading length.");

                    // Check for system metadata keys
                    if (keys.Contains(keyType))
                    {
                        Console.WriteLine($"Found required key: {keyType}, length: {length}");
                        
                        // Create output stream if it doesn't exist
                        if (!outputStreams.ContainsKey(keyType))
                        {
                            if (KeyTypeToExtension.TryGetValue(keyType, out var extension))
                            {
                                var outputPath = outputBasePath + extension;
                                outputStreams[keyType] = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                                Console.WriteLine($"Created output file: {outputPath}");
                            }
                        }
                        
                        // Read and write the data
                        var value = new byte[length];
                        var bytesRead = reader.Read(value, 0, length);
                        if (bytesRead != length) throw new EndOfStreamException("Unexpected end of stream while reading value.");
                        
                        // Write the data to the appropriate output file
                        outputStreams[keyType].Write(value, 0, length);
                    }
                    else
                    {
                        // Skip the data if it's not a system metadata key
                        reader.Seek(length, SeekOrigin.Current);
                    }
                }
                
                Console.WriteLine($"Finished processing MXF file: {inputFilePath}");
                return 0;
            }
            finally
            {
                // Dispose all output streams
                foreach (var stream in outputStreams.Values)
                {
                    stream?.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing file: {ex.Message}");
            return 1;
        }
    }

    // Print Help message
    private static int PrintHelp(int exitCode = 0)
    {
        Console.WriteLine("Usage: mxfExtract <input.mxf> [options]\n");
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, --help            Show this help message");
        Console.WriteLine("  -v, --version         Show version information");
        Console.WriteLine("  -o, --output <base>   Specify output base path - files will be created as <base>_d.bin, <base>_v.bin, etc");
        Console.WriteLine("  -k, --key <string[]>  Specify keys to extract (d,v,s,t,a - comma-separated)\n");
        Console.WriteLine("Example: mxfExtract input.mxf -o output -k d,v,s\n");
        Console.WriteLine("Keys:");
        Console.WriteLine("  d - Data");
        Console.WriteLine("  v - Video");
        Console.WriteLine("  s - System");
        Console.WriteLine("  t - TimecodeComponent");
        Console.WriteLine("  a - Audio");
        return exitCode;
    }

    // Print Version information
    private static int PrintVersion()
    {
        Console.WriteLine($"mxfExtract version {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
        return 0;
    }

    private static List<KeyType> ParseKeys(string arg)
    {
        var keys = new List<KeyType>();
        var keyStrings = arg.Split(',').Select(k => k.Trim().ToLowerInvariant());
        foreach (var keyString in keyStrings)
        {
            switch (keyString)
            {
                case "d":
                    keys.Add(KeyType.Data);
                    Console.WriteLine("Data key specified.");
                    break;
                case "v":
                    keys.Add(KeyType.Video);
                    Console.WriteLine("Video key specified.");
                    break;
                case "s":
                    keys.Add(KeyType.System);
                    Console.WriteLine("System key specified.");
                    break;
                case "t":
                    keys.Add(KeyType.TimecodeComponent);
                    Console.WriteLine("TimecodeComponent key specified.");
                    break;
                case "a":
                    keys.Add(KeyType.Audio);
                    Console.WriteLine("Audio key specified.");
                    break;
                default:
                    Console.WriteLine($"Unknown key type: {keyString}");
                    break;
            }
        }
        if (keys.Count == 0)
        {
            keys.Add(KeyType.Data); // Default to Data if no keys specified
            Console.WriteLine("No keys specified, defaulting to Data.");
        }
        return keys;
    }

    private static int ReadBerLength(Stream input)
    {
        var firstByte = input.ReadByte();
        if (firstByte == -1) return -1;

        if ((firstByte & 0x80) == 0)
        {
            return firstByte;
        }
        else
        {
            var byteCount = firstByte & 0x7F;
            if (byteCount > 8) return -1;

            var length = 0;
            for (var i = 0; i < byteCount; i++)
            {
                var b = input.ReadByte();
                if (b == -1) return -1;
                length = (length << 8) | b;
            }

            return length;
        }
    }
}
