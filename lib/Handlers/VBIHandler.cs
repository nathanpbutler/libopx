using System.Buffers;
using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;

namespace nathanbutlerDEV.libopx.Handlers;

/// <summary>
/// Handler for parsing VBI (Vertical Blanking Interval) format files.
/// Handles both single and double-line VBI data with automatic format detection
/// and support for conversion to T42 teletext format.
/// </summary>
public class VBIHandler : IFormatHandler
{
    /// <summary>
    /// Gets or sets the VBI input format (VBI or VBI_DOUBLE).
    /// </summary>
    public Format VbiInputFormat { get; set; } = Format.VBI;

    /// <inheritdoc />
    public Format InputFormat => VbiInputFormat;

    /// <inheritdoc />
    public Format[] ValidOutputs => [Format.VBI, Format.VBI_DOUBLE, Format.T42, Format.RCWT, Format.STL];

    /// <inheritdoc />
    public int LineLength => VbiInputFormat == Format.VBI_DOUBLE ? Constants.VBI_DOUBLE_LINE_SIZE : Constants.VBI_LINE_SIZE;

    /// <summary>
    /// Creates a new instance of VBIHandler.
    /// </summary>
    /// <param name="vbiType">The VBI format type (VBI or VBI_DOUBLE)</param>
    public VBIHandler(Format? vbiType = Format.VBI)
    {
        VbiInputFormat = vbiType ?? Format.VBI;
    }

