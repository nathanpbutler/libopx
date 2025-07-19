using System.Diagnostics.CodeAnalysis;
using nathanbutlerDEV.libopx.SMPTE;

namespace nathanbutlerDEV.libopx.Formats;

public class MXF : IDisposable
{
    // Reusable buffers to reduce allocations
    private readonly byte[] _keyBuffer = new byte[Constants.KLV_KEY_SIZE];
    private readonly byte[] _smpteBuffer = new byte[Constants.SMPTE_TIMECODE_SIZE];
    public required FileInfo InputFile { get; set; }
    public FileInfo? OutputFile { get; set; } = null; // If null, write to stdout
    private Stream? _outputStream;
    public required Stream Input { get; set; }
    public Stream Output => _outputStream ??= OutputFile == null ? Console.OpenStandardOutput() : OutputFile.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
    public Timecode StartTimecode { get; set; } = new Timecode(0); // Start timecode of the MXF file
    public List<Timecode> SMPTETimecodes { get; set; } = []; // List of SMPTE timecodes per-frame in the MXF file
    public List<Packet> Packets { get; set; } = []; // List of packets parsed from the MXF data stream
    public List<KeyType> RequiredKeys { get; set; } = []; // List of required keys for parsing
    public bool CheckSequential { get; set; } = true; // Check if SMPTE timecodes are sequential
    public bool Extract { get; set; } = false; // Extract required streams from the MXF file
    public bool DemuxMode { get; set; } = false; // Extract all keys found, output as separate files
    public bool UseKeyNames { get; set; } = false; // Use Key/Essence names instead of hex keys (use with DemuxMode)
    public bool KlvMode { get; set; } = false; // Include key and length bytes in output files
    public string? OutputBasePath { get; set; } // Base path for extracted files
    private readonly Dictionary<KeyType, string> _keyTypeToExtension = new()
    {
        { KeyType.Data, "_d.raw" },
        { KeyType.Video, "_v.raw" },
        { KeyType.System, "_s.raw" },
        { KeyType.TimecodeComponent, "_t.raw" },
        { KeyType.Audio, "_a.raw" }
    };
    
    // Cache the previous timecode for sequential checking to avoid recalculation
    private Timecode? _lastTimecode;
    
    // Extraction-related fields
    private readonly Dictionary<KeyType, FileStream> _outputStreams = new();
    private readonly Dictionary<string, FileStream> _demuxStreams = new();
    private readonly List<byte> _berLengthBuffer = new();
    private readonly HashSet<string> _foundKeys = new();

    [SetsRequiredMembers]
    public MXF(string inputFile)
    {
        InputFile = new FileInfo(inputFile);

        if (!InputFile.Exists)
        {
            throw new FileNotFoundException("The specified MXF file does not exist.", inputFile);
        }

        Input = InputFile.OpenRead();

        if (!IsValidMXFFile())
        {
            throw new InvalidDataException("The specified file is not a valid MXF file.");
        }

        if (!GetStartTimecode())
        {
            throw new InvalidOperationException("Failed to retrieve start timecode from the MXF file.");
        }

        Input.Seek(0, SeekOrigin.Begin); // Reset stream position to the beginning
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        CloseExtractionStreams();
        Input?.Close();
        Input?.Dispose();
        _outputStream?.Dispose();
    }

    /// <summary>
    /// Checks if the file is a valid MXF file by reading the first 4 bytes.
    /// </summary>
    /// <returns>True if the file is a valid MXF file, otherwise false.</returns>
    private bool IsValidMXFFile()
    {
        // Use existing Input stream instead of creating new one
        var currentPosition = Input.Position;
        Input.Seek(0, SeekOrigin.Begin);
        
        Span<byte> header = stackalloc byte[4];
        var bytesRead = Input.Read(header);
        
        Input.Seek(currentPosition, SeekOrigin.Begin); // Restore position
        
        return bytesRead == header.Length && header.SequenceEqual(Keys.FourCc);
    }

    public void AddRequiredKey(KeyType keyType)
    {
        if (!RequiredKeys.Contains(keyType))
        {
            RequiredKeys.Add(keyType);
        }
    }

