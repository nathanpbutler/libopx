using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.libopx.Formats;

/// <summary>
/// Parser for BIN format files containing teletext data packets with line headers.
/// Supports streaming parsing with magazine and row filtering capabilities.
/// </summary>
public class BIN : IDisposable
{
    private readonly byte[] _packetHeader = new byte[Constants.PACKET_HEADER_SIZE];
    private readonly byte[] _lineHeader = new byte[Constants.LINE_HEADER_SIZE];
    /// <summary>
    /// Gets or sets the input file. If null, reads from stdin.
    /// </summary>
    public FileInfo? InputFile { get; set; } = null;
    /// <summary>
    /// Gets or sets the output file. If null, writes to stdout.
    /// </summary>
    public FileInfo? OutputFile { get; set; } = null;
    private Stream? _outputStream;
    /// <summary>
    /// Gets or sets the input stream for reading BIN data.
    /// </summary>
    public required Stream Input { get; set; }
    /// <summary>
    /// Gets the output stream for writing processed data.
    /// </summary>
    public Stream Output => _outputStream ??= OutputFile == null ? Console.OpenStandardOutput() : OutputFile.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
    // TODO: Change Parse() to output Packets instead of storing them in the BIN object
    /// <summary>
    /// Gets or sets the list of packets in the BIN file. Use Parse() method instead.
    /// </summary>
    [Obsolete("Use Parse() method which returns IEnumerable<Packet> instead")]
    public List<Packet> Packets { get; set; } = [];
    /// <summary>
    /// Gets or sets the output format for processed data. Default is T42.
    /// </summary>
    public Format? OutputFormat { get; set; } = Format.T42;
    // TODO: Implement Extract and Filter functions
    /// <summary>
    /// Gets or sets the function mode for processing. Default is Filter.
    /// </summary>
    public Function Function { get; set; } = Function.Filter;

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
    
    /// <summary>
    /// Sets the output stream for writing
    /// </summary>
    /// <param name="outputStream">The output stream to write to</param>
    public void SetOutput(Stream outputStream)
    {
        OutputFile = null; // Clear OutputFile since we're using a custom stream
        _outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream), "Output stream cannot be null.");
    }

    /// <summary>
    /// Parses the BIN file and returns an enumerable of packets with optional filtering.
    /// </summary>
    /// <param name="magazine">Optional magazine number filter (default: all magazines)</param>
    /// <param name="rows">Optional array of row numbers to filter (default: all rows)</param>
    /// <param name="startTimecode">Optional starting timecode for packet numbering</param>
    /// <returns>An enumerable of parsed packets matching the filter criteria</returns>
    public IEnumerable<Packet> Parse(int? magazine = null, int[]? rows = null, Timecode? startTimecode = null)
    {
        // Use default rows if not specified
        rows ??= Constants.DEFAULT_ROWS;

        // If OutputFormat is not set, use the provided outputFormat
        var outputFormat = OutputFormat ?? Format.T42;

        int lineNumber = 0;

        var timecode = startTimecode ?? new Timecode(0); // Default timecode, can be modified later

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
                if (magazine.HasValue && line.Magazine != magazine.Value && outputFormat == Format.T42)
                {
                    lineNumber++;
                    continue; // Skip lines that don't match the magazine filter
                }

                if (rows != null && !rows.Contains(line.Row) && outputFormat == Format.T42)
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

    /// <summary>
    /// Asynchronously parses the BIN file and returns an async enumerable of packets with optional filtering.
    /// </summary>
    /// <param name="magazine">Optional magazine number filter (default: all magazines)</param>
    /// <param name="rows">Optional array of row numbers to filter (default: all rows)</param>
    /// <param name="startTimecode">Optional starting timecode for packet numbering</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>An async enumerable of parsed packets matching the filter criteria</returns>
    public async IAsyncEnumerable<Packet> ParseAsync(
        int? magazine = null, 
        int[]? rows = null, 
        Timecode? startTimecode = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        rows ??= Constants.DEFAULT_ROWS;
        var outputFormat = OutputFormat ?? Format.T42;
        int lineNumber = 0;
        var timecode = startTimecode ?? new Timecode(0);

        var arrayPool = ArrayPool<byte>.Shared;
        var packetBuffer = arrayPool.Rent(Constants.PACKET_HEADER_SIZE);
        var lineBuffer = arrayPool.Rent(Constants.LINE_HEADER_SIZE);

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Read packet header asynchronously
                var packetMemory = packetBuffer.AsMemory(0, Constants.PACKET_HEADER_SIZE);
                var packetBytesRead = await Input.ReadAsync(packetMemory, cancellationToken);
                
                if (packetBytesRead != Constants.PACKET_HEADER_SIZE)
                    break;

                var packet = new Packet(packetBuffer.AsSpan(0, Constants.PACKET_HEADER_SIZE).ToArray())
                {
                    Timecode = timecode
                };

                for (var l = 0; l < packet.LineCount; l++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var lineMemory = lineBuffer.AsMemory(0, Constants.LINE_HEADER_SIZE);
                    var lineBytesRead = await Input.ReadAsync(lineMemory, cancellationToken);
                    
                    if (lineBytesRead < Constants.LINE_HEADER_SIZE) break;

                    var line = new Line(lineBuffer.AsSpan(0, Constants.LINE_HEADER_SIZE))
                    {
                        LineNumber = lineNumber
                    };

                    if (line.Length <= 0)
                        throw new InvalidDataException("Line length is invalid.");

                    // Parse line data asynchronously
                    await ParseLineAsync(line, Input, outputFormat, cancellationToken);

                    // Apply filtering
                    if (magazine.HasValue && line.Magazine != magazine.Value && outputFormat == Format.T42)
                    {
                        lineNumber++;
                        continue;
                    }

                    if (rows != null && !rows.Contains(line.Row) && outputFormat == Format.T42)
                    {
                        lineNumber++;
                        continue;
                    }

                    packet.Lines.Add(line);
                    lineNumber++;
                }

                if (packet.Lines.Count > 0)
                {
                    yield return packet;
                }
                
                timecode = timecode.GetNext();
            }
        }
        finally
        {
            arrayPool.Return(packetBuffer);
            arrayPool.Return(lineBuffer);
        }
    }

    /// <summary>
    /// Asynchronously parses line data from a stream
    /// </summary>
    private static async Task ParseLineAsync(Line line, Stream input, Format outputFormat, CancellationToken cancellationToken)
    {
        if (line.Length <= 0)
            throw new InvalidDataException("Line length is invalid.");

        var arrayPool = ArrayPool<byte>.Shared;
        var dataBuffer = arrayPool.Rent(line.Length);
        
        try
        {
            var dataMemory = dataBuffer.AsMemory(0, line.Length);
            var bytesRead = await input.ReadAsync(dataMemory, cancellationToken);
            
            if (bytesRead < line.Length)
                throw new InvalidDataException($"Not enough data to read the line. Expected {line.Length}, got {bytesRead}.");

            // Use the existing ParseLine logic with the read data
            line.ParseLine(dataBuffer.AsSpan(0, line.Length).ToArray(), outputFormat);
        }
        finally
        {
            arrayPool.Return(dataBuffer);
        }
    }

    /// <summary>
    /// Releases all resources used by the BIN parser.
    /// </summary>
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
