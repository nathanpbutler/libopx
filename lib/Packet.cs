using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.libopx;

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
    public int LineCount => Header[0] << 8 | Header[1];
    public int Magazine { get; set; } = Constants.DEFAULT_MAGAZINE; // Default magazine for T42
    public int[] Row { get; set; } = Constants.DEFAULT_ROWS; // Default rows for T42
    public List<Line> Lines { get; set; } = []; // 0 or more lines

    public Packet() {} // Default constructor

    public Packet(byte[] header)
    {
        if (header.Length != Constants.PACKET_HEADER_SIZE)
            throw new ArgumentException("Header must be exactly 2 bytes long.");

        Header = header;
        // Pre-allocate with known capacity for better performance
        Lines = new List<Line>(LineCount);
    }

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

    // ToString override (prints out the parsed data if text is not null or empty)
    public override string ToString()
    {
        return ToString(Magazine, Row);
    }

    // ToString but with magazine and row filters
    public string ToString(int magazine, int[] row)
    {
        var sb = new StringBuilder();

        foreach (var line in Lines)
        {
            // Otherwise, filter by magazine and row
            if (line.Magazine == magazine && row.Contains(line.Row))
            {
                sb.AppendLine($"{Timecode} {line.Magazine} {line.Row:D2} {line.Text}");
            }
        }

        return sb.ToString().TrimEnd();
    }
}
