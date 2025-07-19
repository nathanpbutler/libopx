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
    public Timecode LineTimecode { get; set; } = new Timecode(); // Optional timecode for Filtering lines
    public int LineNumber { get; set; } // Optional line number/timecode for Filtering lines
    public int Magazine { get; set; } = -1;
    public int Row { get; set; } = -1;
    public string Text { get; set; } = Constants.T42_BLANK_LINE; // Default text for T42 lines

    // Cache the type calculation to avoid repeated computation
    private LineFormat? _cachedType;

    /// <summary>
    /// Type of the line. Determined by either the length or if there is a CRIFC
    /// </summary>
    public LineFormat Type
    {
        get
        {
            if (_cachedType.HasValue)
                return _cachedType.Value;

            if (Length == Constants.VBI_LINE_SIZE)
            {
                _cachedType = LineFormat.VBI; // VBI lines have no data
            }
            else if (Length == Constants.VBI_DOUBLE_LINE_SIZE)
            {
                _cachedType = LineFormat.VBI_DOUBLE; // Double VBI lines
            }
            else if (Data.Length >= Constants.T42_LINE_SIZE && Data.Length < Constants.VBI_LINE_SIZE)
            {
                var crifc = Functions.GetCrifc(Data);
                if (crifc >= 0)
                {
                    _cachedType = LineFormat.T42; // T42 lines have a CRIFC
                }
                else
                {
                    _cachedType = LineFormat.Unknown; // Unknown line type
                }
            }
            else
            {
                _cachedType = LineFormat.Unknown; // Unknown line type
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
    public static LineFormat GetLineFormat(byte[] data)
    {
        if (data.Length == 720)
            return LineFormat.VBI;

        if (data.Length >= 45 && data.Length < 720)
        {
            var crifc = Functions.GetCrifc(data);
            if (crifc >= 0)
                return LineFormat.T42;
        }

        return LineFormat.Unknown;
    }

    /// <summary>
    /// Gets the Type of the Line based on the Length, or if the Data contains a clock run-in and framing code
    /// More efficient version using spans
    /// </summary>
    /// <param name="data">The data to check</param>
    /// <returns>The type of the line</returns>
    public static LineFormat GetLineFormat(ReadOnlySpan<byte> data)
    {
        if (data.Length == 720)
            return LineFormat.VBI;

        if (data.Length >= 45 && data.Length < 720)
        {
            var crifc = Functions.GetCrifc(data);
            if (crifc >= 0)
                return LineFormat.T42;
        }

        return LineFormat.Unknown;
    }

    /// <summary>
    /// Parses the line data from a stream.
    /// </summary>
    /// <param name="input">The input stream</param>
    public void ParseLine(Stream input, LineFormat outputFormat = LineFormat.Unknown)
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

        if (data.Length >= Constants.T42_PLUS_CRIFC && data.Length < Constants.VBI_LINE_SIZE)
        {
            var crifc = Functions.GetCrifc(data);
            if (crifc >= 0)
            {
                // If the CRIFC is valid, we can set the type to T42
                _cachedType = LineFormat.T42;

                // Ensure we have enough data after CRIFC for a full T42 line
                var remainingBytes = data.Length - (crifc + 3);
                if (remainingBytes >= Constants.T42_LINE_SIZE)
                {
                    Data = [.. data.Skip(crifc + 3).Take(Constants.T42_LINE_SIZE)];
                    Magazine = T42.GetMagazine(Data[0]);
                    Row = T42.GetRow([.. Data.Take(2)]);
                    Text = T42.GetText([.. Data.Skip(2)]);
                }
                else
                {
                    // Not enough data for a complete T42 line
                    Data = data; // Store the data as is
                    Magazine = -1; // Unknown magazine
                    Row = -1; // Unknown row
                    Text = Constants.T42_BLANK_LINE; // Default text for T42 lines
                    _cachedType = LineFormat.Unknown;
                }
            }
            else
            {
                _cachedType = LineFormat.Unknown; // Unknown line type
                Data = [.. data.Take(Length)]; // Store the data as is
                Magazine = -1; // Unknown magazine
                Row = -1; // Unknown row
                Text = Constants.T42_BLANK_LINE; // Default text for T42 lines
            }
        }
        else
        {
            _cachedType = GetLineFormat(data); // Use the static method to determine the type
            Data = [.. data.Take(Length)];
            
            // Convert VBI to T42 if requested
            if (outputFormat == LineFormat.T42 && (_cachedType == LineFormat.VBI || _cachedType == LineFormat.VBI_DOUBLE))
            {
                try
                {
                    // Convert VBI data to T42
                    var t42Data = VBI.ToT42(Data);
                    
                    // Update line properties for T42
                    Data = t42Data;
                    _cachedType = LineFormat.T42;
                    SampleCoding = 0x31; // T42 sample coding
                    SampleCount = t42Data.Length;
                    
                    // Extract T42 metadata
                    if (t42Data.Length >= Constants.T42_LINE_SIZE)
                    {
                        Magazine = T42.GetMagazine(Data[0]);
                        Row = T42.GetRow([.. Data.Take(2)]);
                        Text = T42.GetText([.. Data.Skip(2)]);
                    }
                    else
                    {
                        Magazine = -1;
                        Row = -1;
                        Text = Constants.T42_BLANK_LINE;
                    }
                }
                catch
                {
                    // If conversion fails, keep original data
                    Magazine = -1;
                    Row = -1;
                    Text = Constants.T42_BLANK_LINE;
                }
            }
        }
    }

    // ToString override
    public override string ToString()
    {
        var sb = new StringBuilder();
        
        sb.Append($"{LineNumber.ToString().PadLeft(8)} {Magazine} {Row:D2} {Text}");

        return sb.ToString().TrimEnd();
    }
}