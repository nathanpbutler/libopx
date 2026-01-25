using System;
using System.Runtime.CompilerServices;

namespace nathanbutlerDEV.libopx;

/// <summary>
/// Represents SMPTE timecode with support for various frame rates, drop frame mode, and frame calculations.
/// Provides arithmetic operations and conversion between different timecode representations.
/// </summary>
public class Timecode : IEquatable<Timecode>, IComparable<Timecode>
{
    /// <summary>
    /// The hours component of the timecode
    /// </summary>
    public int Hours { get; private set; }
    /// <summary>
    /// The minutes component of the timecode
    /// </summary>
    public int Minutes { get; private set; }
    /// <summary>
    /// The seconds component of the timecode
    /// </summary>
    public int Seconds { get; private set; }
    /// <summary>
    /// The frames component of the timecode
    /// </summary>
    public int Frames { get; private set; }
    /// <summary>
    /// The field component of the timecode - Determined automatically based on the timebase
    /// </summary>
    public int Field => Timebase is 48 or 50 or 60 ? Frames % 2 : 0;
    /// <summary>
    /// The timebase of the timecode
    /// </summary>
    public int Timebase { get; private set; }
    /// <summary>
    /// The frame rate of the timecode
    /// </summary>
    public int FrameRate => Timebase switch
    {
        48 => 24,
        50 => 25,
        60 => 30,
        _ => Timebase
    };

    /// <summary>
    /// Whether the timecode is a drop frame timecode
    /// </summary>
    public bool DropFrame { get; private set; }
    /// <summary>
    /// The maximum number of frames in the timecode
    /// </summary>
    public int MaxFrames => Timebase switch
    {
        24 => 2073600,
        48 => 4147200,
        50 => 4320000,
        60 => DropFrame ? 5178816 : 5184000,
        30 => DropFrame ? 2589408 : 2592000,
        _ => 2160000
    };

    /// <summary>
    /// Gets the total frame number since the start of the day, accounting for drop frame calculations.
    /// </summary>
    public long FrameNumber
    {
        get
        {
            // Start with basic frame calculation
            var totalSeconds = Hours * 3600 + Minutes * 60 + Seconds;
            var totalFrames = (long)totalSeconds * Timebase + Frames;

            // Handle drop frame for 30p and 60p
            if (DropFrame && Timebase is 30 or 60)
            {
                var totalMinutes = Hours * 60 + Minutes;
                var dropFramesPerMinute = Timebase == 30 ? 2 : 4;
                var dropFrames = totalMinutes / 10 * 9 * dropFramesPerMinute;
                
                // Add drop frames for non-tenth minutes
                if (totalMinutes % 10 != 0)
                {
                    dropFrames += totalMinutes % 10 * dropFramesPerMinute;
                }
                
                totalFrames -= dropFrames;
            }

            return totalFrames % MaxFrames;
        }
        set
        {
            var totalFrames = value % MaxFrames;
            if (totalFrames < 0) totalFrames += MaxFrames;

            // Handle drop frame adjustment - add back the dropped frames for display timecode
            if (DropFrame && Timebase is 30 or 60)
            {
                var dropFramesPerMinute = Timebase == 30 ? 2 : 4;
                var framesPerMinute = Timebase * 60;
                var framesPerTenMinutes = Timebase * 600;

                // Calculate how many 10-minute blocks
                var tenMinuteBlocks = totalFrames / (framesPerTenMinutes - 9 * dropFramesPerMinute);
                var remainingFrames = totalFrames % (framesPerTenMinutes - 9 * dropFramesPerMinute);

                // Add back the drop frames for the 10-minute blocks
                totalFrames += tenMinuteBlocks * 9 * dropFramesPerMinute;

                // Handle remaining minutes (each loses dropFramesPerMinute except the first)
                if (remainingFrames >= framesPerMinute)
                {
                    var additionalMinutes = (remainingFrames - framesPerMinute) / (framesPerMinute - dropFramesPerMinute) + 1;
                    if (additionalMinutes > 9) additionalMinutes = 9;
                    totalFrames += additionalMinutes * dropFramesPerMinute;
                }
            }

            // Convert back to H:M:S:F
            var totalSeconds = (int)(totalFrames / Timebase);
            var frames = (int)(totalFrames % Timebase);

            var hours = totalSeconds / 3600;
            var minutes = totalSeconds % 3600 / 60;
            var seconds = totalSeconds % 60;

            Hours = hours % 24;  // Ensure hours wrap at 24
            Minutes = minutes;
            Seconds = seconds;
            Frames = frames;
            // Field is now automatically calculated as Frames % 2 for progressive formats
        }
    }

