using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.libopx.Formats;

/// <summary>
/// Parser and writer for EBU STL (Subtitle Exchange Format) files with support for conversion from teletext formats.
/// Implements the EBU Tech 3264 specification for subtitle data exchange between broadcast systems.
/// </summary>
public partial class STL : IDisposable
{
    /// <summary>
    /// Gets or sets the input file. If null, reads from stdin.
    /// </summary>
    public FileInfo? InputFile { get; set; } = null;
    /// <summary>
    /// Gets or sets the output file. If null, writes to stdout.
    /// </summary>
    public FileInfo? OutputFile { get; set; } = null;
    private Stream? _outputStream;
    /// <summary>
    /// Gets or sets the input stream for reading STL data.
    /// </summary>
    public required Stream Input { get; set; }
    /// <summary>
    /// Gets the output stream for writing processed data.
    /// </summary>
    public Stream Output => _outputStream ??= OutputFile == null ? Console.OpenStandardOutput() : OutputFile.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
    /// <summary>
    /// Gets or sets the input format. Default is STL.
    /// </summary>
    public Format InputFormat { get; set; } = Format.STL;
    /// <summary>
    /// Gets or sets the output format for processed data. Default is STL.
    /// </summary>
    public Format? OutputFormat { get; set; } = Format.STL;
    /// <summary>
    /// Gets or sets the function mode for processing. Default is Filter.
    /// </summary>
    public Function Function { get; set; } = Function.Filter;
    /// <summary>
    /// Gets or sets the number of lines per frame for timecode incrementation. Default is 2.
    /// </summary>
    public int LineCount { get; set; } = 2;
    /// <summary>
    /// Gets or sets the frame rate for STL timecode calculations. Default is 25fps.
    /// </summary>
    public double FrameRate { get; set; } = 25.0;

    /// <summary>
    /// Valid outputs: t42/vbi/vbi_double/stl
    /// </summary>
    public static readonly Format[] ValidOutputs = [Format.T42, Format.VBI, Format.VBI_DOUBLE, Format.STL];

    /// <summary>
    /// Constructor for STL format from file
    /// </summary>
    /// <param name="inputFile">Path to the input STL file</param>
    /// <exception cref="FileNotFoundException">Thrown if the specified file does not exist</exception>
    [SetsRequiredMembers]
    public STL(string inputFile)
    {
        InputFile = new FileInfo(inputFile);

        if (!InputFile.Exists)
        {
            throw new FileNotFoundException("Input file not found", InputFile.FullName);
        }

        Input = InputFile.OpenRead();
    }

    /// <summary>
    /// Constructor for STL format from stdin
    /// </summary>
    [SetsRequiredMembers]
    public STL()
    {
        InputFile = null;
        Input = Console.OpenStandardInput();
    }

    /// <summary>
    /// Constructor for STL format with custom stream
    /// </summary>
    [SetsRequiredMembers]
    public STL(Stream inputStream)
    {
        InputFile = null;
        Input = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
    }

    /// <summary>
    /// Sets the output file for writing
    /// </summary>
    /// <param name="outputFile">Path to the output file</param>
    public void SetOutput(string outputFile)
    {
        OutputFile = new FileInfo(outputFile);
    }
    
