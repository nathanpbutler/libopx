using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace nathanbutlerDEV.libopx.Formats;

public class MXF : IDisposable
{
    public required FileInfo File { get; set; }
    public required Stream Input { get; set; }
    public Timecode StartTimecode { get; set; } = new Timecode(0); // Start timecode of the MXF file
    public List<Timecode> SMPTETimecodes { get; set; } = []; // List of SMPTE timecodes per-frame in the MXF file
    public bool CheckSequential { get; set; } = true; // Check if SMPTE timecodes are sequential

    [SetsRequiredMembers]
    public MXF(string inputFile)
    {
        File = new FileInfo(inputFile);

        if (!File.Exists)
        {
            throw new FileNotFoundException("The specified MXF file does not exist.", inputFile);
        }

        Input = File.OpenRead();

        if (!IsValidMXFFile())
        {
            throw new InvalidDataException("The specified file is not a valid MXF file.");
        }

        if (!GetStartTimecode())
        {
            throw new InvalidOperationException("Failed to retrieve start timecode from the MXF file.");
        }
        
        Input.Seek(0, SeekOrigin.Begin); // Reset stream position to the beginning
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Input?.Close();
        Input?.Dispose();
    }

    // Check method to verify if the file is a valid MXF file (read first 4 bytes for 0x06, 0x0E, 0x2B, 0x34)
    private bool IsValidMXFFile()
    {
        using var stream = File.OpenRead();
        byte[] header = new byte[4];
        if (stream.Read(header, 0, header.Length) == header.Length)
        {
            // If the first 4 bytes match the FourCC key for MXF
            return header.SequenceEqual(Keys.FourCc);
        }
        return false;
    }

    // GetStartTimecode method to retrieve the start timecode from the MXF file
    public bool GetStartTimecode()
    {
        Input.Seek(0, SeekOrigin.Begin); // Reset stream position to the beginning
        while (Input.Position < 128000)
        {
            var keyBuffer = new byte[16];
            var keyBytesRead = Input.Read(keyBuffer, 0, 16);
            if (keyBytesRead != 16) break; // End of stream

            var isTargetKey = Keys.GetKeyType(keyBuffer) == KeyType.TimecodeComponent;

            // Step 2: Read BER Length
            var length = ReadBerLengthAsync(Input).Result;
            if (length < 0) break; // Invalid or end of stream

            // Step 3: Handle Value based on Key match
            if (isTargetKey)
            {
                // Step 4: Read Data
                var data = new byte[length];
                var dataBytesRead = Input.Read(data, 0, length);
                if (dataBytesRead != length) break; // End of stream or invalid data

                // Step 5: Parse Data
                var timecodeComponent = TimecodeComponent.Parse(data);

                StartTimecode = new Timecode(timecodeComponent.StartTimecode, timecodeComponent.RoundedTimecodeTimebase, timecodeComponent.DropFrame);
                return true; // Successfully retrieved start timecode
            }
            else
            {
                // Skip the data for non-target keys
                Input.Seek(length, SeekOrigin.Current);
            }
        }
        Input.Seek(0, SeekOrigin.Begin); // Reset stream position to the beginning
        StartTimecode = new Timecode(0); // Reset start timecode if not found
        return false; // Return an integer status code, e.g., 0 for success
    }

    /// <summary>
    /// Parses SMPTE timecodes from the MXF file.
    /// </summary>
    /// <returns>A list of SMPTE timecodes.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no SMPTE timecodes are found.</exception>
    public async Task<int> ParseSMPTETimecodesAsync()
    {
        var keyBuffer = new byte[16];
        while (await ReadExactAsync(Input, keyBuffer, 16) == 16)
        {
            var isTargetKey = Keys.GetKeyType(keyBuffer) == KeyType.System;

            // Step 2: Read BER Length
            var length = await ReadBerLengthAsync(Input);
            if (length < 0) break; // Invalid or end of stream

            if (isTargetKey)
            {
                var data = ArrayPool<byte>.Shared.Rent(length);
                try
                {
                    var dataRead = await ReadExactAsync(Input, data, length);

                    if (dataRead == length)
                    {
                        var smpteBytes = data.AsSpan(41, 4);
                        var smpte = Timecode.FromBytes(smpteBytes.ToArray(), StartTimecode.Timebase, StartTimecode.DropFrame);
                        if (CheckSequential && SMPTETimecodes.Count > 0)
                        {
                            // If the previous timecode is not sequential, throw an error
                            if (SMPTETimecodes[^1] != smpte.GetPrevious())
                            {
                                throw new Exception($"SMPTE timecodes are not sequential: {SMPTETimecodes[^1]} != {smpte.GetPrevious()}");
                            }
                        }
                        SMPTETimecodes.Add(smpte);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(data);
                }
            }
            else
            {
                Input.Seek(length, SeekOrigin.Current);
            }
        }
        Input.Seek(0, SeekOrigin.Begin); // Reset stream position to the beginning
        if (SMPTETimecodes.Count == 0)
        {
            throw new InvalidOperationException("No SMPTE timecodes found in the MXF file.");
        }
        return SMPTETimecodes.Count; // Return the count of parsed SMPTE timecodes
    }

    private static async Task<int> ReadExactAsync(Stream input, byte[] buffer, int count)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var bytesRead = await input.ReadAsync(buffer.AsMemory(totalRead, count - totalRead));
            if (bytesRead == 0) break; // End of stream
            totalRead += bytesRead;
        }
        return totalRead;
    }

    private static async Task<int> ReadBerLengthAsync(Stream input)
    {
        // Read first byte to determine BER encoding type
        var lengthByte = new byte[1];
        if (await ReadExactAsync(input, lengthByte, 1) != 1) return -1;

        if ((lengthByte[0] & 0x80) == 0)
        {
            // Short form: length is just this byte
            return lengthByte[0];
        }
        else
        {
            // Long form: first byte tells us how many additional bytes
            var byteCount = lengthByte[0] & 0x7F;
            if (byteCount > 8) return -1; // Sanity check

            var encodedBytes = new byte[byteCount];
            if (await ReadExactAsync(input, encodedBytes, byteCount) != byteCount) return -1;

            var length = 0;
            for (var i = 0; i < byteCount; i++)
            {
                length = (length << 8) | encodedBytes[i];
            }

            return length;
        }
    }
}