    /// <summary>
    /// Initializes a new instance of the Timecode class with default values (00:00:00:00 at 25fps).
    /// </summary>
    public Timecode()
    {
        Timebase = 25;
    }

    /// <summary>
    /// Initializes a new instance of the Timecode class with the specified time components.
    /// </summary>
    /// <param name="hours">The hours component of the timecode (0-23)</param>
    /// <param name="minutes">The minutes component of the timecode (0-59)</param>
    /// <param name="seconds">The seconds component of the timecode (0-59)</param>
    /// <param name="frames">The frames component of the timecode (0 to timebase-1)</param>
    /// <param name="timebase">The frame rate timebase (24, 25, 30, 48, 50, or 60)</param>
    /// <param name="dropFrame">Whether to use drop frame mode (only valid for 30 and 60 fps)</param>
    public Timecode(int hours, int minutes, int seconds, int frames, int timebase = 25, bool dropFrame = false)
    {
        ValidateTimebase(timebase);
        if (hours < 0 || hours >= 24) throw new ArgumentException("Hours must be between 0 and 23", nameof(hours));
        if (minutes < 0 || minutes >= 60) throw new ArgumentException("Minutes must be between 0 and 59", nameof(minutes));
        if (seconds < 0 || seconds >= 60) throw new ArgumentException("Seconds must be between 0 and 59", nameof(seconds));
        if (frames < 0 || frames >= timebase) throw new ArgumentException($"Frames must be between 0 and {timebase - 1}", nameof(frames));
        if (dropFrame && timebase is not 30 and not 60) throw new ArgumentException("Drop frame is only supported for 30 and 60 timebases", nameof(dropFrame));

        Hours = hours;
        Minutes = minutes;
        Seconds = seconds;
        Frames = frames;
        DropFrame = dropFrame;
        Timebase = timebase;
    }

    /// <summary>
    /// Initializes a new instance of the Timecode class from a string representation.
    /// </summary>
    /// <param name="timecode">The timecode as a string in HH:MM:SS:FF or HH:MM:SS;FF format</param>
    /// <param name="timebase">The frame rate timebase (24, 25, 30, 48, 50, or 60)</param>
    /// <param name="dropFrame">Whether to use drop frame mode (only valid for 30 and 60 fps)</param>
    public Timecode(string timecode, int timebase = 25, bool dropFrame = false)
    {
        ValidateTimebase(timebase);

        string[] parts;
        if (timecode.Contains(';'))
        {
            parts = timecode.Replace(';', ':').Split(':');
            DropFrame = true; // Force drop frame for timecodes with semicolons
        }
        else
        {
            parts = timecode.Split(':');
            DropFrame = dropFrame;
        }
        if (parts.Length != 4) throw new ArgumentException("Timecode must be in the format HH:MM:SS:FF or HH:MM:SS;FF", nameof(timecode));
        if (dropFrame && timebase is not 30 and not 60) throw new ArgumentException("Drop frame is only supported for 30 and 60 timebases", nameof(dropFrame));

        Hours = int.Parse(parts[0]);
        Minutes = int.Parse(parts[1]);
        Seconds = int.Parse(parts[2]);
        Frames = int.Parse(parts[3]);

        if (Frames < 0 || Frames >= timebase) throw new ArgumentException($"Frames must be between 0 and {timebase - 1}", nameof(timecode));
        Timebase = timebase;
    }

