using System;
using System.Collections;
using System.Text;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;

namespace nathanbutlerDEV.libopx;

/// <summary>
/// A class representing a line of data in a packet
/// This new Line class is a replacement for Data and RCWT and contains
/// </summary>
public class Line : IDisposable
{
    /// <summary>
    /// Header of the line.
    /// The header is a byte array that contains the first 14 bytes of the line.
    /// </summary>
    private byte[] Header { get; } = [];

    /// <summary>
    /// Number of the line.
    /// The Number integer value is stored in the first 2 bytes of the header.
    /// The Number is in big-endian format.
    /// </summary>
    public int Number { get; }

    /// <summary>
    /// Wrapping type of the line.
    /// The Wrapping integer value is stored in the third byte of the header.
    /// If the type is 2, then we need to pad and crop the Data by 8 bytes.
    /// Otherwise, the type is 1.
    /// </summary>
    public int Wrapping { get; }

    /// <summary>
    /// Sample coding of the line.
    /// The Sample Coding integer value is stored in the fourth byte of the header.
    /// The Sample Coding is in big-endian format.
    /// </summary>
    public int SampleCoding { get; set; }

    /// <summary>
    /// Sample count of the line.
    /// The Sample Count integer value is stored in the fifth and sixth bytes of the header.
    /// The Sample Count is in big-endian format.
    /// </summary>
    public int SampleCount { get; set; }

    /// <summary>
    /// Length of the line.
    /// The Length integer value is stored in the eighth and ninth bytes of the header.
    /// The Length is in big-endian format.
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// Data of the line.
    /// The Data is a byte array that contains the data of the line.
    /// The Data is stored after the header.
    /// </summary>
    public byte[] Data { get; set; } = [];
    /// <summary>
    /// Gets or sets the optional timecode associated with this line for filtering operations.
    /// </summary>
    public Timecode? LineTimecode { get; set; }
    /// <summary>
    /// Gets or sets the optional line number for filtering and sequencing operations.
    /// </summary>
    public int LineNumber { get; set; }
    /// <summary>
    /// Gets or sets the teletext magazine number. Default is -1 for non-teletext data.
    /// </summary>
    public int Magazine { get; set; } = -1;
    /// <summary>
    /// Gets or sets the teletext row number. Default is -1 for non-teletext data.
    /// </summary>
    public int Row { get; set; } = -1;
    /// <summary>
    /// Gets or sets the decoded teletext content as formatted text with ANSI colors.
    /// </summary>
    public string Text { get; set; } = Constants.T42_BLANK_LINE;

    // Cache the type calculation to avoid repeated computation
    private Format? _cachedType;

    /// <summary>
    /// Type of the line. Determined by either the length or if there is a CRIFC
    /// </summary>
    public Format Type
    {
        get
        {
            if (_cachedType.HasValue)
                return _cachedType.Value;

            if (Length == Constants.VBI_LINE_SIZE)
            {
                _cachedType = Format.VBI; // VBI lines have no data
            }
            else if (Length == Constants.VBI_DOUBLE_LINE_SIZE)
            {
                _cachedType = Format.VBI_DOUBLE; // Double VBI lines
            }
            else if (Data.Length >= Constants.T42_LINE_SIZE && Data.Length < Constants.VBI_LINE_SIZE)
            {
                var crifc = Functions.GetCrifc(Data);
                if (crifc >= 0)
                {
                    _cachedType = Format.T42; // T42 lines have a CRIFC
                }
                else
                {
                    _cachedType = Format.Unknown; // Unknown line type
                }
            }
            else
            {
                _cachedType = Format.Unknown; // Unknown line type
            }

            return _cachedType.Value;
        }
    }

    /// <summary>
    /// Basic constructor for the Line class without any header or data set.
    /// </summary>
    public Line() { }

    /// <summary>
    /// Constructor for the Line class.
    /// This is used to create a line from a header.
    /// </summary>
    /// <param name="header">The header of the line</param>
    /// <exception cref="ArgumentException">Thrown when the header is not 14 bytes</exception>
    /// <exception cref="ArgumentException">Thrown when the length is not between 0 and 10000</exception>
    public Line(byte[] header)
    {
        // If the header is 14 bytes, then we are parsing an MXFData line
        if (header.Length != Constants.LINE_HEADER_SIZE)
            throw new ArgumentException($"Header must be exactly {Constants.LINE_HEADER_SIZE} bytes", nameof(header));

        Header = header;

        // Header must end with 0x01
        if (header[^1] != 0x01)
            throw new ArgumentException("Invalid header type, expected 0x01", nameof(header));

        // Parse the header more efficiently using bit operations
        Number = (header[0] << 8) | header[1];
        Wrapping = header[2];
        SampleCoding = header[3];
        SampleCount = (header[4] << 8) | header[5];
        Length = (header[8] << 8) | header[9];

        if (Length < 0 || Length > 10000)
            throw new ArgumentException($"Invalid line length: {Length}", nameof(header));

        Data = new byte[Length]; // Set the data to the length of the line
    }

