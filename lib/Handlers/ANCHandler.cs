using System.Buffers;
using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;

namespace nathanbutlerDEV.libopx.Handlers;

/// <summary>
/// Handler for parsing MXF Ancillary Data (ANC) format files.
/// Processes binary data extracted from MXF containers.
/// </summary>
public class ANCHandler : IPacketFormatHandler
{
    private readonly byte[] _packetHeader = new byte[Constants.PACKET_HEADER_SIZE];
    private readonly byte[] _lineHeader = new byte[Constants.LINE_HEADER_SIZE];

    /// <inheritdoc />
    public Format InputFormat => Format.ANC;

    /// <inheritdoc />
    public Format[] ValidOutputs => [Format.T42, Format.VBI, Format.VBI_DOUBLE, Format.RCWT, Format.STL];

    /// <summary>
    /// Creates a new instance of ANCHandler.
    /// </summary>
    public ANCHandler()
    {
    }

    /// <inheritdoc />
    public IEnumerable<Packet> Parse(Stream inputStream, ParseOptions options)
    {
        ArgumentNullException.ThrowIfNull(inputStream);
        ArgumentNullException.ThrowIfNull(options);

        // Use default rows if not specified
        var rows = options.Rows ?? Constants.DEFAULT_ROWS;
        // ANC is a container format; default to T42 for line parsing since ANC data contains T42
        var outputFormat = options.OutputFormat == Format.ANC ? Format.T42 : options.OutputFormat;
        var lineNumber = 0;
        var timecode = options.StartTimecode ?? new Timecode(0);
        string? currentPageNumber = null; // Track current page for filtering

        while (inputStream.Read(_packetHeader, 0, Constants.PACKET_HEADER_SIZE) == Constants.PACKET_HEADER_SIZE)
        {
            var packet = new Packet(_packetHeader)
            {
                Timecode = timecode
            };

            for (var l = 0; l < packet.LineCount; l++)
            {
                if (inputStream.Read(_lineHeader, 0, Constants.LINE_HEADER_SIZE) < Constants.LINE_HEADER_SIZE) break;
                var line = new Line(_lineHeader)
                {
                    LineNumber = lineNumber,
                    LineTimecode = timecode
                };

                if (line.Length <= 0)
                {
                    throw new InvalidDataException("Line length is invalid.");
                }

                // Use the more efficient ParseLine method
                line.ParseLine(inputStream, outputFormat);

                // Track current page for filtering
                if (line.Row == 0 && line.Data.Length >= Constants.T42_LINE_SIZE)
                {
                    var pageNumber = T42.GetPageNumber(line.Data);
                    if (pageNumber != null)
                    {
                        currentPageNumber = pageNumber;
                    }
                }

                // Apply filtering if specified
                // Magazine filtering
                if (options.Magazine.HasValue && line.Magazine != options.Magazine.Value)
                {
                    lineNumber++;
                    continue;
                }

                // Page number filtering
                if (!string.IsNullOrEmpty(options.PageNumber))
                {
                    if (currentPageNumber == null || currentPageNumber != options.PageNumber)
                    {
                        lineNumber++;
                        continue;
                    }
                }

                // Row filtering
                if (rows != null && !rows.Contains(line.Row))
                {
                    lineNumber++;
                    continue;
                }

                // Caption content filtering - skip rows with only spaces/control codes
                if (options.UseCaps && line.Row > 0 && line.Data.Length >= Constants.T42_LINE_SIZE && !T42.HasMeaningfulContent(line.Data))
                {
                    lineNumber++;
                    continue;
                }

                packet.Lines.Add(line);
                lineNumber++;
            }

            // Only yield packets that have at least one line after filtering
            if (packet.Lines.Count > 0)
            {
                yield return packet;
            }

            timecode = timecode.GetNext();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Packet> ParseAsync(
        Stream inputStream,
        ParseOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputStream);
        ArgumentNullException.ThrowIfNull(options);

        var rows = options.Rows ?? Constants.DEFAULT_ROWS;
        // ANC is a container format; default to T42 for line parsing since ANC data contains T42
        var outputFormat = options.OutputFormat == Format.ANC ? Format.T42 : options.OutputFormat;
        int lineNumber = 0;
        var timecode = options.StartTimecode ?? new Timecode(0);
        string? currentPageNumber = null; // Track current page for filtering

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
                var packetBytesRead = await inputStream.ReadAsync(packetMemory, cancellationToken);

                if (packetBytesRead != Constants.PACKET_HEADER_SIZE)
                    break;

                var packet = new Packet(packetBuffer.AsSpan(0, Constants.PACKET_HEADER_SIZE).ToArray())
                {
                    Timecode = timecode
                };

                for (var l = 0; l < packet.LineCount; l++)
                {
                    var lineMemory = lineBuffer.AsMemory(0, Constants.LINE_HEADER_SIZE);
                    var lineBytesRead = await inputStream.ReadAsync(lineMemory, cancellationToken);

                    if (lineBytesRead < Constants.LINE_HEADER_SIZE)
                        break;

                    var line = new Line(lineBuffer.AsSpan(0, Constants.LINE_HEADER_SIZE).ToArray())
                    {
                        LineNumber = lineNumber,
                        LineTimecode = timecode
                    };

                    if (line.Length <= 0)
                    {
                        throw new InvalidDataException("Line length is invalid.");
                    }

                    // Use the more efficient ParseLine method
                    await line.ParseLineAsync(inputStream, outputFormat, cancellationToken);

                    // Track current page for filtering
                    if (line.Row == 0 && line.Data.Length >= Constants.T42_LINE_SIZE)
                    {
                        var pageNumber = T42.GetPageNumber(line.Data);
                        if (pageNumber != null)
                        {
                            currentPageNumber = pageNumber;
                        }
                    }

                    // Apply filtering
                    // Magazine filtering
                    if (options.Magazine.HasValue && line.Magazine != options.Magazine.Value)
                    {
                        lineNumber++;
                        continue;
                    }

                    // Page number filtering
                    if (!string.IsNullOrEmpty(options.PageNumber))
                    {
                        if (currentPageNumber == null || currentPageNumber != options.PageNumber)
                        {
                            lineNumber++;
                            continue;
                        }
                    }

                    // Row filtering
                    if (rows != null && !rows.Contains(line.Row))
                    {
                        lineNumber++;
                        continue;
                    }

                    // Caption content filtering - skip rows with only spaces/control codes
                    if (options.UseCaps && line.Row > 0 && line.Data.Length >= Constants.T42_LINE_SIZE && !T42.HasMeaningfulContent(line.Data))
                    {
                        lineNumber++;
                        continue;
                    }

                    packet.Lines.Add(line);
                    lineNumber++;
                }

                // Only yield packets that have at least one line after filtering
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
}