    /// <summary>
    /// Initializes a new instance of the Timecode class from a total frame count.
    /// </summary>
    /// <param name="totalFrames">The total number of frames since start of day</param>
    /// <param name="timebase">The frame rate timebase (24, 25, 30, 48, 50, or 60)</param>
    /// <param name="dropFrame">Whether to use drop frame mode (only valid for 30 and 60 fps)</param>
    /// <exception cref="ArgumentException">Thrown when the timebase is not 30 or 60 and dropFrame is true</exception>
    /// <exception cref="ArgumentException">Thrown when the frames are not between 0 and the timebase</exception>
    /// <exception cref="ArgumentException">Thrown when the total frames are not between 0 and the maximum number of frames</exception>
    public Timecode(int totalFrames, int timebase = 25, bool dropFrame = false)
    {
        ValidateTimebase(timebase);
        if (dropFrame && timebase is not 30 and not 60) throw new ArgumentException("Drop frame is only supported for 30 and 60 timebases", nameof(dropFrame));

        // Set properties BEFORE using MaxFrames to ensure correct calculation
        Timebase = timebase;
        DropFrame = dropFrame;

        if (totalFrames > MaxFrames)
        {
            totalFrames %= MaxFrames;
        }

        // Handle negative frame counts
        if (totalFrames < 0) totalFrames += MaxFrames;

        // For drop frame, we need to add back the dropped frames to get display timecode
        if (dropFrame && timebase is 30 or 60)
        {
            var dropFramesPerMinute = timebase == 30 ? 2 : 4;
            var framesPerMinute = timebase * 60;
            var framesPerTenMinutes = timebase * 600;

            // Calculate how many 10-minute blocks
            var tenMinuteBlocks = totalFrames / (framesPerTenMinutes - 9 * dropFramesPerMinute);
            var remainingFrames = totalFrames % (framesPerTenMinutes - 9 * dropFramesPerMinute);

            // Add back the drop frames for the 10-minute blocks
            totalFrames += tenMinuteBlocks * 9 * dropFramesPerMinute;

            // Handle remaining minutes (each loses dropFramesPerMinute except the first)
            if (remainingFrames >= framesPerMinute)
            {
                var additionalMinutes = (remainingFrames - framesPerMinute) / (framesPerMinute - dropFramesPerMinute) + 1;
                if (additionalMinutes > 9) additionalMinutes = 9;
                totalFrames += additionalMinutes * dropFramesPerMinute;
            }
        }

        // Convert total frames to H:M:S:F using simple arithmetic
        var totalSeconds = totalFrames / timebase;
        var frames = totalFrames % timebase;

        var hours = totalSeconds / 3600;
        var minutes = totalSeconds % 3600 / 60;
        var seconds = totalSeconds % 60;

        Hours = hours % 24;
        Minutes = minutes;
        Seconds = seconds;
        Frames = frames;
    }

    /// <summary>
    /// Create a Timecode from SMPTE bytes
    /// </summary>
    /// <param name="bytes">4-byte array containing SMPTE timecode</param>
    /// <param name="timebase">The timebase of the timecode</param>
    /// <param name="dropFrame">Whether the timecode is a drop frame timecode</param>
    public static Timecode FromBytes(byte[] bytes, int timebase = 25, bool dropFrame = false)
    {
        return FromBytes(bytes.AsSpan(), timebase, dropFrame);
    }

    /// <summary>
    /// Create a Timecode from SMPTE bytes (more efficient span version)
    /// </summary>
    /// <param name="bytes">4-byte span containing SMPTE timecode</param>
    /// <param name="timebase">The timebase of the timecode</param>
    /// <param name="dropFrame">Whether the timecode is a drop frame timecode</param>
    public static Timecode FromBytes(ReadOnlySpan<byte> bytes, int timebase = 25, bool dropFrame = false)
    {
        ValidateTimebase(timebase);
        if (bytes.Length != 4)
            throw new ArgumentException("Byte span must be exactly 4 bytes long", nameof(bytes));

        var hours = bytes[3];
        var minutes = bytes[2];
        var seconds = bytes[1];
        var frames = bytes[0];
        var extractedField = 0;

        // MXF System Stream field encoding for progressive formats:
        // 48p: 0x80 bit in seconds byte indicates field 1
        // 50p/60p: 0x80 bit in hours byte indicates field 1
        // Note: 50p and 60p share the same bit position; caller must know format from context
        if ((hours & 0x80) == 0x80)
        {
            extractedField = 1;
            hours -= 128;
        }

        if ((seconds & 0x80) == 0x80)
        {
            extractedField = 1;
            seconds -= 128;
        }

        // Handle drop frame offset for 30/60
        if (dropFrame && (timebase is 30 or 60))
        {
            frames -= 64;
        }

        // Convert BCD (Binary Coded Decimal) to integers efficiently
        var parsedFrames = BcdToInt(frames);
        
        // For progressive formats, multiply by 2 (inverse of the /2 in ToBytes) and add field
        if (timebase is 48 or 50 or 60)
        {
            parsedFrames = parsedFrames * 2 + extractedField;
        }

        // Create the timecode with the adjusted frames value
        var smpte = new Timecode
        {
            Hours = BcdToInt(hours),
            Minutes = BcdToInt(minutes),
            Seconds = BcdToInt(seconds),
            Frames = parsedFrames,
            Timebase = timebase,
            DropFrame = dropFrame
        };

        return smpte;
    }

