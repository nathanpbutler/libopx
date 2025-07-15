using System;
using System.Diagnostics.CodeAnalysis;

namespace nathanbutlerDEV.libopx.Formats;

public class BIN : IDisposable
{
    public FileInfo? InputFile { get; set; } = null; // If null, read from stdin
    public FileInfo? OutputFile { get; set; } = null; // If null, write to stdout
    private Stream? _outputStream;
    
    public required Stream Input { get; set; }
    public Stream Output => _outputStream ??= OutputFile == null ? Console.OpenStandardOutput() : OutputFile.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
    public List<Packet> Packets { get; set; } = []; // List of packets in the BIN file

    /// <summary>
    /// Constructor for BIN format from file
    /// </summary>
    /// <param name="inputFile">Path to the input BIN file</param>
    /// <exception cref="FileNotFoundException">Thrown if the specified file does not exist</exception>
    [SetsRequiredMembers]
    public BIN(string inputFile)
    {
        InputFile = new FileInfo(inputFile);

        if (!InputFile.Exists)
        {
            throw new FileNotFoundException("The specified BIN file does not exist.", inputFile);
        }

        Input = InputFile.OpenRead();
        Input.Seek(0, SeekOrigin.Begin); // Reset stream position to the beginning
    }

    /// <summary>
    /// Constructor for BIN format from stdin
    /// </summary>
    [SetsRequiredMembers]
    public BIN()
    {
        InputFile = null;
        Input = Console.OpenStandardInput();
    }

    /// <summary>
    /// Constructor for BIN format with custom stream
    /// </summary>
    /// <param name="inputStream">The input stream to read from</param>
    [SetsRequiredMembers]
    public BIN(Stream inputStream)
    {
        InputFile = null;
        Input = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
    }

    /// <summary>
    /// Sets the output file for writing
    /// </summary>
    /// <param name="outputFile">Path to the output file</param>
    public void SetOutput(string outputFile)
    {
        OutputFile = new FileInfo(outputFile);
    }

    public void Parse()
    {
        Input.Seek(0, SeekOrigin.Begin);

        // Reuse buffers to reduce allocations
        var packetHeader = new byte[Packet.HeaderSize];
        var lineHeader = new byte[Line.HeaderSize];

        while (Input.Read(packetHeader, 0, Packet.HeaderSize) == Packet.HeaderSize)
        {
            var packet = new Packet(packetHeader);
            
            for (var l = 0; l < packet.LineCount; l++)
            {
                if (Input.Read(lineHeader, 0, Line.HeaderSize) < Line.HeaderSize) break;
                var line = new Line(lineHeader);
                
                if (line.Length <= 0)
                {
                    throw new InvalidDataException("Line length is invalid.");
                }
                
                // Use the more efficient ParseLine method
                line.ParseLine(Input);
                packet.Lines.Add(line);
            }
            Packets.Add(packet);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        // Only dispose streams we created (not stdin/stdout)
        if (InputFile != null)
        {
            Input?.Dispose();
        }
        if (OutputFile != null)
        {
            _outputStream?.Dispose();
        }
    }
}