    /// <summary>
    /// Sets the output stream for writing
    /// </summary>
    /// <param name="outputStream">The output stream to write to</param>
    public void SetOutput(Stream outputStream)
    {
        OutputFile = null; // Clear OutputFile since we're using a custom stream
        _outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream), "Output stream cannot be null.");
    }

    /// <summary>
    /// Parses the STL file and returns an enumerable of lines with optional filtering.
    /// </summary>
    /// <param name="magazine">Optional magazine number filter (default: all magazines)</param>
    /// <param name="rows">Optional array of row numbers to filter (default: all rows)</param>
    /// <returns>An enumerable of parsed lines matching the filter criteria</returns>
    public IEnumerable<Line> Parse(int? magazine = null, int[]? rows = null)
    {
        // Use default rows if not specified
        rows ??= Constants.DEFAULT_ROWS;

        // Read STL header first
        var headerBytes = new byte[Constants.STL_HEADER_SIZE];
        var bytesRead = Input.Read(headerBytes, 0, Constants.STL_HEADER_SIZE);
        if (bytesRead < Constants.STL_HEADER_SIZE)
        {
            yield break; // Invalid STL file
        }

        ParseSTLHeader(headerBytes);
        int lineNumber = 0;
        var timecode = new Timecode(0);

        // Read subtitle blocks
        var blockBuffer = new byte[Constants.STL_BLOCK_SIZE];
        while (Input.Read(blockBuffer, 0, Constants.STL_BLOCK_SIZE) == Constants.STL_BLOCK_SIZE)
        {
            // Increment timecode if LineCount is reached
            if (lineNumber % LineCount == 0 && lineNumber != 0)
            {
                timecode = timecode.GetNext();
            }

            var stlBlock = ParseSTLBlock(blockBuffer);
            if (stlBlock == null) continue;

            // Create a Line object for STL data
            var line = new Line()
            {
                LineNumber = lineNumber,
                Data = blockBuffer,
                Length = Constants.STL_BLOCK_SIZE,
                SampleCoding = 0x31, // STL sample coding
                SampleCount = Constants.STL_BLOCK_SIZE,
                LineTimecode = timecode,
                Magazine = stlBlock.Value.subtitleGroup,
                Row = stlBlock.Value.verticalPosition,
                Text = stlBlock.Value.text
            };

            // Apply filtering if specified
            if (magazine.HasValue && line.Magazine != magazine.Value)
            {
                lineNumber++;
                continue;
            }

            if (rows != null && !rows.Contains(line.Row))
            {
                lineNumber++;
                continue;
            }

            yield return line;
            lineNumber++;
        }
    }

    /// <summary>
    /// Writes teletext data as EBU STL format
    /// </summary>
    /// <param name="lines">Enumerable of teletext lines to convert</param>
    /// <param name="programTitle">Title for the STL file header</param>
    public void WriteTeletext(IEnumerable<Line> lines, string programTitle = "Teletext Subtitles")
    {
        // Write STL header
        WriteSTLHeader(programTitle);

        int subtitleNumber = 1;
        foreach (var line in lines)
        {
            if (line.Magazine < 0 || line.Row < 0 || string.IsNullOrEmpty(line.Text?.Trim()))
                continue;

            WriteSTLBlock(subtitleNumber++, line);
        }
    }

    /// <summary>
    /// Disposes the resources used by the STL parser.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _outputStream?.Dispose();
        Input?.Dispose();
    }

    #region Private Methods

    /// <summary>
    /// Parses the STL file header (GSI block)
    /// </summary>
    private static STLHeader ParseSTLHeader(byte[] headerBytes)
    {
        return new STLHeader
        {
            CodePageNumber = Encoding.ASCII.GetString(headerBytes, 0, 3).TrimEnd(),
            DiskFormatCode = Encoding.ASCII.GetString(headerBytes, 3, 8).TrimEnd(),
            DisplayStandardCode = headerBytes[11],
            LanguageCode = headerBytes[12],
            OriginalProgrammeTitle = Encoding.ASCII.GetString(headerBytes, 13, 32).TrimEnd(),
            OriginalEpisodeTitle = Encoding.ASCII.GetString(headerBytes, 45, 32).TrimEnd(),
            TranslatedProgrammeTitle = Encoding.ASCII.GetString(headerBytes, 77, 32).TrimEnd(),
            TranslatedEpisodeTitle = Encoding.ASCII.GetString(headerBytes, 109, 32).TrimEnd(),
            TranslatorsName = Encoding.ASCII.GetString(headerBytes, 141, 32).TrimEnd(),
            TranslatorsContactDetails = Encoding.ASCII.GetString(headerBytes, 173, 32).TrimEnd(),
            SubtitleListReferenceCode = Encoding.ASCII.GetString(headerBytes, 205, 16).TrimEnd(),
            CreationDate = Encoding.ASCII.GetString(headerBytes, 221, 6).TrimEnd(),
            RevisionDate = Encoding.ASCII.GetString(headerBytes, 227, 6).TrimEnd(),
            RevisionNumber = headerBytes[233],
            TotalNumberOfTTIBlocks = BitConverter.ToUInt32(headerBytes, 234),
            TotalNumberOfSubtitles = BitConverter.ToUInt32(headerBytes, 238),
            TotalNumberOfSubtitleGroups = headerBytes[242],
            MaximumNumberOfDisplayableCharactersInAnyTextRow = headerBytes[243],
            MaximumNumberOfDisplayableRows = headerBytes[244],
            TimeCodeStatus = headerBytes[245],
            TimeCodeStartOfProgramme = new TimeCode(headerBytes[246], headerBytes[247], headerBytes[248], headerBytes[249]),
            CountryOfOrigin = Encoding.ASCII.GetString(headerBytes, 250, 3).TrimEnd()
        };
    }

    /// <summary>
    /// Parses an STL subtitle block (TTI block)
    /// </summary>
    private static STLBlock? ParseSTLBlock(byte[] blockBytes)
    {
        // Check if this is a valid subtitle block (not filler)
        if (blockBytes[0] == 0x00) return null;

        var subtitleGroupNumber = blockBytes[0];
        var subtitleNumber = BitConverter.ToUInt16(blockBytes, 1);
        var extensionBlockNumber = blockBytes[3];
        var cumulativeStatus = blockBytes[4];
        var timeCodeIn = new TimeCode(blockBytes[5], blockBytes[6], blockBytes[7], blockBytes[8]);
        var timeCodeOut = new TimeCode(blockBytes[9], blockBytes[10], blockBytes[11], blockBytes[12]);
        var verticalPosition = blockBytes[13];
        var justificationCode = blockBytes[14];
        var commentFlag = blockBytes[15];

        // Extract text field (112 bytes starting at offset 16)
        var textBytes = new byte[112];
        Array.Copy(blockBytes, 16, textBytes, 0, 112);
        var text = ConvertSTLTextToString(textBytes);

        return new STLBlock
        {
            subtitleGroup = subtitleGroupNumber,
            subtitleNumber = subtitleNumber,
            extensionBlock = extensionBlockNumber,
            cumulativeStatus = cumulativeStatus,
            timeCodeIn = timeCodeIn,
            timeCodeOut = timeCodeOut,
            verticalPosition = verticalPosition,
            justificationCode = justificationCode,
            commentFlag = commentFlag,
            text = text
        };
    }

    /// <summary>
    /// Converts STL text field bytes to readable string
    /// </summary>
    private static string ConvertSTLTextToString(byte[] textBytes)
    {
        var result = new StringBuilder();
        
        for (int i = 0; i < textBytes.Length; i++)
        {
            var b = textBytes[i];
            
            // Handle STL control codes
            switch (b)
            {
                case 0x00: // Null - end of text
                    return result.ToString().TrimEnd();
                case 0x8A: // Line break
                    result.Append('\n');
                    break;
                case 0x8F: // End of line
                    break;
                default:
                    if (b >= 0x20 && b <= 0x7F) // Printable ASCII
                    {
                        result.Append((char)b);
                    }
                    else if (b >= 0x80) // Extended characters
                    {
                        // Map to Unicode using teletext charset if available
                        var unicodeChar = TeletextCharsets.GetUnicodeChar("G0", b & 0x7F);
                        if (unicodeChar != '\0')
                        {
                            result.Append(unicodeChar);
                        }
                        else
                        {
                            result.Append(' '); // Fallback to space
                        }
                    }
                    break;
            }
        }
        
        return result.ToString().TrimEnd();
    }

    /// <summary>
    /// Writes the STL file header (GSI block)
    /// </summary>
    private void WriteSTLHeader(string programTitle)
    {
        var header = new byte[Constants.STL_HEADER_SIZE];
        
        // Fill with spaces
        for (int i = 0; i < header.Length; i++)
        {
            header[i] = 0x20;
        }

        // Code Page Number (3 bytes) - must be exactly 3 characters
        var codePageStr = Constants.STL_DEFAULT_CODE_PAGE.PadRight(3).Substring(0, 3);
        var codePageBytes = Encoding.ASCII.GetBytes(codePageStr);
        Array.Copy(codePageBytes, 0, header, 0, 3);

        // Disk Format Code (8 bytes) - must be exactly 8 characters  
        var diskFormatStr = Constants.STL_DEFAULT_DISK_FORMAT.PadRight(8).Substring(0, 8);
        var diskFormatBytes = Encoding.ASCII.GetBytes(diskFormatStr);
        Array.Copy(diskFormatBytes, 0, header, 3, 8);

        // Display Standard Code (1 byte)
        header[11] = Constants.STL_DEFAULT_DISPLAY_STANDARD;

        // Language Code (1 byte)
        header[12] = Constants.STL_DEFAULT_LANGUAGE_CODE;

        // Original Programme Title (32 bytes) - ensure exactly 32 bytes
        var titleStr = programTitle.Length > 32 ? programTitle.Substring(0, 32) : programTitle.PadRight(32);
        var titleBytes = Encoding.ASCII.GetBytes(titleStr);
        Array.Copy(titleBytes, 0, header, 13, 32);

        // Skip other fields that should remain as spaces (bytes 45-220)

        // Creation Date (6 bytes) - YYMMDD format
        var creationDate = DateTime.Now.ToString("yyMMdd");
        var dateBytes = Encoding.ASCII.GetBytes(creationDate);
        Array.Copy(dateBytes, 0, header, 221, 6);

        // Revision Date (6 bytes)
        Array.Copy(dateBytes, 0, header, 227, 6);

        // Revision Number (1 byte) - set to 01
        header[233] = 0x01;

        // Total Number of TTI Blocks (4 bytes) - will be set later, leave as 00000000 for now
        // Total Number of Subtitles (4 bytes) - will be set later, leave as 00000000 for now  
        // Total Number of Subtitle Groups (1 byte) - leave as 00 for now
        
        // Maximum characters per row
        header[243] = Constants.STL_MAX_CHARS_PER_LINE;

        // Maximum displayable rows
        header[244] = Constants.STL_MAX_ROWS;

        // Time Code Status (1 byte) - 1 for available
        header[245] = 0x01;

        // Time Code Start of Programme (4 bytes) - 00:00:00:00
        header[246] = 0x00; // Hours
        header[247] = 0x00; // Minutes  
        header[248] = 0x00; // Seconds
        header[249] = 0x00; // Frames

        // Country of Origin (3 bytes)
        var countryStr = Constants.STL_DEFAULT_COUNTRY.PadRight(3).Substring(0, 3);
        var countryBytes = Encoding.ASCII.GetBytes(countryStr);
        Array.Copy(countryBytes, 0, header, 250, 3);

        Output.Write(header, 0, header.Length);
    }

    /// <summary>
    /// Writes an STL subtitle block (TTI block)
    /// </summary>
    private void WriteSTLBlock(int subtitleNumber, Line line)
    {
        var block = new byte[Constants.STL_BLOCK_SIZE];
        
        // Fill with zeros
        Array.Clear(block, 0, block.Length);

        // Subtitle Group Number (1 byte)
        block[0] = (byte)Math.Max(1, line.Magazine);

        // Subtitle Number (2 bytes, little-endian)
        var subtitleBytes = BitConverter.GetBytes((ushort)subtitleNumber);
        block[1] = subtitleBytes[0];
        block[2] = subtitleBytes[1];

        // Extension Block Number (1 byte)
        block[3] = 0xFF; // Not an extension

        // Cumulative Status (1 byte)
        block[4] = 0x00; // First subtitle in sequence

        // Time Code In/Out (4 bytes each) - for now use line timecode
        if (line.LineTimecode != null)
        {
            var timecodeIn = ConvertToSTLTimeCode(line.LineTimecode);
            var timecodeOut = ConvertToSTLTimeCode(line.LineTimecode.GetNext()); // Default 1 frame duration
            
            block[5] = timecodeIn.Hours;
            block[6] = timecodeIn.Minutes;
            block[7] = timecodeIn.Seconds;
            block[8] = timecodeIn.Frames;
            
            block[9] = timecodeOut.Hours;
            block[10] = timecodeOut.Minutes;
            block[11] = timecodeOut.Seconds;
            block[12] = timecodeOut.Frames;
        }

        // Vertical Position (1 byte)
        block[13] = (byte)Math.Max(1, Math.Min(Constants.STL_MAX_ROWS, line.Row));

        // Justification Code (1 byte) - 0 = unchanged, 1 = left, 2 = center, 3 = right
        block[14] = 0x01; // Left justify

        // Comment Flag (1 byte)
        block[15] = Constants.STL_COMMENT_FLAG;

        // Text Field (112 bytes)
        var textBytes = ConvertStringToSTLText(line.Text, 112);
        Array.Copy(textBytes, 0, block, 16, textBytes.Length);

        Output.Write(block, 0, block.Length);
    }

    /// <summary>
    /// Converts a string to STL text field format
    /// </summary>
    private static byte[] ConvertStringToSTLText(string text, int maxLength)
    {
        var result = new byte[maxLength];
        
        // Fill with 0x8F (as per EBU STL specification)
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = 0x8F;
        }
        
        if (string.IsNullOrEmpty(text))
            return result;

        // Convert ANSI sequences to teletext control bytes
        var cleanText = ConvertAnsiToTeletextBytes(text.Trim());
        
        var textBytes = Encoding.ASCII.GetBytes(cleanText);
        var copyLength = Math.Min(textBytes.Length, maxLength);
        
        Array.Copy(textBytes, 0, result, 0, copyLength);
        
        return result;
    }

    /// <summary>
    /// Converts ANSI escape sequences to equivalent teletext control bytes
    /// </summary>
    private static string ConvertAnsiToTeletextBytes(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var result = new StringBuilder();
        int i = 0;
        
        while (i < text.Length)
        {
            // Look for ANSI escape sequences
            if (i < text.Length - 1 && text[i] == '\x1b' && text[i + 1] == '[')
            {
                var seqStart = i;
                i += 2; // Skip \x1b[
                
                // Find the end of the sequence
                var codes = new StringBuilder();
                while (i < text.Length && (char.IsDigit(text[i]) || text[i] == ';'))
                {
                    codes.Append(text[i]);
                    i++;
                }
                
                if (i < text.Length && text[i] == 'm')
                {
                    // This is a color sequence - convert to teletext byte
                    var teletextByte = ConvertAnsiColorToTeletextByte(codes.ToString());
                    if (teletextByte.HasValue)
                    {
                        result.Append((char)teletextByte.Value);
                    }
                    i++; // Skip the 'm'
                }
                else
                {
                    // Not a color sequence, preserve as-is
                    result.Append(text.Substring(seqStart, i - seqStart + 1));
                    i++;
                }
            }
            else
            {
                // Regular character, keep as-is
                result.Append(text[i]);
                i++;
            }
        }
        
        return result.ToString();
    }

    /// <summary>
    /// Converts ANSI color codes to teletext control bytes
    /// </summary>
    private static byte? ConvertAnsiColorToTeletextByte(string colorCodes)
    {
        var codes = colorCodes.Split(';');
        
        foreach (var code in codes)
        {
            if (int.TryParse(code, out int colorCode))
            {
                return colorCode switch
                {
                    // Foreground colors -> teletext color bytes 0-7
                    30 => 0x00, // Black
                    31 => 0x01, // Red  
                    32 => 0x02, // Green
                    33 => 0x03, // Yellow
                    34 => 0x04, // Blue
                    35 => 0x05, // Magenta
                    36 => 0x06, // Cyan
                    37 => 0x07, // White
                    
                    // Background colors -> teletext background bytes 16-23  
                    40 => 0x10, // Black background
                    41 => 0x11, // Red background
                    42 => 0x12, // Green background
                    43 => 0x13, // Yellow background
                    44 => 0x14, // Blue background
                    45 => 0x15, // Magenta background
                    46 => 0x16, // Cyan background
                    47 => 0x17, // White background
                    
                    _ => null
                };
            }
        }
        
        return null;
    }

    /// <summary>
    /// Converts libopx Timecode to STL TimeCode format with BCD encoding
    /// </summary>
    private TimeCode ConvertToSTLTimeCode(Timecode timecode)
    {
        // Convert frame number to hours:minutes:seconds:frames based on frame rate
        var totalFrames = timecode.FrameNumber;
        var framesPerSecond = (int)Math.Round(FrameRate);
        var framesPerMinute = framesPerSecond * 60;
        var framesPerHour = framesPerMinute * 60;

        var hours = totalFrames / framesPerHour;
        totalFrames %= framesPerHour;
        
        var minutes = totalFrames / framesPerMinute;
        totalFrames %= framesPerMinute;
        
        var seconds = totalFrames / framesPerSecond;
        var frames = totalFrames % framesPerSecond;

        // Convert to BCD format (Binary Coded Decimal)
        var bcdHours = (byte)((hours / 10) * 16 + (hours % 10));
        var bcdMinutes = (byte)((minutes / 10) * 16 + (minutes % 10));
        var bcdSeconds = (byte)((seconds / 10) * 16 + (seconds % 10));
        var bcdFrames = (byte)((frames / 10) * 16 + (frames % 10));

        return new TimeCode(bcdHours, bcdMinutes, bcdSeconds, bcdFrames);
    }

    #endregion

    #region Data Structures

    /// <summary>
    /// Represents the STL file header (GSI block)
    /// </summary>
    private struct STLHeader
    {
        public string CodePageNumber;
        public string DiskFormatCode;
        public byte DisplayStandardCode;
        public byte LanguageCode;
        public string OriginalProgrammeTitle;
        public string OriginalEpisodeTitle;
        public string TranslatedProgrammeTitle;
        public string TranslatedEpisodeTitle;
        public string TranslatorsName;
        public string TranslatorsContactDetails;
        public string SubtitleListReferenceCode;
        public string CreationDate;
        public string RevisionDate;
        public byte RevisionNumber;
        public uint TotalNumberOfTTIBlocks;
        public uint TotalNumberOfSubtitles;
        public byte TotalNumberOfSubtitleGroups;
        public byte MaximumNumberOfDisplayableCharactersInAnyTextRow;
        public byte MaximumNumberOfDisplayableRows;
        public byte TimeCodeStatus;
        public TimeCode TimeCodeStartOfProgramme;
        public string CountryOfOrigin;
    }

    /// <summary>
    /// Represents an STL subtitle block (TTI block)
    /// </summary>
    private struct STLBlock
    {
        public byte subtitleGroup;
        public ushort subtitleNumber;
        public byte extensionBlock;
        public byte cumulativeStatus;
        public TimeCode timeCodeIn;
        public TimeCode timeCodeOut;
        public byte verticalPosition;
        public byte justificationCode;
        public byte commentFlag;
        public string text;
    }

    /// <summary>
    /// Represents an STL timecode
    /// </summary>
    private struct TimeCode
    {
        public byte Hours;
        public byte Minutes;
        public byte Seconds;
        public byte Frames;

        public TimeCode(byte hours, byte minutes, byte seconds, byte frames)
        {
            Hours = hours;
            Minutes = minutes;
            Seconds = seconds;
            Frames = frames;
        }
    }

    #endregion
}