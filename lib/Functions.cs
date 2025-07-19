using System.Collections;

namespace nathanbutlerDEV.libopx;

public class Functions
{
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
