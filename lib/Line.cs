using System;

namespace nathanbutlerDEV.libopx;

/// <summary>
/// A class representing a line of data in a packet
/// This new Line class is a replacement for Data and RCWT and contains
/// </summary>
public class Line
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
        if (header.Length != 14)
            throw new ArgumentException("Header must be exactly 14 bytes", nameof(header));

        Header = header;

        // Header must end with 0x01
        if (header[^1] != 0x01)
            throw new ArgumentException("Invalid header type, expected 0x01", nameof(header));

        // Parse the header
        Number = header[0] << 8 | header[1];
        Wrapping = header[2];
        SampleCoding = header[3];
        SampleCount = header[4] << 8 | header[5];
        Length = header[8] << 8 | header[9];

        if (Length < 0 || Length > 10000)
            throw new ArgumentException($"Invalid line length: {Length}", nameof(header));

        Data = new byte[Length]; // Set the data to the length of the line
    }
}
