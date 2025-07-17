using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.SMPTE;
using System.Reflection;
using System.CommandLine;

namespace nathanbutlerDEV.Tools.mxfExtract;

class Program
{
    public const int KeySize = 16;
    private static readonly byte[] _keyBuffer = new byte[KeySize];
    private static readonly Dictionary<KeyType, string> KeyTypeToExtension = new()
    {
        { KeyType.Data, "_d.raw" },
        { KeyType.Video, "_v.raw" },
        { KeyType.System, "_s.raw" },
        { KeyType.TimecodeComponent, "_t.raw" },
        { KeyType.Audio, "_a.raw" }
    };

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

            return ExtractMxf(inputFile, outputBasePath, keyString, demuxMode, useNames, klvMode, verbose);
        });

        ParseResult parseResult = rootCommand.Parse(args);
        return parseResult.Invoke();
    }

    private static async Task<int> ExtractMxf(FileInfo inputFile, string? outputBasePath, string? keyString, bool demuxMode, bool useNames, bool klvMode, bool verbose)
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

        // Parameter priority: --klv > -d/--demux > -n > -k/--key
        // Determine extraction mode and naming scheme
        bool extractAllKeys = demuxMode;
        bool useKeyNames = useNames && demuxMode;
        
        var targetKeys = new List<KeyType>();
        if (!extractAllKeys)
        {
            if (!string.IsNullOrEmpty(keyString))
            {
                targetKeys = ParseKeys(keyString);
            }
            else
            {
                targetKeys.Add(KeyType.Data);
                Console.WriteLine("No keys specified, defaulting to Data.");
            }
        }

        // Print active modes
        if (klvMode)
        {
            Console.WriteLine("KLV mode enabled - key and length bytes will be included in output files.");
        }
        
        if (extractAllKeys)
        {
            Console.WriteLine("Demux mode enabled - all keys will be extracted.");
            if (useKeyNames)
            {
                Console.WriteLine("Name mode enabled - using Key/Essence names instead of hex keys.");
            }
            else
            {
                Console.WriteLine("Using hex key names for output files.");
            }
        }

        var outputStreams = new Dictionary<KeyType, FileStream>();
        var demuxStreams = new Dictionary<string, FileStream>();
        var berLengthBuffer = new List<byte>();
        var foundKeys = new HashSet<string>();

        try
        {
            using var reader = new FileStream(inputFile.FullName, FileMode.Open, FileAccess.Read);
            Console.WriteLine($"Processing MXF file: {inputFile.FullName}");
            
            try
            {
                while (reader.Position < reader.Length)
                {
                    var keyBytesRead = await reader.ReadAsync(_keyBuffer, 0, KeySize);
                    if (keyBytesRead != KeySize) throw new EndOfStreamException("Unexpected end of stream while reading key.");

                    var keyType = Keys.GetKeyType(_keyBuffer.AsSpan(0, KeySize));
                    int length = ReadBerLength(reader, berLengthBuffer);
                    if (length < 0) throw new EndOfStreamException("Unexpected end of stream while reading length.");

                    // Check if we should extract this key
                    bool shouldExtract = extractAllKeys || targetKeys.Contains(keyType);
                    
                    if (shouldExtract)
                    {
                        FileStream outputStream;
                        
                        if (extractAllKeys)
                        {
                            // Demux mode: create unique file for each key
                            var keyIdentifier = useKeyNames ? GetKeyName(_keyBuffer) : BytesToHexString(_keyBuffer);
                            
                            if (!foundKeys.Contains(keyIdentifier))
                            {
                                Console.WriteLine($"Found key: {keyIdentifier}, length: {length}");
                                if (!verbose)
                                    foundKeys.Add(keyIdentifier);
                            }
                            
                            if (!demuxStreams.TryGetValue(keyIdentifier, out outputStream!))
                            {
                                var fileExtension = klvMode ? ".klv" : ".raw";
                                var outputPath = $"{outputBasePath}_{keyIdentifier}{fileExtension}";
                                outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                                demuxStreams[keyIdentifier] = outputStream;
                            }
                        }
                        else
                        {
                            var keyIdentifier = useKeyNames ? GetKeyName(_keyBuffer) : BytesToHexString(_keyBuffer);

                            if(!foundKeys.Contains(keyIdentifier))
                            {
                                Console.WriteLine($"Found required key: {keyType}, length: {length}");
                                if (!verbose)
                                    foundKeys.Add(keyIdentifier);
                            }
                            
                            if (!outputStreams.TryGetValue(keyType, out outputStream!))
                            {
                                if (KeyTypeToExtension.TryGetValue(keyType, out var extension))
                                {
                                    if (klvMode)
                                    {
                                        extension = extension.Replace(".raw", ".klv");
                                    }
                                    var outputPath = outputBasePath + extension;
                                    outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                                    outputStreams[keyType] = outputStream;
                                }
                                else
                                {
                                    // Skip if no extension mapping
                                    reader.Seek(length, SeekOrigin.Current);
                                    continue;
                                }
                            }
                        }
                        
                        // Write KLV header if requested
                        if (klvMode)
                        {
                            await outputStream.WriteAsync(_keyBuffer, 0, KeySize);
                            await outputStream.WriteAsync(berLengthBuffer.ToArray(), 0, berLengthBuffer.Count);
                        }
                        
                        // Write essence data
                        var essenceData = new byte[length];
                        var bytesRead = await reader.ReadAsync(essenceData, 0, length);
                        if (bytesRead != length) throw new EndOfStreamException("Unexpected end of stream while reading value.");
                        await outputStream.WriteAsync(essenceData, 0, length);
                    }
                    else
                    {
                        // Skip this key
                        reader.Seek(length, SeekOrigin.Current);
                    }
                }
                
                Console.WriteLine($"Finished processing MXF file: {inputFile.FullName}");
            }
            finally
            {
                foreach (var stream in outputStreams.Values)
                {
                    stream?.Dispose();
                }
                
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
        
        return 0;
    }

    private static void PrintVersion()
    {
        Console.WriteLine($"mxfExtract version {Assembly.GetExecutingAssembly().GetName().Version}");
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
            keys.Add(KeyType.Data);
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
        Type[] typesToSearch = { typeof(Essence), typeof(Keys) };
        
        foreach (var type in typesToSearch)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(byte[]))
                {
                    var fieldValue = (byte[])field.GetValue(null)!;
                    
                    if (field.Name == "FourCc")
                        continue;
                    
                    if (fieldValue.Length <= keyBytes.Length)
                    {
                        var keyPrefix = keyBytes.Take(fieldValue.Length).ToArray();
                        if (keyPrefix.SequenceEqual(fieldValue))
                        {
                            return field.Name.TrimStart('_');
                        }
                    }
                }
            }
        }
        
        var keyType = Keys.GetKeyType(keyBytes.AsSpan());
        if (keyType != KeyType.Unknown)
        {
            return keyType.ToString();
        }
        
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