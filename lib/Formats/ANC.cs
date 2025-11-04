using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Handlers;

namespace nathanbutlerDEV.libopx.Formats;

/// <summary>
/// Handles parsing of MXF Ancillary Data (ANC) extracted to binary files.
/// Formerly known as MXFData. Renamed to ANC in v2.2.0 for clarity.
/// Typically processes .bin files extracted from MXF containers.
/// </summary>
public class ANC : FormatIOBase
{
    /// <summary>
    /// Internal handler for ANC format parsing operations.
    /// </summary>
    private static readonly ANCHandler _handler = new();

    private readonly byte[] _packetHeader = new byte[Constants.PACKET_HEADER_SIZE];
    private readonly byte[] _lineHeader = new byte[Constants.LINE_HEADER_SIZE];

    // TODO: Change Parse() to output Packets instead of storing them in the ANC object
    /// <summary>
    /// Gets or sets the list of packets in the extracted MXF data. Use Parse() method instead.
    /// </summary>
    [Obsolete("Use Parse() method which returns IEnumerable<Packet> instead")]
    public List<Packet> Packets { get; set; } = [];

    /// <summary>
    /// Constructor for extracted MXF ancillary data from file
    /// </summary>
    /// <param name="inputFile">Path to the input file (typically *.bin)</param>
    /// <exception cref="FileNotFoundException">Thrown if the specified file does not exist</exception>
    [SetsRequiredMembers]
    public ANC(string inputFile)
    {
        InputFile = new FileInfo(inputFile);

        if (!InputFile.Exists)
        {
            throw new FileNotFoundException("The specified file does not exist.", inputFile);
        }

        Input = InputFile.OpenRead();
        Input.Seek(0, SeekOrigin.Begin); // Reset stream position to the beginning
        OutputFormat = Format.T42; // Set default output format
    }

    /// <summary>
    /// Constructor for extracted MXF ancillary data from stdin
    /// </summary>
    [SetsRequiredMembers]
    public ANC()
    {
        InputFile = null;
        Input = Console.OpenStandardInput();
        OutputFormat = Format.T42; // Set default output format
    }

    /// <summary>
    /// Constructor for extracted MXF ancillary data with custom stream
    /// </summary>
    /// <param name="inputStream">The input stream to read from</param>
    /// <exception cref="ArgumentNullException">Thrown if inputStream is null</exception>
    [SetsRequiredMembers]
    public ANC(Stream inputStream)
    {
        InputFile = null;
        Input = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
        OutputFormat = Format.T42; // Set default output format
    }

    /// <summary>
    /// Parses the extracted MXF ancillary data and returns an enumerable of packets with optional filtering.
    /// </summary>
    /// <param name="magazine">Optional magazine number filter (default: all magazines)</param>
    /// <param name="rows">Optional array of row numbers to filter (default: all rows)</param>
    /// <param name="startTimecode">Optional starting timecode for packet numbering</param>
    /// <returns>An enumerable of parsed packets matching the filter criteria</returns>
    public IEnumerable<Packet> Parse(int? magazine = null, int[]? rows = null, Timecode? startTimecode = null)
    {
        // Create options from parameters
        var options = new ParseOptions
        {
            Magazine = magazine,
            Rows = rows,
            OutputFormat = OutputFormat ?? Format.T42,
            StartTimecode = startTimecode
        };

        // Delegate to handler
        return _handler.Parse(Input, options);
    }

    /// <summary>
    /// [Obsolete] Original Parse implementation - kept for reference.
    /// </summary>
    private IEnumerable<Packet> ParseOriginal(int? magazine = null, int[]? rows = null, Timecode? startTimecode = null)
    {
        // Use default rows if not specified
        rows ??= Constants.DEFAULT_ROWS;

        // If OutputFormat is not set, use the provided outputFormat
        var outputFormat = OutputFormat ?? Format.T42;

        var lineNumber = 0;

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
                    LineNumber = lineNumber, // Increment line number for each line
                    LineTimecode = timecode  // Propagate packet timecode to line for RCWT
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

    /// <summary>
    /// Asynchronously parses the extracted MXF ancillary data and returns an async enumerable of packets with optional filtering.
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
        // Create options from parameters
        var options = new ParseOptions
        {
            Magazine = magazine,
            Rows = rows,
            OutputFormat = OutputFormat ?? Format.T42,
            StartTimecode = startTimecode
        };

        // Delegate to handler
        await foreach (var packet in _handler.ParseAsync(Input, options, cancellationToken))
        {
            yield return packet;
        }
    }

    /// <summary>
    /// [Obsolete] Original ParseAsync implementation - kept for reference.
    /// </summary>
    private async IAsyncEnumerable<Packet> ParseAsyncOriginal(
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
                        LineNumber = lineNumber,
                        LineTimecode = timecode
                    };

                    if (line.Length <= 0)
                        throw new InvalidDataException("Line length is invalid.");

                    // Parse line data asynchronously
                    await ParseLineAsync(line, Input, outputFormat, cancellationToken);

                    // Apply filtering
                    if (magazine.HasValue && line.Magazine != magazine.Value)
                    {
                        lineNumber++;
                        continue;
                    }

                    if (rows != null && !rows.Contains(line.Row))
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
}
