using System.Collections;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;

namespace nathanbutlerDEV.libopx.Core;

/// <summary>
/// Provides static methods for converting between teletext data formats.
/// Centralizes all format conversion logic for VBI, T42, RCWT, and STL formats.
/// </summary>
public static class FormatConverter
{
    #region Format-to-Format Conversions

    /// <summary>
    /// Converts VBI or VBI_DOUBLE format data to T42 teletext format.
    /// </summary>
    /// <param name="lineData">VBI line data (must be 720 or 1440 bytes)</param>
    /// <param name="debug">Enable debug output during conversion</param>
    /// <returns>42-byte T42 teletext line, or empty array if conversion fails</returns>
    /// <exception cref="ArgumentException">Thrown when lineData is not 720 or 1440 bytes</exception>
    public static byte[] VBIToT42(byte[] lineData, bool debug = false)
    {
        if (lineData.Length != Constants.VBI_LINE_SIZE && lineData.Length != Constants.VBI_DOUBLE_LINE_SIZE)
        {
            throw new ArgumentException($"Line data must be {Constants.VBI_LINE_SIZE} or {Constants.VBI_DOUBLE_LINE_SIZE} bytes long.");
        }
        // Double the line data
        var newLine = lineData.Length == Constants.VBI_DOUBLE_LINE_SIZE ? lineData : Functions.Double(lineData);

        // Normalise the line data
        var normalised = Functions.Normalise(newLine);

        // Create a BitArray from the normalised line
        var bits = Functions.GetBits(normalised);

        // Get the offset of the line
        var offset = Functions.GetOffset(bits);

        // If the offset is not within valid range, return a blank byte array
        if (offset is <= -1 or >= Constants.VBI_MAX_OFFSET_RANGE)
        {
            // Return a blank byte array
            return new byte[Constants.T42_LINE_SIZE];
        }

        // Get the T42 bytes from the line
        var t42 = T42.Get(bits, offset, debug);

        return t42;
    }

    /// <summary>
    /// Converts T42 teletext format data to VBI or VBI_DOUBLE format.
    /// </summary>
    /// <param name="t42bytes">T42 data to convert (must be 42 bytes)</param>
    /// <param name="outputFormat">Target format (Format.VBI for 720 bytes or Format.VBI_DOUBLE for 1440 bytes)</param>
    /// <returns>VBI or VBI_DOUBLE formatted data</returns>
    public static byte[] T42ToVBI(byte[] t42bytes, Format outputFormat = Format.VBI)
    {
        // If T42 data is not the correct size, return blank VBI
        if (t42bytes.Length != Constants.T42_LINE_SIZE)
        {
            return new byte[outputFormat == Format.VBI_DOUBLE ? Constants.VBI_DOUBLE_LINE_SIZE : Constants.VBI_LINE_SIZE];
        }

        // Prepend the clock run-in and framing code to the T42 data
        byte[] lineData = [0x55, 0x55, 0x27, .. t42bytes];

        // Create a new BitArray from the line data
        BitArray bits = new(lineData);

        // Create a new byte array with a length of 360 (45 * 8)
        var bytes = new byte[Constants.VBI_BITS_SIZE];

        // For each bit in the BitArray...
        for (var b = 0; b < Constants.VBI_BITS_SIZE; b++)
        {
            // Set the byte at the current index to the Constants.VBI_HIGH_VALUE or Constants.VBI_LOW_VALUE value
            bytes[b] = bits[b] ? Constants.VBI_HIGH_VALUE : Constants.VBI_LOW_VALUE;
        }

        // Create a new byte array with a length of Constants.VBI_RESIZE_BYTES
        var resized = Constants.VBI_RESIZE_BYTES;

        // For each byte in the Constants.VBI_RESIZE_BYTES...
        for (var i = 0; i < Constants.VBI_RESIZE_SIZE; i++)
        {
            // Calculate the original position
            var originalPosition = i * Constants.VBI_SCALE;
            // Calculate the left pixel
            var leftPixel = (int)originalPosition;
            // Calculate the right pixel
            var rightPixel = Math.Min(leftPixel + 1, Constants.VBI_BITS_SIZE - 1);
            // Calculate the right weight
            var rightWeight = originalPosition - leftPixel;
            // Calculate the left weight
            var leftWeight = 1f - rightWeight;

            // Calculate the resized byte
            resized[i] = (byte)(bytes[leftPixel] * leftWeight + bytes[rightPixel] * rightWeight);
        }

        resized = [.. Constants.VBI_PADDING_BYTES.Take(Constants.VBI_PAD_START).Concat(resized).Concat(Constants.VBI_PADDING_BYTES).Take(Constants.VBI_LINE_SIZE)];

        if (outputFormat == Format.VBI_DOUBLE)
        {
            resized = Functions.Double(resized);
        }

        return resized;
    }

