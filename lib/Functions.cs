using System.Collections;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;

namespace nathanbutlerDEV.libopx;

public class Functions
{
    #region General Functions

    /// <summary>
    /// Filter function to process input files based on specified parameters.
    /// </summary>
    /// <param name="inputFile">The input file to process, or null to read from stdin.</param>
    /// <param name="magazine">The magazine number to filter by.</param>
    /// <param name="rows">The number of rows to filter by.</param>
    /// <param name="lineCount">The number of lines per frame for timecode incrementation.</param>
    /// <param name="inputFormat">The input format to use (e.g., BIN, VBI, T42).</param>
    /// <param name="verbose">Whether to enable verbose output.</param>
    /// <returns>An integer indicating the result of the filtering operation.</returns>
    /// <remarks>
    /// This function reads the specified input file or stdin, processes it according to the provided parameters,
    /// and outputs the filtered lines to stdout. The input format determines how the data is parsed and processed.
    /// Supported formats include BIN, VBI, VBI_DOUBLE, and T42.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown if an unsupported input format is specified.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the specified input file does not exist.</exception>
    /// <exception cref="IOException">Thrown if there is an error reading the input file or stdin.</exception>
    public static int Filter(FileInfo? input, int magazine, int[] rows, int lineCount, Format inputFormat, bool verbose)
    {
        try
        {
            if (verbose)
            {
                if (input != null && input.Exists)
                    Console.WriteLine($"  Input file: {input.FullName}");
                else
                    Console.WriteLine("Reading from stdin");
                Console.WriteLine($"    Magazine: {magazine}");
                Console.WriteLine($"        Rows: [{string.Join(", ", rows)}]");
                Console.WriteLine($"Input format: {inputFormat}");
                Console.WriteLine($"  Line count: {lineCount}");
            }

            switch (inputFormat)
            {
                case Format.BIN:
                    var bin = input is FileInfo inputBIN && inputBIN.Exists
                        ? new BIN(inputBIN.FullName)
                        : new BIN(Console.OpenStandardInput());
                    foreach (var line in bin.Parse(magazine, rows))
                    {
                        Console.WriteLine(line);
                    }
                    return 0;
                case Format.VBI:
                case Format.VBI_DOUBLE:
                    var vbi = input is FileInfo inputVBI && inputVBI.Exists
                        ? new VBI(inputVBI.FullName)
                        : new VBI(Console.OpenStandardInput());
                    vbi.LineCount = lineCount;
                    foreach (var line in vbi.Parse(magazine, rows))
                    {
                        Console.WriteLine(line);
                    }
                    return 0;
                case Format.T42:
                    var t42 = input is FileInfo inputT42 && inputT42.Exists
                        ? new T42(inputT42.FullName)
                        : new T42(Console.OpenStandardInput());
                    t42.LineCount = lineCount;
                    foreach (var line in t42.Parse(magazine, rows))
                    {
                        Console.WriteLine(line);
                    }
                    return 0;
                case Format.MXF:
                // Only Filter if file exists, otherwise return 1
                    if (input is FileInfo inputMXF && inputMXF.Exists)
                    {
                        // Implement MXF processing logic
                        var mxf = new MXF(inputMXF.FullName)
                        {
                            Function = Function.Filter, // Set function to Filter
                            Verbose = verbose
                        };
                        mxf.AddRequiredKey(KeyType.Data); // Add Data key to process data packets
                        foreach (var packet in mxf.Parse(magazine, rows))
                        {
                            if (verbose) Console.WriteLine($"Debug: Found packet with {packet.Lines.Count} lines");
                            Console.WriteLine(packet);
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("Error: Input file does not exist or is not specified for MXF format.");
                        return 1;
                    }
                    return 0;
                default:
                    Console.WriteLine($"Unsupported input format: {inputFormat}");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    public static Format ParseInputFormat(string format)
    {
        return format.TrimStart('.').ToLowerInvariant() switch
        {
            "bin" => Format.BIN,
            "vbi" => Format.VBI,
            "vbid" => Format.VBI_DOUBLE,
            "t42" => Format.T42,
            "mxf" => Format.MXF,
            "rcwt" => Format.RCWT,
            _ => Format.VBI // Default to VBI if unknown format
        };
    }
    
    /// <summary>
    /// Extracts essence from an MXF file based on specified parameters.
    /// </summary>
    /// <param name="inputFile">The MXF file to extract essence from.</param>
    /// <param name="outputBasePath">The base path for output files. If null, defaults to the input file's path without extension.</param>
    /// <param name="keyString">A string of keys to filter the essence. If null or empty, defaults to extracting all essence.</param>
    /// <param name="demuxMode">If true, extracts all keys in demux mode. If false, extracts only specified keys.</param>
    /// <param name="useNames">If true, uses key names instead of hex keys for output files.</param>
    /// <param name="klvMode">If true, includes key and length bytes in output files.</param>
    /// <param name="verbose">If true, enables verbose output during processing.</param>
    /// <returns>An integer indicating the result of the extraction operation (0 for success, 1 for failure).</returns>
    public static int Extract(FileInfo inputFile, string? outputBasePath, string? keyString, bool demuxMode, bool useNames, bool klvMode, bool verbose)
    {
        outputBasePath ??= Path.ChangeExtension(inputFile.FullName, null);

        if (!string.IsNullOrEmpty(outputBasePath))
        {
            Console.WriteLine($"Output base path specified: {outputBasePath}");
        }

        try
        {
            using var mxf = new MXF(inputFile.FullName);

            // Configure extraction settings
            mxf.OutputBasePath = outputBasePath;
            mxf.DemuxMode = demuxMode;
            mxf.UseKeyNames = useNames && demuxMode; // Only use names in demux mode
            mxf.KlvMode = klvMode;

            // Parse keys if specified
            if (!demuxMode && !string.IsNullOrEmpty(keyString))
            {
                var targetKeys = ParseKeys(keyString, verbose);
                if (targetKeys.Count > 0)
                {
                    mxf.ClearRequiredKeys();
                    foreach (var key in targetKeys)
                    {
                        mxf.AddRequiredKey(key);
                    }
                }
            }
            else if (!demuxMode)
            {
                // Default to Data if no keys specified
                Console.WriteLine("No keys specified, defaulting to Data.");
                mxf.ClearRequiredKeys();
                mxf.AddRequiredKey(KeyType.Data);
            }

            // Print active modes
            if (klvMode)
            {
                Console.WriteLine("KLV mode enabled - key and length bytes will be included in output files.");
            }

            if (demuxMode)
            {
                Console.WriteLine("Demux mode enabled - all keys will be extracted.");
                if (mxf.UseKeyNames)
                {
                    Console.WriteLine("Name mode enabled - using Key/Essence names instead of hex keys.");
                }
                else
                {
                    Console.WriteLine("Using hex key names for output files.");
                }
            }

            Console.WriteLine($"Processing MXF file: {inputFile.FullName}");

            // Extract the essence
            mxf.ExtractEssence();

            Console.WriteLine($"Finished processing MXF file: {inputFile.FullName}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing file: {ex.Message}");
            return 1;
        }
    }

    private static List<KeyType> ParseKeys(string arg, bool verbose = false)
    {
        var keys = new List<KeyType>();
        var keyStrings = arg.Split(',').Select(k => k.Trim().ToLowerInvariant());
        foreach (var keyString in keyStrings)
        {
            switch (keyString)
            {
                case "d":
                    keys.Add(KeyType.Data);
                    if (verbose) Console.WriteLine("Data key specified.");
                    break;
                case "v":
                    keys.Add(KeyType.Video);
                    if (verbose) Console.WriteLine("Video key specified.");
                    break;
                case "s":
                    keys.Add(KeyType.System);
                    if (verbose) Console.WriteLine("System key specified.");
                    break;
                case "t":
                    keys.Add(KeyType.TimecodeComponent);
                    if (verbose) Console.WriteLine("TimecodeComponent key specified.");
                    break;
                case "a":
                    keys.Add(KeyType.Audio);
                    if (verbose) Console.WriteLine("Audio key specified.");
                    break;
                default:
                    Console.WriteLine($"Unknown key type: {keyString}");
                    break;
            }
        }
        return keys;
    }

    #endregion

    #region VBI
    /// <summary>
    /// Double the length of a byte array
    /// </summary>
    /// <param name="bytes">The byte array to double.</param>
    /// <returns>The doubled byte array.</returns>
    public static byte[] Double(byte[] bytes)
    {
        // Create a new byte array with double the length of the input
        var doubled = new byte[bytes.Length * 2];
        // For each byte in the input...
        for (var i = 0; i < bytes.Length; i++)
        {
            // If the byte is not the last byte in the input...
            if (i != bytes.Length - 1)
            {
                // Set the byte at i * 2 to the byte at i
                doubled[i * 2] = bytes[i];
                // Set the byte at i * 2 + 1 to the average of the byte at i and the byte at i + 1
                doubled[i * 2 + 1] = (byte)Math.Floor((bytes[i] + bytes[i + 1]) / 2.0);
            }
            // Otherwise if the byte is the last byte in the input...
            else
            {
                // Set the byte at i * 2 to the byte at i
                doubled[i * 2] = bytes[i];
                // Set the byte at i * 2 + 1 to the byte at i
                doubled[i * 2 + 1] = bytes[i];
            }
        }
        // Return the doubled bytes
        return doubled;
    }

    /// <summary>
    /// Normalise a byte array
    /// </summary>
    /// <param name="line">The byte array to normalise.</param>
    /// <returns>The normalised byte array.</returns>
    public static float[] Normalise(byte[] line)
    {
        // Get minimum and maximum values from line
        var min = line.Min();
        var max = line.Max();

        // Get range
        float range = max - min;

        // If range is 0, set range to 1
        if (range == 0)
        {
            range = 1;
        }

        // Create a new float array with the same length as the line
        var normalised = new float[line.Length];

        // For each byte in the line...
        for (var i = 0; i < line.Length; i++)
        {
            // Normalise the byte and set it in the normalised array
            normalised[i] = (line[i] - min) / range;
        }

        // Return the normalised line
        return normalised;
    }

    /// <summary>
    /// Get the bits from a normalised byte array
    /// </summary>
    /// <param name="normalised">The normalised byte array to get the bits from.</param>
    /// <param name="threshold">The threshold for bit detection (default 0.40f)</param>
    /// <returns>The bits from the normalised byte array.</returns>
    public static BitArray GetBits(float[] normalised, float threshold = Constants.VBI_DEFAULT_THRESHOLD)
    {
        // Create a new BitArray with the same length as the normalised line
        BitArray bits = new(normalised.Length + 16);
        // For each byte in the normalised line...
        for (var i = 0; i < normalised.Length; i++)
        {
            // Set the bit in the BitArray to true if the normalised byte is greater than or equal to the threshold
            // Default threshold of 0.40 has so far produced the best results
            bits[i] = normalised[i] >= threshold;
        }
        // Return the BitArray
        return bits;
    }

    /// <summary>
    /// Check if a byte has odd parity
    /// </summary>
    /// <param name="value">The byte to check.</param>
    /// <returns>True if the byte has odd parity, false otherwise.</returns>
    public static bool HasOddParity(byte value)
    {
        // Set count to 0
        var count = 0;

        // For each bit in the byte...
        for (var i = 0; i < 8; i++)
        {
            // If the bit is 1, increment the count
            if ((value & (1 << i)) != 0)
            {
                count++;
            }
        }

        // Return true if the count of '1' bits is odd
        return (count % 2) != 0;
    }

    /// <summary>
    /// Get a byte from a bit array
    /// </summary>
    /// <param name="bits">The bit array to get the byte from.</param>
    /// <param name="offset">The offset to get the byte from.</param>
    /// <param name="dataBits">Whether to use data bits.</param>
    /// <returns>The byte from the bit array.</returns>
    public static byte GetByte(BitArray bits, int offset, bool dataBits)
    {
        // Create a new bit array with 8 bits
        BitArray b = new(8);

        // If the offset is negative or greater than the length of the bits, return 0
        if (offset < 0 || offset + 28 > bits.Length)
        {
            return 0;
        }

        // Collect bits from the offset (Every 4 bytes has so far produced the best results)
        b[0] = bits[offset + 0];
        b[1] = bits[offset + 4];
        b[2] = bits[offset + 8];
        b[3] = bits[offset + 12];
        b[4] = bits[offset + 16];
        b[5] = bits[offset + 20];
        b[6] = bits[offset + 24];
        b[7] = bits[offset + 28];

        // Convert the bit array to a byte
        var byteArr = new byte[1];
        b.CopyTo(byteArr, 0);

        // If the parity is even, flip the MSB to ensure odd parity
        if (dataBits && !HasOddParity(byteArr[0]))
        {
            byteArr[0] ^= Constants.VBI_PARITY_FLIP_MASK; // XOR with 0x80 to flip the MSB
        }

        // Return the modified byte
        return byteArr[0];
    }

    /// <summary>
    /// Find the clock run-in and framing code from a byte array.
    /// </summary>
    /// <param name="bytes">The byte array to search.</param>
    /// <returns>The offset of the clock run-in and framing code. If not found, returns -1.</returns>
    public static int GetCrifc(byte[] bytes)
    {
        for (var i = 0; i < bytes.Length - 2; i++)
        {
            if (bytes[i] == Constants.T42_CLOCK_BYTE && bytes[i + 1] == Constants.T42_CLOCK_BYTE && bytes[i + 2] == Constants.T42_FRAMING_CODE)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Find the clock run-in and framing code from a span of bytes.
    /// </summary>
    /// <param name="bytes">The span of bytes to search.</param>
    /// <returns>The offset of the clock run-in and framing code. If not found, returns -1.</returns>
    public static int GetCrifc(ReadOnlySpan<byte> bytes)
    {
        for (var i = 0; i < bytes.Length - 2; i++)
        {
            if (bytes[i] == Constants.T42_CLOCK_BYTE && bytes[i + 1] == Constants.T42_CLOCK_BYTE && bytes[i + 2] == Constants.T42_FRAMING_CODE)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Get the offset of the line
    /// </summary>
    /// <param name="bits">The bits to get the offset from.</param>
    /// <returns>The offset of the line.</returns>
    public static int GetOffset(BitArray bits)
    {
        // Set lineOffset to -1
        var lineOffset = -1;
        // Create byte array to store bytes
        var byteArray = new byte[4];
        // For each offset from 0 to MAX_OFFSET_SEARCH...
        for (var o = 0; o < Constants.VBI_MAX_OFFSET_SEARCH; o++)
        {
            // Look for clock (0x55) and framing code (0x27) at specific offsets
            // Clock:
            byteArray[0] = GetByte(bits, o, false);
            byteArray[1] = GetByte(bits, o + Constants.VBI_CLOCK_OFFSET_1, false);
            // Framing Code:
            byteArray[2] = GetByte(bits, o + Constants.VBI_FRAMING_OFFSET_1, false);
            byteArray[3] = GetByte(bits, o + Constants.VBI_FRAMING_OFFSET_2, false);

            // If clock is 0x55 and framing code is 0x27, continue
            if (byteArray[0] != Constants.T42_CLOCK_BYTE || byteArray[1] != Constants.T42_CLOCK_BYTE || (byteArray[2] != Constants.T42_FRAMING_CODE && byteArray[3] != Constants.T42_FRAMING_CODE)) continue;

            // If framing code found at offset 39, return offset + 39
            if (byteArray[2] == Constants.T42_FRAMING_CODE)
            {
                lineOffset = o + Constants.VBI_FRAMING_OFFSET_1;
            }

            // Otherwise if framing code found at offset 40, return offset + 40
            else if (byteArray[3] == Constants.T42_FRAMING_CODE)
            {
                lineOffset = o + Constants.VBI_FRAMING_OFFSET_2;
            }
        }

        // Return the offset
        return lineOffset;
    }

    #endregion

    #region BIN
    
    /// <summary>
    /// Get the count of the bytes
    /// </summary>
    /// <param name="bytes">The byte array to get the count of.</param>
    /// <returns>The count of the bytes.</returns>
    public static int GetCount(byte[] bytes)
    {
        // Return the first byte shifted left by 8 bits OR the second byte
        return bytes[0] << 8 | bytes[1];
    }
    
    #endregion
}
