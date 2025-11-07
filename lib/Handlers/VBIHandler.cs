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
public class VBIHandler : ILineFormatHandler
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

            // Save original VBI data before any conversions
            var originalVbiData = vbiBuffer.ToArray();

            // Always convert to T42 to extract metadata (Magazine, Row, Text)
            byte[] t42Data;
            try
            {
                t42Data = VBI.ToT42(vbiBuffer);

                // Check if conversion resulted in valid data
                if (t42Data.Length < Constants.T42_LINE_SIZE || !t42Data.Any(b => b != 0))
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

            // Create Line object with T42 metadata
            var line = new Line()
            {
                LineNumber = lineNumber,
                Data = t42Data, // Temporarily set to T42 data
                Length = t42Data.Length,
                SampleCoding = 0x31,
                SampleCount = t42Data.Length,
                LineTimecode = timecode,
                Magazine = T42.GetMagazine(t42Data[0]),
                Row = T42.GetRow([.. t42Data.Take(2)]),
                Text = T42.GetText([.. t42Data.Skip(2)], T42.GetRow([.. t42Data.Take(2)]) == 0)
            };

            // Apply filtering (works for all output formats since we have T42 metadata)
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

            // Set final output data based on output format
            if (outputFormat is Format.T42 or Format.RCWT or Format.STL)
            {
                // Keep T42 data
                line.Data = t42Data;
                line.Length = t42Data.Length;
                line.SampleCoding = 0x31;
                line.SampleCount = t42Data.Length;
                line.SetCachedType(Format.T42); // Ensure line.Type reports T42 for RCWT/STL generation
            }
            else if (outputFormat == Format.VBI_DOUBLE)
            {
                // For VBI_DOUBLE output, use doubled VBI data
                if (LineLength == Constants.VBI_LINE_SIZE)
                {
                    line.Data = Functions.Double(originalVbiData);
                }
                else
                {
                    line.Data = originalVbiData; // Already VBI_DOUBLE
                }
                line.Length = line.Data.Length; // Update length to reflect doubled data
                line.SampleCoding = 0x32;
                line.SampleCount = line.Data.Length;
            }
            else // Format.VBI
            {
                // For VBI output, use original VBI data
                line.Data = originalVbiData;
                line.Length = originalVbiData.Length;
                line.SampleCoding = VbiInputFormat == Format.VBI_DOUBLE ? 0x32 : 0x31;
                line.SampleCount = originalVbiData.Length;
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

                // Save original VBI data before any conversions
                var originalVbiData = vbiBuffer.AsSpan(0, LineLength).ToArray();

                // Always convert to T42 to extract metadata (Magazine, Row, Text)
                byte[] t42Data;
                try
                {
                    t42Data = VBI.ToT42(originalVbiData);

                    // Check if conversion resulted in valid data
                    if (t42Data.Length < Constants.T42_LINE_SIZE || !t42Data.Any(b => b != 0))
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

                // Create Line object with T42 metadata
                var line = new Line()
                {
                    LineNumber = lineNumber,
                    Data = t42Data, // Temporarily set to T42 data
                    Length = t42Data.Length,
                    SampleCoding = 0x31,
                    SampleCount = t42Data.Length,
                    LineTimecode = timecode,
                    Magazine = T42.GetMagazine(t42Data[0]),
                    Row = T42.GetRow([.. t42Data.Take(2)]),
                    Text = T42.GetText([.. t42Data.Skip(2)], T42.GetRow([.. t42Data.Take(2)]) == 0)
                };

                // Apply filtering (works for all output formats since we have T42 metadata)
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

                // Set final output data based on output format
                if (outputFormat is Format.T42 or Format.RCWT or Format.STL)
                {
                    // Keep T42 data
                    line.Data = t42Data;
                    line.Length = t42Data.Length;
                    line.SampleCoding = 0x31;
                    line.SampleCount = t42Data.Length;
                    line.SetCachedType(Format.T42); // Ensure line.Type reports T42 for RCWT/STL generation
                }
                else if (outputFormat == Format.VBI_DOUBLE)
                {
                    // For VBI_DOUBLE output, use doubled VBI data
                    if (LineLength == Constants.VBI_LINE_SIZE)
                    {
                        line.Data = Functions.Double(originalVbiData);
                    }
                    else
                    {
                        line.Data = originalVbiData; // Already VBI_DOUBLE
                    }
                    line.Length = line.Data.Length; // Update length to reflect doubled data
                    line.SampleCoding = 0x32;
                    line.SampleCount = line.Data.Length;
                }
                else // Format.VBI
                {
                    // For VBI output, use original VBI data
                    line.Data = originalVbiData;
                    line.Length = originalVbiData.Length;
                    line.SampleCoding = VbiInputFormat == Format.VBI_DOUBLE ? 0x32 : 0x31;
                    line.SampleCount = originalVbiData.Length;
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