    public void AddRequiredKey(string keyType)
    {
        if (Enum.TryParse<KeyType>(keyType, true, out var parsedKeyType))
        {
            AddRequiredKey(parsedKeyType);
        }
        else
        {
            throw new ArgumentException($"Invalid key type: {keyType}", nameof(keyType));
        }
    }

    public void ClearRequiredKeys()
    {
        RequiredKeys.Clear();
    }

    public void RemoveRequiredKey(KeyType keyType)
    {
        RequiredKeys.Remove(keyType);
    }

    public void RemoveRequiredKey(string keyType)
    {
        if (Enum.TryParse<KeyType>(keyType, true, out var parsedKeyType))
        {
            RemoveRequiredKey(parsedKeyType);
        }
        else
        {
            throw new ArgumentException($"Invalid key type: {keyType}", nameof(keyType));
        }
    }

    /// <summary>
    /// Retrieves the start timecode from the MXF file.
    /// </summary>
    /// <returns>True if the start timecode was successfully retrieved, otherwise false.</returns>
    public bool GetStartTimecode()
    {
        Input.Seek(0, SeekOrigin.Begin);

        while (Input.Position < 128000)
        {
            var keyBytesRead = Input.Read(_keyBuffer, 0, Constants.KLV_KEY_SIZE);
            if (keyBytesRead != Constants.KLV_KEY_SIZE) break;

            var keyType = Keys.GetKeyType(_keyBuffer.AsSpan(0, Constants.KLV_KEY_SIZE));

            var length = ReadBerLength(Input);
            if (length < 0) break;

            if (keyType == KeyType.TimecodeComponent)
            {
                var data = new byte[length];
                var dataBytesRead = Input.Read(data, 0, length);
                if (dataBytesRead != length) break;

                var timecodeComponent = TimecodeComponent.Parse(data);
                StartTimecode = new Timecode(timecodeComponent.StartTimecode, timecodeComponent.RoundedTimecodeTimebase, timecodeComponent.DropFrame);
                Input.Seek(0, SeekOrigin.Begin);
                return true;
            }
            else
            {
                Input.Seek(length, SeekOrigin.Current);
            }
        }

        Input.Seek(0, SeekOrigin.Begin);
        StartTimecode = new Timecode(0);
        return false;
    }

    // General Parse method which parses all types of streams in the MXF file
    public void Parse()
    {
        Input.Seek(0, SeekOrigin.Begin);
        _lastTimecode = null; // Reset sequential checking

        try
        {
            while (true)
            {
                var keyBytesRead = Input.Read(_keyBuffer, 0, Constants.KLV_KEY_SIZE);
                if (keyBytesRead != Constants.KLV_KEY_SIZE) break;

                var keyType = Keys.GetKeyType(_keyBuffer.AsSpan(0, Constants.KLV_KEY_SIZE));
                var length = ReadBerLength(Input, _berLengthBuffer);
                if (length < 0) break;

                // Determine if we should extract this key
                bool shouldExtract = Extract && (DemuxMode || RequiredKeys.Contains(keyType));
                
                if (shouldExtract)
                {
                    ExtractPacket(keyType, length);
                }
                else
                {
                    switch (keyType)
                    {
                        case KeyType.System:
                            if (RequiredKeys.Contains(KeyType.System))
                            {
                                ProcessSystemPacket(length);
                            }
                            else
                            {
                                Input.Seek(length, SeekOrigin.Current);
                            }
                            break;

                        case KeyType.Data:
                            if (RequiredKeys.Contains(KeyType.Data))
                                ProcessDataPacket(length);
                            else
                                Input.Seek(length, SeekOrigin.Current);
                            break;
                            
                        case KeyType.Video:
                        case KeyType.Audio:
                        case KeyType.TimecodeComponent:
                            if (RequiredKeys.Contains(keyType))
                            {
                                // For now, just skip these packets
                                Input.Seek(length, SeekOrigin.Current);
                            }
                            else
                            {
                                Input.Seek(length, SeekOrigin.Current);
                            }
                            break;
                        default:
                            Input.Seek(length, SeekOrigin.Current);
                            break;
                    }
                }
            }
        }
        finally
        {
            // Close any open extraction streams
            CloseExtractionStreams();
            Input.Seek(0, SeekOrigin.Begin);
        }
    }