    /// <summary>
    /// Converts VBI format data to VBI_DOUBLE format (720 bytes to 1440 bytes).
    /// </summary>
    /// <param name="vbiData">VBI line data (must be 720 bytes)</param>
    /// <returns>1440-byte VBI_DOUBLE formatted data</returns>
    public static byte[] VBIToVBIDouble(byte[] vbiData)
    {
        return Functions.Double(vbiData);
    }

    /// <summary>
    /// Converts VBI_DOUBLE format data to VBI format (1440 bytes to 720 bytes).
    /// Decimates by keeping every other byte.
    /// </summary>
    /// <param name="vbiDoubleData">VBI_DOUBLE line data (must be 1440 bytes)</param>
    /// <returns>720-byte VBI formatted data</returns>
    public static byte[] VBIDoubleToVBI(byte[] vbiDoubleData)
    {
        return vbiDoubleData.Where((b, i) => i % 2 == 0).ToArray();
    }

    #endregion

    #region Format-to-Wrapper Conversions

    /// <summary>
    /// Converts T42 teletext data to RCWT (Raw Captions With Time) packet format.
    /// Creates a 53-byte packet: [packet_type][fts_bytes][field_marker][framing_code][t42_data].
    /// </summary>
    /// <param name="t42Data">T42 data to convert (must be 42 bytes)</param>
    /// <param name="fts">Frame Time Stamp in milliseconds</param>
    /// <param name="fieldNumber">Field number (0 or 1)</param>
    /// <param name="verbose">Enable verbose debug output</param>
    /// <returns>53-byte RCWT packet</returns>
    public static byte[] T42ToRCWT(byte[] t42Data, int fts, int fieldNumber, bool verbose = false)
    {
        if (verbose)
        {
            Console.Error.WriteLine($"DEBUG: FormatConverter.T42ToRCWT called - T42DataLength: {t42Data.Length}, FTS: {fts}, Field: {fieldNumber}");
        }

        // Ensure we have 42 bytes of T42 data
        if (t42Data.Length != Constants.T42_LINE_SIZE)
        {
            if (verbose)
            {
                Console.Error.WriteLine($"DEBUG: Invalid T42 data length {t42Data.Length}, expected {Constants.T42_LINE_SIZE}. Creating blank data.");
            }
            t42Data = new byte[Constants.T42_LINE_SIZE];
        }

        // Create RCWT packet: [packet_type][fts_bytes][field_marker][framing_code][t42_data]
        var packet = new List<byte>
        {
            // Add packet type (1 byte)
            Constants.RCWT_PACKET_TYPE_UNKNOWN
        };

        // Add FTS bytes (8 bytes, padded if necessary)
        var ftsBytes = GetFTSBytes(fts);
        packet.AddRange(ftsBytes);

        // Add field marker (1 byte)
        packet.Add(GetFieldMarkerByte(fieldNumber));

        // Add framing code (1 byte)
        packet.Add(Constants.RCWT_FRAMING_CODE);

        // Add T42 data payload (42 bytes)
        packet.AddRange(t42Data);

        var result = packet.ToArray();
        if (verbose)
        {
            Console.Error.WriteLine($"DEBUG: Created RCWT packet - Size: {result.Length} bytes, FTS: {fts}, Field: {fieldNumber}");
        }

        return result;
    }