    /// <summary>
    /// Convert the timecode to SMPTE bytes
    /// </summary>
    /// <returns>4-byte array containing SMPTE timecode</returns>
    public byte[] ToBytes()
    {
        var bytes = new byte[4];
        ToBytes(bytes.AsSpan());
        return bytes;
    }

    /// <summary>
    /// Convert the timecode to SMPTE bytes (more efficient span version)
    /// </summary>
    /// <param name="bytes">4-byte span to write SMPTE timecode to</param>
    public void ToBytes(Span<byte> bytes)
    {
        if (bytes.Length != 4)
            throw new ArgumentException("Byte span must be exactly 4 bytes long");

        var hours = Hours;
        var minutes = Minutes;
        var seconds = Seconds;
        var frames = Frames;

        // For progressive formats, divide frames by 2
        if (Timebase is 48 or 50 or 60)
        {
            frames /= 2;
        }

        // Convert integers to BCD (Binary Coded Decimal) format first
        bytes[0] = IntToBcd(frames);  // Frames
        bytes[1] = IntToBcd(seconds); // Seconds
        bytes[2] = IntToBcd(minutes); // Minutes
        bytes[3] = IntToBcd(hours);   // Hours

        // AFTER BCD conversion, set the status bits for progressive formats
        // MXF System Stream field encoding (SMPTE 331M):
        // 48p: 0x80 bit in seconds byte indicates field 1
        // 50p/60p: 0x80 bit in hours byte indicates field 1
        if (Timebase is 48 or 50 or 60)
        {
            if (Field == 1)
            {
                if (Timebase == 48)
                {
                    bytes[1] |= 0x80; // Set 0x80 bit in seconds byte for 48p field 1
                }
                else // 50p or 60p
                {
                    bytes[3] |= 0x80; // Set 0x80 bit in hours byte for 50p/60p field 1
                }
            }
        }

        // Handle drop frame offset for 30/60
        if (DropFrame && (Timebase == 30 || Timebase == 60))
        {
            bytes[0] += 64; // Adjust frames for drop frame
        }
    }

    /// <summary>
    /// Validates that the timebase is one of the supported values
    /// </summary>
    /// <param name="timebase">The timebase to validate</param>
    /// <exception cref="ArgumentException">Thrown when timebase is not supported</exception>
    private static void ValidateTimebase(int timebase)
    {
        if (timebase is not (24 or 25 or 30 or 48 or 50 or 60))
        {
            throw new ArgumentException($"Timebase must be 24, 25, 30, 48, 50, or 60. Got: {timebase}", nameof(timebase));
        }
    }

    /// <summary>
    /// Efficiently converts an integer to BCD (Binary Coded Decimal) byte
    /// Inverse of BcdToInt method
    /// </summary>
    /// <param name="value">Integer value to convert (0-99)</param>
    /// <returns>BCD byte representation</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte IntToBcd(int value)
    {
        if (value < 0 || value > 99)
            throw new ArgumentOutOfRangeException(nameof(value), "Value must be between 0 and 99 for BCD conversion");

        // Convert to BCD: high nibble = tens digit, low nibble = ones digit
        return (byte)((value / 10 << 4) | (value % 10));
    }