    /// <inheritdoc />
    public IEnumerable<Line> Parse(Stream inputStream, ParseOptions options)
    {
        if (inputStream == null)
            throw new ArgumentNullException(nameof(inputStream));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        // Use default rows if not specified
        var rows = options.Rows ?? Constants.DEFAULT_ROWS;
        var outputFormat = options.OutputFormat;

        // Initialize RCWT state if needed
        if (outputFormat == Format.RCWT)
        {
            Functions.ResetRCWTHeader();
        }

        int lineNumber = 0;
        var timecode = new Timecode(0);
        var vbiBuffer = new byte[LineLength];

        while (inputStream.Read(vbiBuffer, 0, LineLength) == LineLength)
        {
            // Increment timecode if LineCount is reached
            if (lineNumber % options.LineCount == 0 && lineNumber != 0)
            {
                timecode = timecode.GetNext();
            }

            // Create a basic Line object for VBI data
            var line = new Line()
            {
                LineNumber = lineNumber,
                Data = [.. vbiBuffer],
                Length = LineLength,
                SampleCoding = VbiInputFormat == Format.VBI_DOUBLE ? 0x32 : 0x31,
                SampleCount = LineLength,
                LineTimecode = timecode,
            };

            // Process the VBI data based on output format
            // For T42, RCWT, and STL: convert to T42 first to extract metadata
            if (outputFormat is Format.T42 or Format.RCWT or Format.STL)
            {
                try
                {
                    // Convert VBI to T42 using the static method
                    var t42Data = VBI.ToT42(vbiBuffer);

                    // Update line properties for T42
                    line.Data = t42Data;
                    line.Length = t42Data.Length;
                    line.SampleCoding = 0x31; // T42 sample coding
                    line.SampleCount = t42Data.Length;
                    line.SetCachedType(Format.T42); // Ensure line.Type reports T42 for RCWT/STL generation

                    // Extract T42 metadata if conversion successful
                    if (t42Data.Length >= Constants.T42_LINE_SIZE && t42Data.Any(b => b != 0))
                    {
                        line.Magazine = T42.GetMagazine(t42Data[0]);
                        line.Row = T42.GetRow([.. t42Data.Take(2)]);
                        line.Text = T42.GetText([.. t42Data.Skip(2)], line.Row == 0);

                        // For RCWT/STL output, keep the T42 data - WriteOutputAsync will handle packet generation
                        // This prevents double-conversion (T42 -> RCWT -> RCWT or T42 -> STL -> STL)
                    }
                    else
                    {
                        // Conversion resulted in blank data, skip this line
                        lineNumber++;
                        continue;
                    }
                }
                catch
                {
                    // If conversion fails, skip this line
                    lineNumber++;
                    continue;
                }
            }
            else if (outputFormat == Format.VBI_DOUBLE && LineLength == Constants.VBI_LINE_SIZE)
            {
                line.Data = Functions.Double(vbiBuffer);
                line.Magazine = -1; // No magazine for VBI
                line.Row = -1; // No row for VBI
                line.Text = Constants.T42_BLANK_LINE; // Default blank line text
            }
            else
            {
                // For VBI output, keep original data
                line.Magazine = -1;
                line.Row = -1;
                line.Text = Constants.T42_BLANK_LINE;
            }

            // Apply filtering if specified and conversion was successful
            if (options.Magazine.HasValue && line.Magazine != options.Magazine.Value)
            {
                lineNumber++;
                continue;
            }

            if (rows != null && !rows.Contains(line.Row))
            {
                lineNumber++;
                continue;
            }

            yield return line;
            lineNumber++;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Line> ParseAsync(
        Stream inputStream,
        ParseOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (inputStream == null)
            throw new ArgumentNullException(nameof(inputStream));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        // Use default rows if not specified
        var rows = options.Rows ?? Constants.DEFAULT_ROWS;
        var outputFormat = options.OutputFormat;

        // Initialize RCWT state if needed
        if (outputFormat == Format.RCWT)
        {
            Functions.ResetRCWTHeader();
        }

        int lineNumber = 0;
        var timecode = new Timecode(0);

        // Use ArrayPool for better memory management
        var arrayPool = ArrayPool<byte>.Shared;
        var vbiBuffer = arrayPool.Rent(LineLength);

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Use Memory<byte> for async operations
                var bufferMemory = vbiBuffer.AsMemory(0, LineLength);
                var bytesRead = await inputStream.ReadAsync(bufferMemory, cancellationToken);

                if (bytesRead != LineLength)
                    break; // End of stream or incomplete read

                // Increment timecode if LineCount is reached
                if (lineNumber % options.LineCount == 0 && lineNumber != 0)
                {
                    timecode = timecode.GetNext();
                }

                // Create line with efficient buffer copying
                var line = new Line()
                {
                    LineNumber = lineNumber,
                    Data = vbiBuffer.AsSpan(0, LineLength).ToArray(), // Only copy what we need
                    Length = LineLength,
                    SampleCoding = VbiInputFormat == Format.VBI_DOUBLE ? 0x32 : 0x31,
                    SampleCount = LineLength,
                    LineTimecode = timecode,
                };

                // Process the VBI data based on output format
                // For T42, RCWT, and STL: convert to T42 first to extract metadata
                if (outputFormat is Format.T42 or Format.RCWT or Format.STL)
                {
                    try
                    {
                        var t42Data = VBI.ToT42(line.Data);
                        if (t42Data.Length >= Constants.T42_LINE_SIZE && t42Data.Any(b => b != 0))
                        {
                            line.Data = t42Data;
                            line.Length = t42Data.Length;
                            line.SampleCoding = 0x31;
                            line.SampleCount = t42Data.Length;
                            line.SetCachedType(Format.T42); // Ensure correct type for RCWT/STL
                            line.Magazine = T42.GetMagazine(t42Data[0]);
                            line.Row = T42.GetRow([.. t42Data.Take(2)]);
                            line.Text = T42.GetText([.. t42Data.Skip(2)], line.Row == 0);

                            // For RCWT/STL output, keep the T42 data - WriteOutputAsync will handle packet generation
                            // This prevents double-conversion (T42 -> RCWT -> RCWT or T42 -> STL -> STL)
                        }
                        else
                        {
                            lineNumber++;
                            continue;
                        }
                    }
                    catch
                    {
                        lineNumber++;
                        continue;
                    }
                }
                else if (outputFormat == Format.VBI_DOUBLE && LineLength == Constants.VBI_LINE_SIZE)
                {
                    line.Data = Functions.Double(line.Data);
                    line.Magazine = -1;
                    line.Row = -1;
                    line.Text = Constants.T42_BLANK_LINE;
                }
                else
                {
                    line.Magazine = -1;
                    line.Row = -1;
                    line.Text = Constants.T42_BLANK_LINE;
                }

                // Apply filtering
                if (options.Magazine.HasValue && line.Magazine != options.Magazine.Value)
                {
                    lineNumber++;
                    continue;
                }

                if (rows != null && !rows.Contains(line.Row))
                {
                    lineNumber++;
                    continue;
                }

                yield return line;
                lineNumber++;
            }
        }
        finally
        {
            arrayPool.Return(vbiBuffer);
        }
    }
}
