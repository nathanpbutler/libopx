using System;
using System.Diagnostics.CodeAnalysis;
using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.libopx.Formats;

public class BIN : IDisposable
{
    private readonly byte[] _packetHeader = new byte[Constants.PACKET_HEADER_SIZE];
    private readonly byte[] _lineHeader = new byte[Constants.LINE_HEADER_SIZE];
    public FileInfo? InputFile { get; set; } = null; // If null, read from stdin
    public FileInfo? OutputFile { get; set; } = null; // If null, write to stdout
    private Stream? _outputStream;
    public required Stream Input { get; set; }
    public Stream Output => _outputStream ??= OutputFile == null ? Console.OpenStandardOutput() : OutputFile.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
    // TODO: Change Parse() to output Packets instead of storing them in the BIN object
    [Obsolete("Use Parse() method which returns IEnumerable<Packet> instead")]
    public List<Packet> Packets { get; set; } = []; // List of packets in the BIN file
    public LineFormat? OutputFormat { get; set; } = LineFormat.T42; // Default output format
    // TODO: Implement Extract and Filter functions
    public Function Function { get; set; } = Function.Filter; // Default function is Filter (outputting to console)

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

    public IEnumerable<Packet> Parse(int? magazine = 8, int[]? rows = null)
    {
        // Use default rows if not specified
        rows ??= Constants.DEFAULT_ROWS;

        // If OutputFormat is not set, use the provided outputFormat
        var outputFormat = OutputFormat ?? LineFormat.T42;
        
        int lineNumber = 0;

        var timecode = new Timecode(0); // Default timecode, can be modified later

        while (Input.Read(_packetHeader, 0, Constants.PACKET_HEADER_SIZE) == Constants.PACKET_HEADER_SIZE)
        {
            var packet = new Packet(_packetHeader)
            {
                Timecode = timecode // Set the timecode for the packet
            };

            for (var l = 0; l < packet.LineCount; l++)
            {
                if (Input.Read(_lineHeader, 0, Constants.LINE_HEADER_SIZE) < Constants.LINE_HEADER_SIZE) break;
                var line = new Line(_lineHeader)
                {
                    LineNumber = lineNumber // Increment line number for each line
                };

                if (line.Length <= 0)
                {
                    throw new InvalidDataException("Line length is invalid.");
                }

                // Use the more efficient ParseLine method
                line.ParseLine(Input, outputFormat);

                // Apply filtering if specified
                if (magazine.HasValue && line.Magazine != magazine.Value)
                {
                    lineNumber++;
                    continue; // Skip lines that don't match the magazine filter
                }
                
                if (rows != null && !rows.Contains(line.Row))
                {
                    lineNumber++;
                    continue; // Skip lines that don't match the row filter
                }
                
                packet.Lines.Add(line);
                lineNumber++; // Increment line number for each line processed
            }
            // Only yield packets that have at least one line after filtering
            if (packet.Lines.Count > 0)
            {
                yield return packet;
            }
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
