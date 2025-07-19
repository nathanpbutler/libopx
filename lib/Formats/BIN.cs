using System;
using System.Diagnostics.CodeAnalysis;
using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.libopx.Formats;

public class BIN : IDisposable
{
    public FileInfo? InputFile { get; set; } = null; // If null, read from stdin
    public FileInfo? OutputFile { get; set; } = null; // If null, write to stdout
    private Stream? _outputStream;
    public required Stream Input { get; set; }
    public Stream Output => _outputStream ??= OutputFile == null ? Console.OpenStandardOutput() : OutputFile.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
    // TODO: Change Parse() to output Packets instead of storing them in the BIN object
    public List<Packet> Packets { get; set; } = []; // List of packets in the BIN file
    // TODO: Implement Extract and Filter functions
    public Function Function { get; set; } = Function.Extract; // Default function is Extract

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
        // Console.WriteLine("Parsing BIN file...");
        int lineNumber = 0;

        Input.Seek(0, SeekOrigin.Begin);

        var timecode = new Timecode(0); // Default timecode, can be modified later

        // Reuse buffers to reduce allocations
        var packetHeader = new byte[Constants.PACKET_HEADER_SIZE];
        var lineHeader = new byte[Constants.LINE_HEADER_SIZE];

        while (Input.Read(packetHeader, 0, Constants.PACKET_HEADER_SIZE) == Constants.PACKET_HEADER_SIZE)
        {
            var packet = new Packet(packetHeader)
            {
                Timecode = timecode // Set the timecode for the packet
            };

            for (var l = 0; l < packet.LineCount; l++)
            {
                if (Input.Read(lineHeader, 0, Constants.LINE_HEADER_SIZE) < Constants.LINE_HEADER_SIZE) break;
                var line = new Line(lineHeader)
                {
                    LineNumber = lineNumber // Increment line number for each line
                };

                if (line.Length <= 0)
                {
                    throw new InvalidDataException("Line length is invalid.");
                }

                // Use the more efficient ParseLine method
                line.ParseLine(Input);
                packet.Lines.Add(line);
                lineNumber++; // Increment line number for each line processed
            }
            Packets.Add(packet);
            timecode = timecode.GetNext(); // Increment timecode for the next packet
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