    /// <summary>
    /// Constructor for the Line class with span input for better performance.
    /// </summary>
    /// <param name="headerSpan">The header span of the line</param>
    /// <exception cref="ArgumentException">Thrown when the header is not 14 bytes</exception>
    /// <exception cref="ArgumentException">Thrown when the length is not between 0 and 10000</exception>
    public Line(ReadOnlySpan<byte> headerSpan)
    {
        if (headerSpan.Length != Constants.LINE_HEADER_SIZE)
            throw new ArgumentException($"Header must be exactly {Constants.LINE_HEADER_SIZE} bytes");

        Header = headerSpan.ToArray();

        if (headerSpan[^1] != 0x01)
            throw new ArgumentException("Invalid header type, expected 0x01");

        Number = (headerSpan[0] << 8) | headerSpan[1];
        Wrapping = headerSpan[2];
        SampleCoding = headerSpan[3];
        SampleCount = (headerSpan[4] << 8) | headerSpan[5];
        Length = (headerSpan[8] << 8) | headerSpan[9];

        if (Length < 0 || Length > 10000)
            throw new ArgumentException($"Invalid line length: {Length}");

        Data = new byte[Length];
    }

    /// <summary>
    /// Disposes the resources used by the Line.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Gets the Type of the Line based on the Length, or if the Data contains a clock run-in and framing code
    /// </summary>
    /// <param name="data">The data to check</param>
    /// <returns>The type of the line</returns>
    public static Format GetFormat(byte[] data)
    {
        if (data.Length == 720)
            return Format.VBI;

        if (data.Length >= 45 && data.Length < 720)
        {
            var crifc = Functions.GetCrifc(data);
            if (crifc >= 0)
                return Format.T42;
        }

        return Format.Unknown;
    }

    /// <summary>
    /// Gets the Type of the Line based on the Length, or if the Data contains a clock run-in and framing code
    /// More efficient version using spans
    /// </summary>
    /// <param name="data">The data to check</param>
    /// <returns>The type of the line</returns>
    public static Format GetFormat(ReadOnlySpan<byte> data)
    {
        if (data.Length == 720)
            return Format.VBI;

        if (data.Length >= 45 && data.Length < 720)
        {
            var crifc = Functions.GetCrifc(data);
            if (crifc >= 0)
                return Format.T42;
        }

        return Format.Unknown;
    }

    /// <summary>
    /// Parses the line data from a stream.
    /// </summary>
    /// <param name="input">The input stream</param>
    /// <param name="outputFormat">The desired output format for conversion</param>
    public void ParseLine(Stream input, Format outputFormat = Format.Unknown)
    {
        if (Length <= 0)
        {
            throw new InvalidDataException("Line length is invalid.");
        }

        var data = new byte[Length];

        // Read the data into the line
        var bytesRead = input.Read(data, 0, Length);
        if (bytesRead < Length)
        {
            throw new InvalidDataException($"Not enough data to read the line. Expected {Length}, got {bytesRead}.");
        }

        // Parse the line with the read data
        ParseLine(data, outputFormat);
    }

