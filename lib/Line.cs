using System;
using System.Buffers;
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
            else if (Data.Length == Constants.T42_LINE_SIZE)
            {
                // Exact 42-byte payload (pure T42 line without CRIFC prefix)
                _cachedType = Format.T42;
            }
            else if (Data.Length > Constants.T42_LINE_SIZE && Data.Length < Constants.VBI_LINE_SIZE)
            {
                // Longer than 42 but shorter than VBI: likely contains CRI + FC preceding T42
                var crifc = Functions.GetCrifc(Data);
                _cachedType = crifc >= 0 ? Format.T42 : Format.Unknown;
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
    /// Asynchronously parses the line data from a stream.
    /// </summary>
    /// <param name="input">The input stream</param>
    /// <param name="outputFormat">The desired output format for conversion</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    public async Task ParseLineAsync(Stream input, Format outputFormat = Format.Unknown, CancellationToken cancellationToken = default)
    {
        if (Length <= 0)
            throw new InvalidDataException("Line length is invalid.");

        var arrayPool = ArrayPool<byte>.Shared;
        var dataBuffer = arrayPool.Rent(Length);
        
        try
        {
            var dataMemory = dataBuffer.AsMemory(0, Length);
            var bytesRead = await input.ReadAsync(dataMemory, cancellationToken);
            
            if (bytesRead < Length)
                throw new InvalidDataException($"Not enough data to read the line. Expected {Length}, got {bytesRead}.");

            // Use existing ParseLine logic with the buffer
            ParseLine(dataBuffer.AsSpan(0, Length).ToArray(), outputFormat);
        }
        finally
        {
            arrayPool.Return(dataBuffer);
        }
    }

    /// <summary>
    /// Sets the cached type for the line. This is used when the line type is known explicitly.
    /// </summary>
    /// <param name="format">The format to cache</param>
    internal void SetCachedType(Format format)
    {
        _cachedType = format;
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

        if (data.Length == Constants.T42_LINE_SIZE)
            return Format.T42; // Pure 42-byte T42 line

        if (data.Length > Constants.T42_LINE_SIZE && data.Length < Constants.VBI_LINE_SIZE)
        {
            var crifc = Functions.GetCrifc(data);
            if (crifc >= 0) return Format.T42;
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

        if (data.Length == Constants.T42_LINE_SIZE)
            return Format.T42;

        if (data.Length > Constants.T42_LINE_SIZE && data.Length < Constants.VBI_LINE_SIZE)
        {
            var crifc = Functions.GetCrifc(data);
            if (crifc >= 0) return Format.T42;
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
                // RCWT is a virtual wrapper format; internally we still treat the payload as T42
                _cachedType = outputFormat == Format.RCWT ? Format.T42 : outputFormat;
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
            // Treat RCWT like T42 for sample coding purposes
            Format.RCWT => 0x31,
            _ => SampleCoding
        };
        SampleCount = data.Length;

        // Extract metadata based on output format
        if (outputFormat == Format.RCWT)
        {
            // RCWT encapsulates a T42 payload; extract as T42 so magazine/row/text are populated
            ExtractMetadata(Format.T42);
        }
        else
        {
            ExtractMetadata(outputFormat);
        }
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

            // RCWT conversions - keep as T42 for now, actual RCWT packet generation happens in parsers
            (Format.VBI, Format.RCWT) or (Format.VBI_DOUBLE, Format.RCWT) => VBI.ToT42(data),
            (Format.T42, Format.RCWT) => [.. data.Take(Constants.T42_LINE_SIZE)],

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

    /// <summary>
    /// Converts the line data to RCWT (Raw Captions With Time) format.
    /// </summary>
    /// <param name="fts">Frame Time Stamp in milliseconds</param>
    /// <param name="fieldNumber">Field number (0 or 1)</param>
    /// <param name="verbose">Whether to output verbose debug information</param>
    /// <returns>RCWT packet bytes containing packet type, FTS, field marker, framing code, and T42 data payload</returns>
    public byte[] ToRCWT(int fts, int fieldNumber, bool verbose)
    {
        if (verbose) Console.Error.WriteLine($"DEBUG: ToRCWT called - Type: {Type}, DataLength: {Data.Length}, Magazine: {Magazine}, Row: {Row}");
        
        // Ensure we have T42 data as the payload
        byte[] t42Data;
        if (Type == Format.T42 && Data.Length >= Constants.T42_LINE_SIZE)
        {
            // Use existing T42 data (first 42 bytes)
            t42Data = [.. Data.Take(Constants.T42_LINE_SIZE)];
            if (verbose) Console.Error.WriteLine($"DEBUG: Using existing T42 data ({t42Data.Length} bytes)");
        }
        else if (Type == Format.VBI || Type == Format.VBI_DOUBLE)
        {
            // Convert VBI to T42 first
            try
            {
                var convertedData = ConvertFormat(Data, Type, Format.T42);
                t42Data = [.. convertedData.Take(Constants.T42_LINE_SIZE)];
                if (verbose) Console.Error.WriteLine($"DEBUG: Converted VBI to T42 ({t42Data.Length} bytes)");
            }
            catch (Exception ex)
            {
                // If conversion fails, create blank T42 data
                t42Data = new byte[Constants.T42_LINE_SIZE];
                if (verbose) Console.Error.WriteLine($"DEBUG: VBI to T42 conversion failed: {ex.Message}, using blank T42 data");
            }
        }
        else
        {
            // For unknown or other formats, create blank T42 data
            t42Data = new byte[Constants.T42_LINE_SIZE];
            if (verbose) Console.Error.WriteLine($"DEBUG: Unknown format {Type}, using blank T42 data");
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
        if (verbose) Console.Error.WriteLine($"DEBUG: Created RCWT packet - Size: {result.Length} bytes, FTS: {fts}, Field: {fieldNumber}");
        
        return result;
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

    /// <summary>
    /// Converts the line data to EBU STL (EBU-Tech 3264) TTI block format.
    /// </summary>
    /// <param name="subtitleNumber">Sequential subtitle number</param>
    /// <param name="timeCodeIn">Start timecode for the subtitle</param>
    /// <param name="timeCodeOut">End timecode for the subtitle</param>
    /// <param name="verbose">Whether to output verbose debug information</param>
    /// <returns>TTI block bytes (128 bytes) containing timing and text data without odd-parity encoding</returns>
    public byte[] ToSTL(int subtitleNumber, Timecode timeCodeIn, Timecode? timeCodeOut = null, bool verbose = false)
    {
        if (verbose) Console.Error.WriteLine($"DEBUG: ToSTL called - Type: {Type}, DataLength: {Data.Length}, Magazine: {Magazine}, Row: {Row}");

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
        tti[13] = Row >= 0 && Row <= 31 ? (byte)Row : (byte)0;

        // Justification Code (JC) - byte 14 (0x02 = left-justified)
        tti[14] = 0x02;

        // Comment Flag (CF) - byte 15 (0x00 = contains subtitle data)
        tti[15] = 0x00;

        // Text Field (TF) - bytes 16-127 (112 bytes)
        // Extract T42 data and convert to STL format (strip parity, remap control codes)
        byte[] textData = ExtractSTLTextData(verbose);
        Array.Copy(textData, 0, tti, 16, Math.Min(textData.Length, Constants.STL_TEXT_FIELD_SIZE));

        // Fill remaining text field with spaces if needed
        for (int i = 16 + textData.Length; i < 128; i++)
        {
            tti[i] = 0x8F; // STL space character
        }

        if (verbose) Console.Error.WriteLine($"DEBUG: Created STL TTI block - Size: {tti.Length} bytes, Subtitle: {subtitleNumber}, TCI: {timeCodeIn}, Row: {Row}");

        return tti;
    }

    /// <summary>
    /// Encodes a timecode to STL format (4 bytes in BCD format: HH, MM, SS, FF).
    /// </summary>
    /// <param name="timecode">The timecode to encode</param>
    /// <returns>4-byte array representing the timecode in BCD format</returns>
    private static byte[] EncodeTimecodeToSTL(Timecode timecode)
    {
        var tc = new byte[4];

        // Convert each component to BCD (Binary Coded Decimal)
        tc[0] = (byte)((timecode.Hours / 10 << 4) | (timecode.Hours % 10));
        tc[1] = (byte)((timecode.Minutes / 10 << 4) | (timecode.Minutes % 10));
        tc[2] = (byte)((timecode.Seconds / 10 << 4) | (timecode.Seconds % 10));
        tc[3] = (byte)((timecode.Frames / 10 << 4) | (timecode.Frames % 10));

        return tc;
    }

    /// <summary>
    /// Extracts and converts T42 text data to STL format.
    /// Strips odd-parity bits and remaps control codes to STL equivalents.
    /// </summary>
    /// <param name="verbose">Whether to output verbose debug information</param>
    /// <returns>Byte array containing STL-formatted text data (max 112 bytes)</returns>
    private byte[] ExtractSTLTextData(bool verbose)
    {
        // Ensure we have T42 data as the payload
        byte[] t42Data;
        if (Type == Format.T42 && Data.Length >= Constants.T42_LINE_SIZE)
        {
            t42Data = [.. Data.Take(Constants.T42_LINE_SIZE)];
        }
        else if (Type == Format.VBI || Type == Format.VBI_DOUBLE)
        {
            try
            {
                var convertedData = ConvertFormat(Data, Type, Format.T42);
                t42Data = [.. convertedData.Take(Constants.T42_LINE_SIZE)];
            }
            catch
            {
                t42Data = new byte[Constants.T42_LINE_SIZE];
            }
        }
        else
        {
            t42Data = new byte[Constants.T42_LINE_SIZE];
        }

        // Convert T42 to STL text format
        var stlText = new List<byte>();

        // Determine starting position based on row type
        // Row 0 (header): Skip first 10 bytes (2 mag/row + 8 header metadata), parse last 32 bytes
        // Rows 1-24 (captions): Skip first 2 bytes (mag/row), parse remaining 40 bytes
        int startIndex = Row == 0 ? 10 : 2;

        for (int i = startIndex; i < t42Data.Length && stlText.Count < Constants.STL_TEXT_FIELD_SIZE; i++)
        {
            byte b = t42Data[i];

            // Strip parity bit (bit 7) - STL doesn't use odd-parity encoding
            byte stripped = (byte)(b & 0x7F);

            // Remap T42 control codes to STL control codes
            if (stripped == Constants.T42_BLOCK_START_BYTE)
            {
                // T42 Start Box -> STL Start Box
                stlText.Add(Constants.STL_START_BOX);
            }
            else if (stripped == Constants.T42_NORMAL_HEIGHT)
            {
                // T42 End Box (Normal Height) -> STL End Box
                stlText.Add(Constants.STL_END_BOX);
            }
            else if (stripped <= 7)
            {
                // T42 color codes (0-7) map directly to STL alpha color codes
                stlText.Add(stripped);
            }
            else if (stripped >= 0x20 && stripped <= 0x7F)
            {
                // Displayable ASCII characters - keep as is
                stlText.Add(stripped);
            }
            else if (stripped == 0x00)
            {
                // Null byte - treat as space in STL
                stlText.Add(0x20);
            }
            else
            {
                // Other control codes - map to space for now
                stlText.Add(0x20);
            }
        }

        if (verbose) Console.Error.WriteLine($"DEBUG: Extracted STL text data - {stlText.Count} bytes from {t42Data.Length} T42 bytes (Row: {Row}, StartIndex: {startIndex})");

        return [.. stlText];
    }
}