    private void ProcessSystemPacket(int length)
    {
        int offset = -1;
        if (length >= Constants.SYSTEM_METADATA_PACK_GC + Constants.SMPTE_TIMECODE_SIZE)
        {
            offset = Constants.SYSTEM_METADATA_PACK_GC;
        }
        else if (length >= Constants.SYSTEM_METADATA_SET_GC_OFFSET + Constants.SMPTE_TIMECODE_SIZE)
        {
            offset = Constants.SYSTEM_METADATA_SET_GC_OFFSET;
        }
        if (offset < 0)
        {
            Input.Seek(length, SeekOrigin.Current);
            return;
        }
        Input.Seek(offset, SeekOrigin.Current);

        var smpteRead = Input.Read(_smpteBuffer, 0, Constants.SMPTE_TIMECODE_SIZE);
        if (smpteRead == Constants.SMPTE_TIMECODE_SIZE)
        {
            var smpte = Timecode.FromBytes(_smpteBuffer, StartTimecode.Timebase, StartTimecode.DropFrame);

            if (CheckSequential && _lastTimecode != null)
            {
                var expectedPrevious = smpte.GetPrevious();
                if (_lastTimecode != expectedPrevious)
                {
                    throw new InvalidDataException($"SMPTE timecodes are not sequential: {_lastTimecode} != {expectedPrevious}");
                }
            }

            SMPTETimecodes.Add(smpte);
            _lastTimecode = smpte;
        }

        var remainingBytes = length - offset - Constants.SMPTE_TIMECODE_SIZE;
        if (remainingBytes > 0)
        {
            Input.Seek(remainingBytes, SeekOrigin.Current);
        }
        else if (remainingBytes < 0)
        {
            throw new InvalidDataException("Invalid length for System Metadata Pack or Set.");
        }
    }

    private void ProcessDataPacket(int length)
    {
        Span<byte> header = stackalloc byte[Constants.PACKET_HEADER_SIZE];
        if (Input.Read(header) != Constants.PACKET_HEADER_SIZE)
            throw new InvalidOperationException("Failed to read packet header.");

        var data = new byte[length - Constants.PACKET_HEADER_SIZE];
        if (Input.Read(data, 0, data.Length) != data.Length)
            throw new InvalidOperationException("Failed to read data for Data key.");

        var packet = new Packet(header.ToArray());
        var lines = Packet.ParseLines(data);
        packet.Lines.AddRange(lines);
        Packets.Add(packet);
    }

    public bool ParseSMPTETimecodes()
    {
        Input.Seek(0, SeekOrigin.Begin);
        _lastTimecode = null;

        while (true)
        {
            var keyBytesRead = Input.Read(_keyBuffer, 0, Constants.KLV_KEY_SIZE);
            if (keyBytesRead != Constants.KLV_KEY_SIZE) break;

            var keyType = Keys.GetKeyType(_keyBuffer.AsSpan(0, Constants.KLV_KEY_SIZE));
            var length = ReadBerLength(Input);
            if (length < 0) break;

            if (keyType == KeyType.System)
            {
                ProcessSystemPacket(length);
            }
            else
            {
                Input.Seek(length, SeekOrigin.Current);
            }
        }

        Input.Seek(0, SeekOrigin.Begin);
        return SMPTETimecodes.Count > 0;
    }

    private static int ReadBerLength(Stream input, List<byte>? lengthBuffer = null)
    {
        lengthBuffer?.Clear();
        var firstByte = input.ReadByte();
        if (firstByte == -1) return -1;
        
        lengthBuffer?.Add((byte)firstByte);

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
                lengthBuffer?.Add((byte)b);
                length = (length << 8) | b;
            }