    /// <summary>
    /// Parses line data from a byte array and converts it to the specified output format.
    /// </summary>
    /// <param name="data">The raw line data bytes to parse</param>
    /// <param name="outputFormat">The desired output format for conversion</param>
    public void ParseLine(byte[] data, Format outputFormat = Format.Unknown)
    {
        // Determine the input format first
        Format inputFormat = GetFormat(data);
        
        // Handle MXF data with CRIFC
        if (data.Length >= Constants.T42_PLUS_CRIFC && data.Length < Constants.VBI_LINE_SIZE)
        {
            var crifc = Functions.GetCrifc(data);
            if (crifc >= 0)
            {
                // Extract T42 data after CRIFC
                var remainingBytes = data.Length - (crifc + 3);
                if (remainingBytes >= Constants.T42_LINE_SIZE)
                {
                    data = [.. data.Skip(crifc + 3).Take(Constants.T42_LINE_SIZE)];
                    inputFormat = Format.T42;
                }
                else
                {
                    // Not enough data for a complete T42 line
                    Data = data;
                    Magazine = -1;
                    Row = -1;
                    Text = Constants.T42_BLANK_LINE;
                    _cachedType = Format.Unknown;
                    return;
                }
            }
            else
            {
                Data = [.. data.Take(Length)];
                Magazine = -1;
                Row = -1;
                Text = Constants.T42_BLANK_LINE;
                _cachedType = Format.Unknown;
                return;
            }
        }

        // If no output format specified, use input format
        if (outputFormat == Format.Unknown)
        {
            outputFormat = inputFormat;
        }

        // Perform format conversion if needed
        if (inputFormat != outputFormat)
        {
            try
            {
                data = ConvertFormat(data, inputFormat, outputFormat);
                _cachedType = outputFormat;
            }
            catch
            {
                // If conversion fails, use original data and format
                outputFormat = inputFormat;
                _cachedType = inputFormat;
            }
        }
        else
        {
            _cachedType = inputFormat;
        }

        // Set the converted/original data
        Data = data;
        Length = data.Length;

        // Update sample coding based on output format
        SampleCoding = outputFormat switch
        {
            Format.VBI_DOUBLE => 0x32,
            Format.T42 => 0x31,
            Format.VBI => 0x31,
            _ => SampleCoding
        };
        SampleCount = data.Length;

        // Extract metadata based on output format
        ExtractMetadata(outputFormat);
    }

    /// <summary>
    /// Converts data from one format to another
    /// </summary>
    /// <param name="data">Input data</param>
    /// <param name="inputFormat">Source format</param>
    /// <param name="outputFormat">Target format</param>
    /// <returns>Converted data</returns>
    private static byte[] ConvertFormat(byte[] data, Format inputFormat, Format outputFormat)
    {
        return (inputFormat, outputFormat) switch
        {
            // VBI to T42 conversion
            (Format.VBI, Format.T42) or (Format.VBI_DOUBLE, Format.T42) => VBI.ToT42(data),

            // VBI to VBI_DOUBLE conversion
            (Format.VBI, Format.VBI_DOUBLE) => Functions.Double(data),

            // T42 to VBI conversion
            (Format.T42, Format.VBI) => T42.ToVBI(data, Format.VBI),

            // T42 to VBI_DOUBLE conversion
            (Format.T42, Format.VBI_DOUBLE) => T42.ToVBI(data, Format.VBI_DOUBLE),

            // VBI_DOUBLE to VBI conversion (take every other byte)
            (Format.VBI_DOUBLE, Format.VBI) => [.. data.Where((b, i) => i % 2 == 0)],

            // Same format - no conversion needed
            _ when inputFormat == outputFormat => data,

            // Unsupported conversion
            _ => throw new NotSupportedException($"Conversion from {inputFormat} to {outputFormat} is not supported")
        };
    }

    /// <summary>
    /// Extracts metadata based on the format
    /// </summary>
    /// <param name="format">The format to extract metadata for</param>
    private void ExtractMetadata(Format format)
    {
        switch (format)
        {
            case Format.T42:
                if (Data.Length >= Constants.T42_LINE_SIZE && Data.Any(b => b != 0))
                {
                    Magazine = T42.GetMagazine(Data[0]);
                    Row = T42.GetRow([.. Data.Take(2)]);
                    Text = T42.GetText([.. Data.Skip(2)], Row == 0);
                }
                else
                {
                    Magazine = -1;
                    Row = -1;
                    Text = Constants.T42_BLANK_LINE;
                }
                break;
                
            case Format.VBI:
            case Format.VBI_DOUBLE:
                // VBI formats don't have magazine/row metadata
                Magazine = -1;
                Row = -1;
                Text = Constants.T42_BLANK_LINE;
                break;
                
            default:
                Magazine = -1;
                Row = -1;
                Text = Constants.T42_BLANK_LINE;
                break;
        }
    }

    /// <summary>
    /// Returns a string representation of the line including timecode, magazine, row, and text content.
    /// </summary>
    /// <returns>A formatted string containing line information</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();

        if (LineTimecode != null)
        {
            sb.Append($"{LineTimecode} {Magazine} {Row:D2} {Text}");
        }
        else
        {
            sb.Append($"{LineNumber.ToString().PadLeft(11)} {Magazine} {Row:D2} {Text}");
        }

        return sb.ToString().TrimEnd();
    }
}