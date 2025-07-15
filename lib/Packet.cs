using System;
using System.Diagnostics.CodeAnalysis;

namespace nathanbutlerDEV.libopx;

public class Packet : IDisposable
{
    public const int HeaderSize = 2; // Size of the packet header in bytes
    /// <summary>
    /// The header of the packet
    /// </summary>
    public byte[] Header { get; set; } = [];
    /// <summary>
    /// The timecode of the packet
    /// </summary>
    public Timecode Timecode { get; set; } = new Timecode(0);
    public int LineCount => Header[0] << 8 | Header[1];
    public List<Line> Lines { get; set; } = []; // 0 or more lines

    [SetsRequiredMembers]
    public Packet(byte[] header)
    {
        if (header.Length != HeaderSize)
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
            if (offset + Line.HeaderSize > dataSpan.Length)
                break;
                
            // Use span slicing instead of array allocation
            var lineHeaderSpan = dataSpan.Slice(offset, Line.HeaderSize);
            var lineHeader = lineHeaderSpan.ToArray(); // Only allocate when necessary
            
            var line = new Line(lineHeader);
            lines.Add(line);
            offset += Line.HeaderSize;
            
            // Check if we have enough bytes for the line data
            if (offset + line.Length > dataSpan.Length)
            {
                throw new ArgumentException("Data does not contain enough bytes for the specified line length.");
            }
            
            // Use span slicing for data as well
            var lineDataSpan = dataSpan.Slice(offset, line.Length);
            line.Data = lineDataSpan.ToArray(); // Only allocate when necessary
            offset += line.Length;
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
            if (offset + Line.HeaderSize > data.Length)
                break;
                
            var lineHeaderSpan = data.Slice(offset, Line.HeaderSize);
            var lineHeader = lineHeaderSpan.ToArray();
            
            var line = new Line(lineHeader);
            lines.Add(line);
            offset += Line.HeaderSize;
            
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
}
