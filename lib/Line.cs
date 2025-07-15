using System;

namespace nathanbutlerDEV.libopx;

/// <summary>
/// A class representing a line of data in a packet
/// This new Line class is a replacement for Data and RCWT and contains
/// </summary>
public class Line : IDisposable
{
    public const int HeaderSize = 14; // Size of the line header in bytes

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

    // Cache the type calculation to avoid repeated computation
    private LineType? _cachedType;

    /// <summary>
    /// Type of the line. Determined by either the length or if there is a CRIFC
    /// </summary>
    public LineType Type
    {
        get
        {
            if (_cachedType.HasValue)
                return _cachedType.Value;

            if (Length == 720)
            {
                _cachedType = LineType.VBI; // VBI lines have no data
            }
            else if (Header.Length > 0 && Header[^1] == 0x01)
            {
                _cachedType = LineType.T42; // T42 lines end with 0x01
            }
            else
            {
                _cachedType = LineType.Unknown; // Unknown line type
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
        if (header.Length != HeaderSize)
            throw new ArgumentException($"Header must be exactly {HeaderSize} bytes", nameof(header));

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
        if (headerSpan.Length != HeaderSize)
            throw new ArgumentException($"Header must be exactly {HeaderSize} bytes");

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
        // No need to explicitly clear arrays - GC will handle it
        // Array.Clear is actually slower in this case
    }

    /// <summary>
    /// Gets the Type of the Line based on the Length, or if the Data contains a clock run-in and framing code
    /// </summary>
    /// <param name="data">The data to check</param>
    /// <returns>The type of the line</returns>
    public static LineType GetLineType(byte[] data)
    {
        if (data.Length == 720)
            return LineType.VBI;

        if (data.Length >= 45 && data.Length < 720)
        {
            var crifc = Functions.GetCrifc(data);
            if (crifc >= 0)
                return LineType.T42;
        }

        return LineType.Unknown;
    }

    /// <summary>
    /// Gets the Type of the Line based on the Length, or if the Data contains a clock run-in and framing code
    /// More efficient version using spans
    /// </summary>
    /// <param name="data">The data to check</param>
    /// <returns>The type of the line</returns>
    public static LineType GetLineType(ReadOnlySpan<byte> data)
    {
        if (data.Length == 720)
            return LineType.VBI;

        if (data.Length >= 45 && data.Length < 720)
        {
            var crifc = Functions.GetCrifc(data.ToArray()); // TODO: Update Functions.GetCrifc to accept spans
            if (crifc >= 0)
                return LineType.T42;
        }

        return LineType.Unknown;
    }

    /// <summary>
    /// Parses the line data from a stream.
    /// </summary>
    /// <param name="input">The input stream</param>
    public void ParseLine(Stream input)
    {
        if (Length <= 0)
        {
            throw new InvalidDataException("Line length is invalid.");
        }

        // Read the data into the line
        var bytesRead = input.Read(Data, 0, Length);
        if (bytesRead < Length)
        {
            throw new InvalidDataException($"Not enough data to read the line. Expected {Length}, got {bytesRead}.");
        }
    }

    /// <summary>
    /// Parses the line data from a byte array.
    /// </summary>
    /// <param name="data">The byte array containing the line data</param>
    public void ParseLine(byte[] data)
    {
        ParseLine(data.AsSpan());
    }

    /// <summary>
    /// Parses the line data from a span (most efficient version).
    /// </summary>
    /// <param name="data">The span containing the line data</param>
    public void ParseLine(ReadOnlySpan<byte> data)
    {
        if (Length <= 0)
        {
            throw new InvalidDataException("Line length is invalid.");
        }

        if (data.Length < Length)
        {
            throw new InvalidDataException($"Not enough data to read the line. Expected {Length}, got {data.Length}.");
        }

        Data = new byte[Length];
        data[..Length].CopyTo(Data);
    }
}

public enum LineType
{
    /// <summary>
    /// VBI line type
    /// </summary>
    VBI,

    /// <summary>
    /// T42 line type
    /// </summary>
    T42,

    /// <summary>
    /// Unknown line type
    /// </summary>
    Unknown
}