    /// <summary>
    /// Efficiently converts a BCD (Binary Coded Decimal) byte to integer
    /// Much faster than string conversion approach
    /// </summary>
    /// <param name="bcd">BCD byte to convert</param>
    /// <returns>Integer representation</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BcdToInt(byte bcd)
    {
        return ((bcd >> 4) * 10) + (bcd & 0x0F);
    }

    /// <summary>
    /// Create a Timecode from a number of seconds
    /// </summary>
    /// <param name="seconds">The number of seconds</param>
    /// <param name="timebase">The timebase of the timecode</param>
    /// <param name="dropFrame">Whether the timecode is a drop frame timecode</param>
    public static Timecode FromSeconds(double seconds, int timebase = 25, bool dropFrame = false)
    {
        // Calculate frames from seconds
        var frames = (int)(seconds * timebase);
        return new Timecode(frames, timebase, dropFrame);
    }

    /// <summary>
    /// Create a Timecode from a number of minutes
    /// </summary>
    /// <param name="minutes">The number of minutes</param>
    /// <param name="timebase">The timebase of the timecode</param>
    /// <param name="dropFrame">Whether the timecode is a drop frame timecode</param>
    public static Timecode FromMinutes(double minutes, int timebase = 25, bool dropFrame = false)
    {
        // Calculate frames from minutes
        var frames = (int)(minutes * timebase * 60);
        return new Timecode(frames, timebase, dropFrame);
    }

    /// <summary>
    /// Create a Timecode from a number of hours
    /// </summary>
    /// <param name="hours">The number of hours</param>
    /// <param name="timebase">The timebase of the timecode</param>
    /// <param name="dropFrame">Whether the timecode is a drop frame timecode</param>
    public static Timecode FromHours(double hours, int timebase = 25, bool dropFrame = false)
    {
        // Calculate frames from hours
        var frames = (int)(hours * timebase * 3600);
        return new Timecode(frames, timebase, dropFrame);
    }

    /// <summary>
    /// Create a Timecode from a timecode string
    /// </summary>
    /// <param name="timecode">The timecode as a string</param>
    /// <param name="timebase">The timebase of the timecode</param>
    /// <param name="dropFrame">Whether the timecode is a drop frame timecode</param>
    public static Timecode FromTimecode(string timecode, int timebase = 25, bool dropFrame = false)
    {
        return new Timecode(timecode, timebase, dropFrame);
    }

    /// <summary>
    /// Get the next timecode (incremented by one frame)
    /// </summary>
    /// <returns>A new Timecode object representing the next frame</returns>
    public Timecode GetNext()
    {
        var next = new Timecode(Hours, Minutes, Seconds, Frames, Timebase, DropFrame);
        var nextFrame = FrameNumber + 1;
        if (nextFrame >= MaxFrames) nextFrame = 0;
        next.FrameNumber = nextFrame;
        return next;
    }

    /// <summary>
    /// Get the previous timecode (decremented by one frame)
    /// </summary>
    /// <returns>A new Timecode object representing the previous frame</returns>
    public Timecode GetPrevious()
    {
        var previous = new Timecode(Hours, Minutes, Seconds, Frames, Timebase, DropFrame);
        var prevFrame = FrameNumber - 1;
        if (prevFrame < 0) prevFrame = MaxFrames - 1;
        previous.FrameNumber = prevFrame;
        return previous;
    }

    // TODO: Factor incrementing of field for 48, 50, 60 (in particular for future SMPTE/TimecodeComponent implementation)

    /// <summary>
    /// ToString override
    /// </summary>
    /// <returns>The timecode as a string in HH:MM:SS:FF format</returns>
    public override string ToString()
    {
        return $"{Hours:D2}:{Minutes:D2}:{Seconds:D2}:{Frames:D2}";
    }

    /// <summary>
    /// Returns the timecode as a string (alias for ToString)
    /// </summary>
    /// <returns>The timecode in HH:MM:SS:FF format</returns>
    public string ToShortString()
    {
        return ToString();
    }

    /// <summary>
    /// Increments the timecode by one frame.
    /// </summary>
    /// <param name="timecode">The timecode to increment</param>
    /// <returns>The incremented timecode</returns>
    public static Timecode operator ++(Timecode timecode)
    {
        ArgumentNullException.ThrowIfNull(timecode);
        var newFrameNumber = timecode.FrameNumber + 1;
        if (newFrameNumber >= timecode.MaxFrames)
        {
            newFrameNumber = 0;
        }
        timecode.FrameNumber = newFrameNumber;
        return timecode;
    }

