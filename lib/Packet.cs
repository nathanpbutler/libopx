using System;

namespace nathanbutlerDEV.libopx;

public class Packet
{
    public required byte[] Header { get; set; } // 2 bytes
    public List<Line> Lines { get; set; } = []; // 0 or more lines

    public Packet(byte[] header)
    {
        if (header.Length != 2)
        {
            throw new ArgumentException("Header must be exactly 2 bytes long.");
        }

        Header = header;

        // Set the number of lines by the result of the header
        var lineCount = header[0] << 8 | header[1];
        if (lineCount <= 0)
        {
            throw new ArgumentException("Line count must be greater than zero.");
        }

        // Initialize the list of lines
        Lines = new List<Line>(lineCount);
    }

    public async Task<bool> ParseLinesAsync(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);

        for (int i = 0; i < Lines.Count; i++)
        {
            var header = new byte[14];
            if (await input.ReadAsync(header) != header.Length)
                throw new InvalidOperationException("Failed to read line header.");

            Lines[i] = new Line(header);

            Lines[i].Data = new byte[Lines[i].Length];
            if (await input.ReadAsync(Lines[i].Data) != Lines[i].Length)
                throw new InvalidOperationException("Failed to read line data.");
            if (Lines[i].Data.Length != Lines[i].Length)
                throw new InvalidOperationException("Line data length does not match expected length.");
        }

        return true; // Return true to indicate success
    }
}