    /// <summary>
    /// Converts T42 teletext data to EBU STL (EBU-Tech 3264) TTI block format.
    /// Creates a 128-byte subtitle block with timing and text information.
    /// </summary>
    /// <param name="t42Data">T42 data to convert (must be 42 bytes)</param>
    /// <param name="subtitleNumber">Sequential subtitle number</param>
    /// <param name="row">Teletext row number (0-31)</param>
    /// <param name="timeCodeIn">Start timecode for the subtitle</param>
    /// <param name="timeCodeOut">End timecode for the subtitle (defaults to timeCodeIn if null)</param>
    /// <param name="verbose">Enable verbose debug output</param>
    /// <returns>128-byte STL TTI block containing timing and text data</returns>
    public static byte[] T42ToSTL(byte[] t42Data, int subtitleNumber, int row, Timecode timeCodeIn, Timecode? timeCodeOut = null, bool verbose = false)
    {
        if (verbose)
        {
            Console.Error.WriteLine($"DEBUG: FormatConverter.T42ToSTL called - T42DataLength: {t42Data.Length}, Subtitle: {subtitleNumber}, Row: {row}");
        }

        // Ensure we have 42 bytes of T42 data
        if (t42Data.Length != Constants.T42_LINE_SIZE)
        {
            if (verbose)
            {
                Console.Error.WriteLine($"DEBUG: Invalid T42 data length {t42Data.Length}, expected {Constants.T42_LINE_SIZE}. Creating blank data.");
            }
            t42Data = new byte[Constants.T42_LINE_SIZE];
        }

        // Create TTI block (128 bytes)
        var tti = new byte[Constants.STL_TTI_BLOCK_SIZE];

        // Subtitle Group Number (SGN) - byte 0
        tti[0] = Constants.STL_SUBTITLE_GROUP;

        // Subtitle Number (SN) - bytes 1-2 (big-endian)
        tti[1] = (byte)((subtitleNumber >> 8) & 0xFF);
        tti[2] = (byte)(subtitleNumber & 0xFF);

        // Extension Block Number (EBN) - byte 3 (0xFF = not part of extension)
        tti[3] = 0xFF;

        // Cumulative Status (CS) - byte 4
        tti[4] = Constants.STL_CUMULATIVE_STATUS;

        // Time Code In (TCI) - bytes 5-8 (HH:MM:SS:FF in BCD format)
        var tciBytes = EncodeTimecodeToSTL(timeCodeIn);
        Array.Copy(tciBytes, 0, tti, 5, 4);

        // Time Code Out (TCO) - bytes 9-12 (HH:MM:SS:FF in BCD format)
        var tcoBytes = EncodeTimecodeToSTL(timeCodeOut ?? timeCodeIn);
        Array.Copy(tcoBytes, 0, tti, 9, 4);

        // Vertical Position (VP) - byte 13 (row number, 0-based)
        // Map teletext row (0-31) to STL vertical position
        tti[13] = row >= 0 && row <= 31 ? (byte)row : (byte)0;

        // Justification Code (JC) - byte 14 (0x02 = left-justified)
        tti[14] = 0x02;

        // Comment Flag (CF) - byte 15 (0x00 = contains subtitle data)
        tti[15] = 0x00;

        // Text Field (TF) - bytes 16-127 (112 bytes)
        // Extract T42 data and convert to STL format (strip parity, remap control codes)
        byte[] textData = ExtractSTLTextData(t42Data, row, verbose);
        Array.Copy(textData, 0, tti, 16, Math.Min(textData.Length, Constants.STL_TEXT_FIELD_SIZE));

        // Fill remaining text field with spaces if needed
        for (int i = 16 + textData.Length; i < 128; i++)
        {
            tti[i] = 0x8F; // STL space character
        }

        if (verbose)
        {
            Console.Error.WriteLine($"DEBUG: Created STL TTI block - Size: {tti.Length} bytes, Subtitle: {subtitleNumber}, TCI: {timeCodeIn}, Row: {row}");
        }

        return tti;
    }

    #endregion

    #region Shared Helper Methods

