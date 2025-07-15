using System.Diagnostics.CodeAnalysis;

namespace nathanbutlerDEV.libopx.Formats;

public class MXF : IDisposable
{
    private const int KeySize = 16;
    private const int SystemMetadataPackGC = 41;
    private const int SystemMetadataSetGCOffset = 12;
    private const int SMPTETimecodeSize = 4;
    
    // Reusable buffers to reduce allocations
    private readonly byte[] _keyBuffer = new byte[KeySize];
    private readonly byte[] _smpteBuffer = new byte[SMPTETimecodeSize];

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
    
    // Cache the previous timecode for sequential checking to avoid recalculation
    private Timecode? _lastTimecode;

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
        Input?.Close();
        Input?.Dispose();
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
            var keyBytesRead = Input.Read(_keyBuffer, 0, KeySize);
            if (keyBytesRead != KeySize) break;

            var keyType = Keys.GetKeyType(_keyBuffer.AsSpan(0, KeySize));

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

        while (true)
        {
            var keyBytesRead = Input.Read(_keyBuffer, 0, KeySize);
            if (keyBytesRead != KeySize) break;

            var keyType = Keys.GetKeyType(_keyBuffer.AsSpan(0, KeySize));
            var length = ReadBerLength(Input);
            if (length < 0) break;

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
                    if (RequiredKeys.Contains(KeyType.Video))
                    {
                        // Handle video packets if needed
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
        Input.Seek(0, SeekOrigin.Begin);
    }

    private void ProcessSystemPacket(int length)
    {
        int offset = -1;
        if (length >= SystemMetadataPackGC + SMPTETimecodeSize)
        {
            offset = SystemMetadataPackGC;
        }
        else if (length >= SystemMetadataSetGCOffset + SMPTETimecodeSize)
        {
            offset = SystemMetadataSetGCOffset;
        }
        if (offset < 0)
        {
            Input.Seek(length, SeekOrigin.Current);
            return;
        }
        Input.Seek(offset, SeekOrigin.Current);

        var smpteRead = Input.Read(_smpteBuffer, 0, SMPTETimecodeSize);
        if (smpteRead == SMPTETimecodeSize)
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

        var remainingBytes = length - offset - SMPTETimecodeSize;
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
        Span<byte> header = stackalloc byte[Packet.HeaderSize];
        if (Input.Read(header) != Packet.HeaderSize) 
            throw new InvalidOperationException("Failed to read packet header.");
            
        var data = new byte[length - Packet.HeaderSize];
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
            var keyBytesRead = Input.Read(_keyBuffer, 0, KeySize);
            if (keyBytesRead != KeySize) break;

            var keyType = Keys.GetKeyType(_keyBuffer.AsSpan(0, KeySize));
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
