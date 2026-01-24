using System.Buffers;
using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;

namespace nathanbutlerDEV.libopx.Handlers;

/// <summary>
/// Handler for parsing T42 teletext format files.
/// Implements core parsing logic with support for conversion to VBI formats
/// and teletext content extraction.
/// </summary>
public class T42Handler : ILineFormatHandler
{
    /// <inheritdoc />
    public Format InputFormat => Format.T42;

    /// <inheritdoc />
    public Format[] ValidOutputs => [Format.T42, Format.VBI, Format.VBI_DOUBLE, Format.RCWT, Format.STL];

    /// <inheritdoc />
    public int LineLength => Constants.T42_LINE_SIZE;

    /// <summary>
    /// Creates a new instance of T42Handler.
    /// </summary>
    public T42Handler()
    {
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
        var t42Buffer = new byte[LineLength];
        string? currentPageNumber = null; // Track current page for filtering

        while (inputStream.Read(t42Buffer, 0, LineLength) == LineLength)
        {
            // Increment timecode if LineCount is reached
            if (lineNumber % options.LineCount == 0 && lineNumber != 0)
            {
                timecode = timecode.GetNext();
            }

            // Create a basic Line object for T42 data
            var line = new Line()
            {
                LineNumber = lineNumber,
                Data = [.. t42Buffer],
                Length = LineLength,
                SampleCoding = 0x31, // T42 sample coding
                SampleCount = LineLength,
                LineTimecode = timecode,
            };

            // Explicitly set the cached type to T42 for proper ToRCWT conversion
            line.SetCachedType(Format.T42);

            // Extract T42 metadata
            if (t42Buffer.Length >= Constants.T42_LINE_SIZE && t42Buffer.Any(b => b != 0))
            {
                line.Magazine = T42.GetMagazine(t42Buffer[0]);
                line.Row = T42.GetRow([.. t42Buffer.Take(2)]);

                // For header rows (row 0), extract and display page number
                var pageNumber = line.Row == 0 ? T42.GetPageNumber(t42Buffer) : null;
                if (line.Row == 0 && pageNumber != null)
                {
                    currentPageNumber = pageNumber;
                }

                line.Text = T42.GetText([.. t42Buffer.Skip(2)], line.Row == 0, line.Magazine, pageNumber);
            }
            else
            {
                // Empty data, skip this line
                lineNumber++;
                continue;
            }

            // Process the T42 data based on output format
            if (outputFormat == Format.VBI || outputFormat == Format.VBI_DOUBLE)
            {
                try
                {
                    // Convert T42 to VBI using the static method
                    var vbiData = Core.FormatConverter.T42ToVBI(t42Buffer, outputFormat);

                    // Update line properties for VBI
                    line.Data = vbiData;
                    line.Length = vbiData.Length;
                    line.SampleCoding = outputFormat == Format.VBI_DOUBLE ? 0x32 : 0x31;
                    line.SampleCount = vbiData.Length;

                    // For VBI output, clear T42-specific metadata
                    line.Magazine = -1;
                    line.Row = -1;
                    line.Text = Constants.T42_BLANK_LINE;
                }
                catch
                {
                    // If conversion fails, skip this line
                    lineNumber++;
                    continue;
                }
            }
            // For RCWT/STL output, keep the T42 data - WriteOutputAsync will handle packet generation
            // This prevents double-conversion (T42 -> RCWT -> RCWT)

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
            if (options.UseCaps && line.Row > 0 && !T42.HasMeaningfulContent(t42Buffer))
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

        var rows = options.Rows ?? Constants.DEFAULT_ROWS;
        var outputFormat = options.OutputFormat;

        // Initialize RCWT state if needed
        if (outputFormat == Format.RCWT)
        {
            Functions.ResetRCWTHeader();
        }

        int lineNumber = 0;
        var timecode = new Timecode(0);
        string? currentPageNumber = null; // Track current page for filtering

        var arrayPool = ArrayPool<byte>.Shared;
        var t42Buffer = arrayPool.Rent(LineLength);

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var bufferMemory = t42Buffer.AsMemory(0, LineLength);
                var bytesRead = await inputStream.ReadAsync(bufferMemory, cancellationToken);

                if (bytesRead != LineLength)
                    break;

                if (lineNumber % options.LineCount == 0 && lineNumber != 0)
                {
                    timecode = timecode.GetNext();
                }

                var line = new Line()
                {
                    LineNumber = lineNumber,
                    Data = t42Buffer.AsSpan(0, LineLength).ToArray(),
                    Length = LineLength,
                    SampleCoding = 0x31,
                    SampleCount = LineLength,
                    LineTimecode = timecode,
                };

                // Explicitly set the cached type to T42 for proper ToRCWT conversion
                line.SetCachedType(Format.T42);

                // Extract T42 metadata
                if (line.Data.Length >= Constants.T42_LINE_SIZE && line.Data.Any(b => b != 0))
                {
                    line.Magazine = T42.GetMagazine(line.Data[0]);
                    line.Row = T42.GetRow([.. line.Data.Take(2)]);

                    // For header rows (row 0), extract and display page number
                    var pageNumber = line.Row == 0 ? T42.GetPageNumber(line.Data) : null;
                    if (line.Row == 0 && pageNumber != null)
                    {
                        currentPageNumber = pageNumber;
                    }

                    line.Text = T42.GetText([.. line.Data.Skip(2)], line.Row == 0, line.Magazine, pageNumber);
                }
                else
                {
                    lineNumber++;
                    continue;
                }

                // Format conversion logic (same as synchronous version)
                if (outputFormat == Format.VBI || outputFormat == Format.VBI_DOUBLE)
                {
                    try
                    {
                        var vbiData = Core.FormatConverter.T42ToVBI(line.Data, outputFormat);
                        line.Data = vbiData;
                        line.Length = vbiData.Length;
                        line.SampleCoding = outputFormat == Format.VBI_DOUBLE ? 0x32 : 0x31;
                        line.SampleCount = vbiData.Length;
                        line.Magazine = -1;
                        line.Row = -1;
                        line.Text = Constants.T42_BLANK_LINE;
                    }
                    catch
                    {
                        lineNumber++;
                        continue;
                    }
                }
                // For RCWT/STL output, keep the T42 data - WriteOutputAsync will handle packet generation

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
                if (options.UseCaps && line.Row > 0 && !T42.HasMeaningfulContent(line.Data))
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
            arrayPool.Return(t42Buffer);
        }
    }
}