    /// <summary>
    /// Decrements the timecode by one frame.
    /// </summary>
    /// <param name="timecode">The timecode to decrement</param>
    /// <returns>The decremented timecode</returns>
    public static Timecode operator --(Timecode timecode)
    {
        ArgumentNullException.ThrowIfNull(timecode);
        var newFrameNumber = timecode.FrameNumber - 1;
        if (newFrameNumber < 0)
        {
            newFrameNumber = timecode.MaxFrames - 1;
        }
        timecode.FrameNumber = newFrameNumber;
        return timecode;
    }

    /// <summary>
    /// Adds the specified number of frames to the timecode.
    /// </summary>
    /// <param name="timecode">The source timecode</param>
    /// <param name="frames">The number of frames to add</param>
    /// <returns>A new timecode with the added frames</returns>
    public static Timecode operator +(Timecode timecode, int frames)
    {
        ArgumentNullException.ThrowIfNull(timecode);
        
        var result = new Timecode(timecode.Hours, timecode.Minutes, timecode.Seconds, timecode.Frames, timecode.Timebase, timecode.DropFrame)
        {
            FrameNumber = timecode.FrameNumber + frames
        };
        return result;
    }

    /// <summary>
    /// Subtracts the specified number of frames from the timecode.
    /// </summary>
    /// <param name="timecode">The source timecode</param>
    /// <param name="frames">The number of frames to subtract</param>
    /// <returns>A new timecode with the subtracted frames</returns>
    public static Timecode operator -(Timecode timecode, int frames)
    {
        ArgumentNullException.ThrowIfNull(timecode);
        
        var result = new Timecode(timecode.Hours, timecode.Minutes, timecode.Seconds, timecode.Frames, timecode.Timebase, timecode.DropFrame)
        {
            FrameNumber = timecode.FrameNumber - frames
        };
        return result;
    }

    /// <summary>
    /// Adds two timecodes together.
    /// </summary>
    /// <param name="left">The first timecode</param>
    /// <param name="right">The second timecode</param>
    /// <returns>A new timecode representing the sum</returns>
    /// <exception cref="ArgumentException">Thrown when timecodes have different timebases or drop frame settings</exception>
    public static Timecode operator +(Timecode left, Timecode right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        
        if (left.Timebase != right.Timebase || left.DropFrame != right.DropFrame)
            throw new ArgumentException("Cannot add timecodes with different timebases or drop frame settings");

        var result = new Timecode(left.Hours, left.Minutes, left.Seconds, left.Frames, left.Timebase, left.DropFrame)
        {
            FrameNumber = left.FrameNumber + right.FrameNumber
        };
        return result;
    }

    /// <summary>
    /// Subtracts the second timecode from the first.
    /// </summary>
    /// <param name="left">The first timecode</param>
    /// <param name="right">The second timecode</param>
    /// <returns>A new timecode representing the difference</returns>
    /// <exception cref="ArgumentException">Thrown when timecodes have different timebases or drop frame settings</exception>
    public static Timecode operator -(Timecode left, Timecode right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        
        if (left.Timebase != right.Timebase || left.DropFrame != right.DropFrame)
            throw new ArgumentException("Cannot subtract timecodes with different timebases or drop frame settings");

        var result = new Timecode(left.Hours, left.Minutes, left.Seconds, left.Frames, left.Timebase, left.DropFrame)
        {
            FrameNumber = left.FrameNumber - right.FrameNumber
        };
        return result;
    }

    /// <summary>
    /// Determines whether two timecodes are equal.
    /// </summary>
    /// <param name="left">The first timecode to compare</param>
    /// <param name="right">The second timecode to compare</param>
    /// <returns>True if the timecodes are equal; otherwise, false</returns>
    public static bool operator ==(Timecode? left, Timecode? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.FrameNumber == right.FrameNumber && left.Timebase == right.Timebase && left.DropFrame == right.DropFrame;
    }

