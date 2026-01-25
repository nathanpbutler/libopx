using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.libopx;

/// <summary>
/// Represents a data packet containing multiple lines with header information and timecode data.
/// Used for parsing structured teletext and VBI data from various formats.
/// </summary>
public class Packet : IDisposable
{
    /// <summary>
    /// The header of the packet
    /// </summary>
    public byte[] Header { get; set; } = [];
    /// <summary>
    /// The timecode of the packet
    /// </summary>
    public Timecode Timecode { get; set; } = new Timecode(0);
    /// <summary>
    /// Gets the number of lines in this packet, derived from the first two header bytes.
    /// </summary>
    public int LineCount => Header[0] << 8 | Header[1];
    /// <summary>
    /// Gets or sets the teletext magazine number for this packet. Default is 8.
    /// </summary>
    public int Magazine { get; set; } = Constants.DEFAULT_MAGAZINE;
    /// <summary>
    /// Gets or sets the array of teletext row numbers for filtering. Default includes all rows.
    /// </summary>
    public int[] Row { get; set; } = Constants.DEFAULT_ROWS;
    /// <summary>
    /// Gets or sets the collection of lines contained within this packet.
    /// </summary>
    public List<Line> Lines { get; set; } = [];

    /// <summary>
    /// Initializes a new instance of the Packet class with default values.
    /// </summary>
    public Packet() {}

    /// <summary>
    /// Initializes a new instance of the Packet class with the specified header data.
    /// </summary>
    /// <param name="header">The 2-byte header containing packet information</param>
    /// <exception cref="ArgumentException">Thrown when header is not exactly 2 bytes long</exception>
    public Packet(byte[] header)
    {
        if (header.Length != Constants.PACKET_HEADER_SIZE)
            throw new ArgumentException("Header must be exactly 2 bytes long.");

        Header = header;
        // Pre-allocate with known capacity for better performance
        Lines = new List<Line>(LineCount);
    }

    /// <summary>
    /// Releases all resources used by the packet and disposes of all contained lines.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        // Dispose of any resources if necessary
        foreach (var line in Lines)
        {
            line.Dispose();
        }
        Lines.Clear();
    }

    /// <summary>
    /// Parses raw data bytes into a collection of line objects.
    /// </summary>
    /// <param name="data">The raw data bytes to parse into lines</param>
    /// <returns>A list of parsed line objects</returns>
    public static List<Line> ParseLines(byte[] data)
    {
        var lines = new List<Line>();
        var dataSpan = data.AsSpan();
        int offset = 0;

        while (offset < dataSpan.Length)
        {
            // Check if we have enough bytes for a header
            if (offset + Constants.LINE_HEADER_SIZE > dataSpan.Length)
                break;

            // Use span slicing instead of array allocation
            var lineHeaderSpan = dataSpan.Slice(offset, Constants.LINE_HEADER_SIZE);
            var lineHeader = lineHeaderSpan.ToArray(); // Only allocate when necessary

            var line = new Line(lineHeader);

            offset += Constants.LINE_HEADER_SIZE;

            // Check if we have enough bytes for the line data
            if (offset + line.Length > dataSpan.Length)
            {
                throw new ArgumentException("Data does not contain enough bytes for the specified line length.");
            }

            // Use span slicing for data as well
            var lineDataSpan = dataSpan.Slice(offset, line.Length);
            line.Data = lineDataSpan.ToArray(); // Only allocate when necessary
            offset += line.Length;

            lines.Add(line);
        }

        return lines;
    }

    /// <summary>
    /// More efficient parsing method that avoids intermediate allocations
    /// </summary>
    public static List<Line> ParseLinesOptimized(ReadOnlySpan<byte> data)
    {
        var lines = new List<Line>();
        int offset = 0;

        while (offset < data.Length)
        {
            if (offset + Constants.LINE_HEADER_SIZE > data.Length)
                break;

            var lineHeaderSpan = data.Slice(offset, Constants.LINE_HEADER_SIZE);
            var lineHeader = lineHeaderSpan.ToArray();

            var line = new Line(lineHeader);
            lines.Add(line);
            offset += Constants.LINE_HEADER_SIZE;

            if (offset + line.Length > data.Length)
            {
                throw new ArgumentException("Data does not contain enough bytes for the specified line length.");
            }

            var lineDataSpan = data.Slice(offset, line.Length);
            line.Data = lineDataSpan.ToArray();
            offset += line.Length;
        }

        return lines;
    }

    /// <summary>
    /// Returns a string representation of the packet using default magazine and row filters.
    /// </summary>
    /// <returns>A formatted string containing packet information</returns>
    public override string ToString()
    {
        return ToString(Magazine, Row);
    }

    /// <summary>
    /// Returns a string representation of the packet filtered by the specified magazine and rows.
    /// </summary>
    /// <param name="magazine">The magazine number to filter by</param>
    /// <param name="row">The array of row numbers to include in the output</param>
    /// <returns>A formatted string containing filtered packet information</returns>
    public string ToString(int magazine, int[] row)
    {
        var sb = new StringBuilder();

        foreach (var line in Lines)
        {
            // Otherwise, filter by magazine and row
            if (line.Magazine == magazine && row.Contains(line.Row))
            {
                sb.AppendLine($"{Constants.T42_DEFAULT_COLORS}{Timecode} {line.Magazine} {line.Row:D2} {Constants.T42_ANSI_RESET}{line.Text}");
            }
        }

        return sb.ToString().TrimEnd();
    }
}