            return length;
        }
    }
    
    private void ExtractPacket(KeyType keyType, int length)
    {
        FileStream outputStream;
        
        if (DemuxMode)
        {
            // Demux mode: create unique file for each key
            var keyIdentifier = UseKeyNames ? GetKeyName(_keyBuffer) : BytesToHexString(_keyBuffer);
            
            if (!_foundKeys.Contains(keyIdentifier))
            {
                Console.WriteLine($"Found key: {keyIdentifier}, length: {length}");
                _foundKeys.Add(keyIdentifier);
            }
            
            if (!_demuxStreams.TryGetValue(keyIdentifier, out outputStream!))
            {
                var fileExtension = KlvMode ? ".klv" : ".raw";
                var outputPath = $"{OutputBasePath ?? Path.ChangeExtension(InputFile.FullName, null)}_{keyIdentifier}{fileExtension}";
                outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                _demuxStreams[keyIdentifier] = outputStream;
            }
        }
        else
        {
            // Normal extraction mode
            if (!_outputStreams.TryGetValue(keyType, out outputStream!))
            {
                if (_keyTypeToExtension.TryGetValue(keyType, out var extension))
                {
                    if (KlvMode)
                    {
                        extension = extension.Replace(".raw", ".klv");
                    }
                    var outputPath = (OutputBasePath ?? Path.ChangeExtension(InputFile.FullName, null)) + extension;
                    outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                    _outputStreams[keyType] = outputStream;
                }
                else
                {
                    // Skip if no extension mapping
                    Input.Seek(length, SeekOrigin.Current);
                    return;
                }
            }
        }
        
        // Write KLV header if requested
        if (KlvMode)
        {
            outputStream.Write(_keyBuffer, 0, Constants.KLV_KEY_SIZE);
            outputStream.Write([.. _berLengthBuffer], 0, _berLengthBuffer.Count);
        }
        
        // Write essence data
        var essenceData = new byte[length];
        var bytesRead = Input.Read(essenceData, 0, length);
        if (bytesRead != length) throw new EndOfStreamException("Unexpected end of stream while reading value.");
        outputStream.Write(essenceData, 0, length);
    }
    
    private void CloseExtractionStreams()
    {
        foreach (var stream in _outputStreams.Values)
        {
            stream?.Dispose();
        }
        _outputStreams.Clear();
        
        foreach (var stream in _demuxStreams.Values)
        {
            stream?.Dispose();
        }
        _demuxStreams.Clear();
    }
    
    private static string BytesToHexString(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
    
    private string GetKeyName(byte[] keyBytes)
    {
        Type[] typesToSearch = { typeof(Essence), typeof(Keys) };
        
        foreach (var type in typesToSearch)
        {
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            
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
    
    /// <summary>
    /// Extract essence elements from the MXF file based on the configured extraction settings.
    /// </summary>
    /// <param name="outputBasePath">Optional output base path for extracted files</param>
    /// <param name="demuxMode">Extract all keys found to separate files</param>
    /// <param name="useKeyNames">Use Key/Essence names instead of hex keys</param>
    /// <param name="klvMode">Include key and length bytes in output files</param>
    public void ExtractEssence(string? outputBasePath = null, bool? demuxMode = null, bool? useKeyNames = null, bool? klvMode = null)
    {
        // Apply extraction settings
        OutputBasePath = outputBasePath ?? OutputBasePath;
        DemuxMode = demuxMode ?? DemuxMode;
        UseKeyNames = useKeyNames ?? UseKeyNames;
        KlvMode = klvMode ?? KlvMode;
        Extract = true;
        
        // If no specific keys are required and not in demux mode, default to extracting all
        if (!DemuxMode && RequiredKeys.Count == 0)
        {
            RequiredKeys.Add(KeyType.Data);
            RequiredKeys.Add(KeyType.Video);
            RequiredKeys.Add(KeyType.Audio);
            RequiredKeys.Add(KeyType.System);
            RequiredKeys.Add(KeyType.TimecodeComponent);
        }
        
        // Run the parse method which will handle extraction
        Parse();
    }
}
