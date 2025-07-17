using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.SMPTE;
using System.Reflection;
using System.CommandLine;

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
        bool demuxMode = false;
        bool klvMode = false;
        bool useNames = false;

        var keys = new List<KeyType>();
        var outputStreams = new Dictionary<KeyType, FileStream>();
        var demuxStreams = new Dictionary<string, FileStream>();
        var berLengthBuffer = new List<byte>();
        var foundKeys = new HashSet<string>();

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
                case "-d":
                case "--demux":
                    demuxMode = true;
                    Console.WriteLine("Demux mode enabled - all keys will be extracted with hex key names.");
                    break;
                case "--klv":
                    klvMode = true;
                    Console.WriteLine("KLV mode enabled - key and length bytes will be included in output files.");
                    break;
                case "-n":
                    useNames = true;
                    Console.WriteLine("Name mode enabled - using Key/Essence names instead of hex keys.");
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
                    int length = ReadBerLength(reader, berLengthBuffer);
                    if (length < 0) throw new EndOfStreamException("Unexpected end of stream while reading length.");

                    if (demuxMode)
                    {
                        // In demux mode, extract all keys
                        var keyIdentifier = useNames ? GetKeyName(_keyBuffer) : BytesToHexString(_keyBuffer);
                        
                        // Only output "Found key" message once per unique key
                        if (!foundKeys.Contains(keyIdentifier))
                        {
                            Console.WriteLine($"Found key: {keyIdentifier}, length: {length}");
                            foundKeys.Add(keyIdentifier);
                        }
                        
                        // Create output stream if it doesn't exist
                        if (!demuxStreams.TryGetValue(keyIdentifier, out FileStream? value1))
                        {
                            var outputPath = $"{outputBasePath}_{keyIdentifier}.bin";
                            value1 = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                            demuxStreams[keyIdentifier] = value1;
                            // Console.WriteLine($"Created output file: {outputPath}");
                        }
                        
                        // Write KLV data if requested
                        if (klvMode)
                        {
                            // Write the key bytes
                            value1.Write(_keyBuffer, 0, KeySize);
                            // Write the length bytes
                            value1.Write(berLengthBuffer.ToArray(), 0, berLengthBuffer.Count);
                        }
                        
                        // Read and write the data
                        var value = new byte[length];
                        var bytesRead = reader.Read(value, 0, length);
                        if (bytesRead != length) throw new EndOfStreamException("Unexpected end of stream while reading value.");
                        value1.Write(value, 0, length);
                    }
                    else if (keys.Contains(keyType))
                    {
                        Console.WriteLine($"Found required key: {keyType}, length: {length}");
                        
                        // Create output stream if it doesn't exist
                        if (!outputStreams.ContainsKey(keyType))
                        {
                            if (KeyTypeToExtension.TryGetValue(keyType, out var extension))
                            {
                                var outputPath = outputBasePath + extension;
                                outputStreams[keyType] = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                                //Console.WriteLine($"Created output file: {outputPath}");
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
                
                // Dispose all demux streams
                foreach (var stream in demuxStreams.Values)
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
        Console.WriteLine("  -k, --key <string[]>  Specify keys to extract (d,v,s,t,a - comma-separated)");
        Console.WriteLine("  -d, --demux           Extract all keys found, output as <base>_<hexkey>.bin");
        Console.WriteLine("  -n                    Use Key/Essence names instead of hex keys (use with -d)");
        Console.WriteLine("      --klv             Include key and length bytes in output files (use with -d)\n");
        Console.WriteLine("Example: mxfExtract input.mxf -o output -k d,v,s");
        Console.WriteLine("Example: mxfExtract input.mxf -o output -d");
        Console.WriteLine("Example: mxfExtract input.mxf -o output -d -n");
        Console.WriteLine("Example: mxfExtract input.mxf -o output -d --klv\n");
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

    private static string BytesToHexString(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GetKeyName(byte[] keyBytes)
    {
        // Search Essence first (more specific 16-byte keys), then Keys (more generic patterns)
        Type[] typesToSearch = { typeof(Essence), typeof(Keys) };
        
        foreach (var type in typesToSearch)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(byte[]))
                {
                    var fieldValue = (byte[])field.GetValue(null)!;
                    
                    // Skip FourCc as it's the universal prefix for all MXF keys
                    if (field.Name == "FourCc")
                        continue;
                    
                    // Handle variable-length keys - check if the key starts with the field value
                    // or if they match exactly
                    if (fieldValue.Length <= keyBytes.Length)
                    {
                        var keyPrefix = keyBytes.Take(fieldValue.Length).ToArray();
                        if (keyPrefix.SequenceEqual(fieldValue))
                        {
                            // Return the field name without underscores for better readability
                            return field.Name.TrimStart('_');
                        }
                    }
                }
            }
        }
        
        // If not found in either class, try to get KeyType
        var keyType = Keys.GetKeyType(keyBytes.AsSpan());
        if (keyType != KeyType.Unknown)
        {
            return keyType.ToString();
        }
        
        // For unknown keys, return the hex representation
        return BytesToHexString(keyBytes);
    }

    private static int ReadBerLength(Stream input, List<byte> lengthBuffer)
    {
        lengthBuffer.Clear();
        var firstByte = input.ReadByte();
        if (firstByte == -1) return -1;
        
        lengthBuffer.Add((byte)firstByte);

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
                lengthBuffer.Add((byte)b);
                length = (length << 8) | b;
            }

            return length;
        }
    }
}