    /// <summary>
    /// Determines whether two timecodes are not equal.
    /// </summary>
    /// <param name="left">The first timecode to compare</param>
    /// <param name="right">The second timecode to compare</param>
    /// <returns>True if the timecodes are not equal; otherwise, false</returns>
    public static bool operator !=(Timecode? left, Timecode? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Determines whether the first timecode is less than the second.
    /// </summary>
    /// <param name="left">The first timecode to compare</param>
    /// <param name="right">The second timecode to compare</param>
    /// <returns>True if the first timecode is less than the second; otherwise, false</returns>
    /// <exception cref="ArgumentException">Thrown when timecodes have different timebases or drop frame settings</exception>
    public static bool operator <(Timecode? left, Timecode? right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Timebase != right.Timebase || left.DropFrame != right.DropFrame)
            throw new ArgumentException("Cannot compare timecodes with different timebases or drop frame settings");
        return left.FrameNumber < right.FrameNumber;
    }

    /// <summary>
    /// Determines whether the first timecode is greater than the second.
    /// </summary>
    /// <param name="left">The first timecode to compare</param>
    /// <param name="right">The second timecode to compare</param>
    /// <returns>True if the first timecode is greater than the second; otherwise, false</returns>
    /// <exception cref="ArgumentException">Thrown when timecodes have different timebases or drop frame settings</exception>
    public static bool operator >(Timecode? left, Timecode? right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Timebase != right.Timebase || left.DropFrame != right.DropFrame)
            throw new ArgumentException("Cannot compare timecodes with different timebases or drop frame settings");
        return left.FrameNumber > right.FrameNumber;
    }

    /// <summary>
    /// Determines whether the first timecode is less than or equal to the second.
    /// </summary>
    /// <param name="left">The first timecode to compare</param>
    /// <param name="right">The second timecode to compare</param>
    /// <returns>True if the first timecode is less than or equal to the second; otherwise, false</returns>
    public static bool operator <=(Timecode? left, Timecode? right)
    {
        return left < right || left == right;
    }

    /// <summary>
    /// Determines whether the first timecode is greater than or equal to the second.
    /// </summary>
    /// <param name="left">The first timecode to compare</param>
    /// <param name="right">The second timecode to compare</param>
    /// <returns>True if the first timecode is greater than or equal to the second; otherwise, false</returns>
    public static bool operator >=(Timecode? left, Timecode? right)
    {
        return left > right || left == right;
    }

    /// <summary>
    /// Determines whether the specified timecode is equal to the current timecode.
    /// </summary>
    /// <param name="other">The timecode to compare with the current timecode</param>
    /// <returns>True if the specified timecode is equal to the current timecode; otherwise, false</returns>
    public bool Equals(Timecode? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return FrameNumber == other.FrameNumber && Timebase == other.Timebase && DropFrame == other.DropFrame;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current timecode.
    /// </summary>
    /// <param name="obj">The object to compare with the current timecode</param>
    /// <returns>True if the specified object is equal to the current timecode; otherwise, false</returns>
    public override bool Equals(object? obj)
    {
        return obj is Timecode other && Equals(other);
    }

    /// <summary>
    /// Returns a hash code for the current timecode.
    /// </summary>
    /// <returns>A hash code for the current timecode</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(FrameNumber, Timebase, DropFrame);
    }

    /// <summary>
    /// Compares the current timecode with another timecode and returns an integer that indicates
    /// whether the current timecode precedes, follows, or occurs in the same position in the sort
    /// order as the other timecode.
    /// </summary>
    /// <param name="other">The timecode to compare with this instance</param>
    /// <returns>
    /// A value that indicates the relative order of the objects being compared:
    /// Less than zero: This instance precedes other in the sort order.
    /// Zero: This instance occurs in the same position in the sort order as other.
    /// Greater than zero: This instance follows other in the sort order.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when timecodes have different timebases or drop frame settings</exception>
    public int CompareTo(Timecode? other)
    {
        if (other is null) return 1;
        if (ReferenceEquals(this, other)) return 0;

        if (Timebase != other.Timebase || DropFrame != other.DropFrame)
            throw new ArgumentException("Cannot compare timecodes with different timebases or drop frame settings");

        return FrameNumber.CompareTo(other.FrameNumber);
    }
}