    /// <summary>
    /// Encodes a timecode to STL format (4 bytes in BCD format: HH, MM, SS, FF).
    /// </summary>
    /// <param name="timecode">The timecode to encode</param>
    /// <returns>4-byte array representing the timecode in BCD format</returns>
    public static byte[] EncodeTimecodeToSTL(Timecode timecode)
    {
        return
        [
            (byte)((timecode.Hours / 10 << 4) | (timecode.Hours % 10)),
            (byte)((timecode.Minutes / 10 << 4) | (timecode.Minutes % 10)),
            (byte)((timecode.Seconds / 10 << 4) | (timecode.Seconds % 10)),
            (byte)((timecode.Frames / 10 << 4) | (timecode.Frames % 10))
        ];
    }

    /// <summary>
    /// Extracts and converts T42 text data to STL format.
    /// Strips parity bits and remaps control codes to STL equivalents.
    /// </summary>
    /// <param name="t42Data">T42 data to extract text from (should be 42 bytes)</param>
    /// <param name="row">Teletext row number (0 for header, 1-24 for captions)</param>
    /// <param name="verbose">Enable verbose debug output</param>
    /// <returns>Byte array containing STL-formatted text data (max 112 bytes)</returns>
    public static byte[] ExtractSTLTextData(byte[] t42Data, int row, bool verbose = false)
    {
        if (t42Data.Length < Constants.T42_LINE_SIZE)
        {
            // Pad with zeros if needed
            var padded = new byte[Constants.T42_LINE_SIZE];
            Array.Copy(t42Data, padded, Math.Min(t42Data.Length, Constants.T42_LINE_SIZE));
            t42Data = padded;
        }

        var stlText = new List<byte>();

        // Row 0 (header): Skip first 10 bytes, parse last 32
        // Rows 1-24 (captions): Skip first 2 bytes (mag/row), parse remaining 40
        int startIndex = row == 0 ? 10 : 2;

        for (int i = startIndex; i < t42Data.Length && stlText.Count < Constants.STL_TEXT_FIELD_SIZE; i++)
        {
            byte b = t42Data[i];

            // Strip parity bit (bit 7)
            byte stripped = (byte)(b & 0x7F);

            if (stripped == Constants.T42_BLOCK_START_BYTE)
            {
                // T42 Start Box -> STL Start Box
                stlText.Add(Constants.STL_START_BOX);
            }
            else if (stripped == Constants.T42_NORMAL_HEIGHT)
            {
                // T42 Normal Height -> STL End Box
                stlText.Add(Constants.STL_END_BOX);
            }
            else if (stripped <= 7)
            {
                // T42 color codes (0-7) map directly to STL alpha color codes
                stlText.Add(stripped);
            }
            else if (stripped is >= 0x20 and <= 0x7F)
            {
                // Displayable ASCII characters
                stlText.Add(stripped);
            }
            else if (stripped == 0x00)
            {
                // Null byte -> space
                stlText.Add(0x20);
            }
            else
            {
                // Other control codes -> space
                stlText.Add(0x20);
            }
        }

        if (verbose)
        {
            Console.Error.WriteLine($"DEBUG: Extracted STL text data - {stlText.Count} bytes from {t42Data.Length} T42 bytes (Row: {row}, StartIndex: {startIndex})");
        }

        return [.. stlText];
    }

    /// <summary>
    /// Converts FTS value to 8-byte array, padding with zeros if necessary.
    /// </summary>
    /// <param name="fts">Frame Time Stamp in milliseconds</param>
    /// <returns>8-byte array representing the FTS value</returns>
    private static byte[] GetFTSBytes(int fts)
    {
        var ftsBytes = BitConverter.GetBytes(fts);
        return ftsBytes.Length == Constants.RCWT_FTS_BYTE_SIZE
            ? ftsBytes
            : [.. ftsBytes, .. new byte[Constants.RCWT_FTS_BYTE_SIZE - ftsBytes.Length]];
    }

    /// <summary>
    /// Gets the field marker byte based on field number.
    /// </summary>
    /// <param name="fieldNumber">Field number (0 or 1)</param>
    /// <returns>Field marker byte (0xAF for field 0, 0xAB for field 1)</returns>
    private static byte GetFieldMarkerByte(int fieldNumber)
    {
        return fieldNumber == 0 ? Constants.RCWT_FIELD_0_MARKER : Constants.RCWT_FIELD_1_MARKER;
    }

    #endregion
}
