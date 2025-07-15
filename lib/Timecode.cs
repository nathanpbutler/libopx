using System;

namespace nathanbutlerDEV.libopx;

public class Timecode
{
    /// <summary>
    /// The hours component of the timecode
    /// </summary>
    public int Hours { get; set; }
    /// <summary>
    /// The minutes component of the timecode
    /// </summary>
    public int Minutes { get; set; }
    /// <summary>
    /// The seconds component of the timecode
    /// </summary>
    public int Seconds { get; set; }
    /// <summary>
    /// The frames component of the timecode
    /// </summary>
    public int Frames { get; set; }
    /// <summary>
    /// The field component of the timecode - Determined automatically based on the timebase
    /// </summary>
    public int Field => Timebase is 48 or 50 or 60 ? Frames % 2 : 0;
    /// <summary>
    /// The timebase of the timecode
    /// </summary>
    public int Timebase { get; set; }
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
    public bool DropFrame { get; set; }
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

            // Handle drop frame adjustment - add back the dropped frames
            if (DropFrame && Timebase is 30 or 60)
            {
                var dropFramesPerMinute = Timebase == 30 ? 2 : 4;
                
                // Estimate minutes and add back drop frames
                var estimatedMinutes = totalFrames / (Timebase * 60);
                var dropFrames = estimatedMinutes / 10 * 9 * dropFramesPerMinute;
                if (estimatedMinutes % 10 != 0)
                {
                    dropFrames += estimatedMinutes % 10 * dropFramesPerMinute;
                }
                totalFrames += dropFrames;
            }

            // Convert back to H:M:S:F
            var totalSeconds = totalFrames / Timebase;
            var frames = (int)(totalFrames % Timebase);
            
            var hours = (int)(totalSeconds / 3600);
            var minutes = (int)(totalSeconds % 3600 / 60);
            var seconds = (int)(totalSeconds % 60);

            Hours = hours % 24;  // Ensure hours wrap at 24
            Minutes = minutes;
            Seconds = seconds;
            Frames = frames;
            // Field is now automatically calculated as Frames % 2 for progressive formats
        }
    }

    public Timecode() {}

    /// <summary>
    /// Constructor for the Timecode class
    /// </summary>
    /// <param name="hours">The hours component of the timecode</param>
    /// <param name="minutes">The minutes component of the timecode</param>
    /// <param name="seconds">The seconds component of the timecode</param>
    /// <param name="frames">The frames component of the timecode</param>
    /// <param name="field">The field component of the timecode</param>
    public Timecode(int hours, int minutes, int seconds, int frames, int timebase = 25, bool dropFrame = false)
    {
        if (hours < 0 || hours >= 24) throw new ArgumentException("Hours must be between 0 and 23");
        if (minutes < 0 || minutes >= 60) throw new ArgumentException("Minutes must be between 0 and 59");
        if (seconds < 0 || seconds >= 60) throw new ArgumentException("Seconds must be between 0 and 59");
        if (frames < 0 || frames >= timebase) throw new ArgumentException("Frames must be between 0 and the timebase");
        if (timebase < 24 || timebase > 60) throw new ArgumentException("Timebase must be between 24 and 60");
        if (dropFrame && timebase is not 30 and not 60) throw new ArgumentException("Drop frame is only supported for 30 and 60 timebases");

        Hours = hours;
        Minutes = minutes;
        Seconds = seconds;
        Frames = frames;
        DropFrame = dropFrame;
        Timebase = timebase;
    }

    /// <summary>
    /// Constructor for the Timecode class
    /// </summary>
    /// <param name="timecode">The timecode as a string</param>
    /// <param name="timebase">The timebase of the timecode</param>
    /// <param name="dropFrame">Whether the timecode is a drop frame timecode</param>
    public Timecode(string timecode, int timebase = 25, bool dropFrame = false, int field = 0)
    {
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
        if (parts.Length != 4) throw new ArgumentException("Timecode must be in the format HH:MM:SS:FF or HH:MM:SS;FF");
        if (dropFrame && timebase is not 30 and not 60) throw new ArgumentException("Drop frame is only supported for 30 and 60 timebases");
        Hours = int.Parse(parts[0]);
        Minutes = int.Parse(parts[1]);
        Seconds = int.Parse(parts[2]);
        Frames = int.Parse(parts[3]);
        if (Frames < 0 || Frames >= timebase) throw new ArgumentException("Frames must be between 0 and the timebase");
        Timebase = timebase;
    }

    /// <summary>
    /// Constructor for the Timecode class
    /// </summary>
    /// <param name="totalFrames">The total number of frames in the timecode</param>
    /// <param name="timebase">The timebase of the timecode</param>
    /// <param name="dropFrame">Whether the timecode is a drop frame timecode</param>
    /// <param name="field">The field component of the timecode</param>
    /// <exception cref="ArgumentException">Thrown when the timebase is not 30 or 60 and dropFrame is true</exception>
    /// <exception cref="ArgumentException">Thrown when the frames are not between 0 and the timebase</exception>
    /// <exception cref="ArgumentException">Thrown when the total frames are not between 0 and the maximum number of frames</exception>
    public Timecode(int totalFrames, int timebase = 25, bool dropFrame = false)
    {
        if (dropFrame && timebase is not 30 and not 60) throw new ArgumentException("Drop frame is only supported for 30 and 60 timebases");

        if (totalFrames > MaxFrames)
        {
            totalFrames %= MaxFrames;
        }

        var frames = 0;
        var seconds = 0;
        var minutes = 0;
        var hours = 0;

        for (var f = 0; f < totalFrames; f++)
        {
            frames++;

            if (frames < timebase) continue;
            frames = 0;
            seconds++;

            if (seconds < 60) continue;
            seconds = 0;
            minutes++;

            if (dropFrame && timebase is 30 or 60)
            {
                switch (timebase)
                {
                    case 30 when minutes % 10 != 0:
                        frames += 2;
                        break;
                    case 60 when minutes % 10 != 0:
                        frames += 4;
                        break;
                }
            }

            if (minutes < 60) continue;
            minutes = 0;
            hours++;
        }

        Hours = hours;
        Minutes = minutes;
        Seconds = seconds;
        Frames = frames;
        DropFrame = dropFrame;
        Timebase = timebase;
    }

    /// <summary>
    /// Create a Timecode from SMPTE bytes
    /// </summary>
    /// <param name="bytes">4-byte array containing SMPTE timecode</param>
    /// <param name="timebase">The timebase of the timecode</param>
    /// <param name="dropFrame">Whether the timecode is a drop frame timecode</param>
    /// <param name="field">The field component of the timecode</param>
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
    /// <param name="field">The field component of the timecode</param>
    public static Timecode FromBytes(ReadOnlySpan<byte> bytes, int timebase = 25, bool dropFrame = false)
    {
        if (bytes.Length != 4)
            throw new ArgumentException("Byte span must be exactly 4 bytes long");

        var hours = bytes[3];
        var minutes = bytes[2];
        var seconds = bytes[1];
        var frames = bytes[0];
        var extractedField = 0;

        // If Hours -band 0x80 = 128, then it is 50p and field is 1
        if ((hours & 0x80) == 0x80)
        {
            extractedField = 1;
            hours -= 128;
        }

        // If Seconds -band 0x80 = 128, then it is 48p and field is 1
        if ((seconds & 0x80) == 0x80)
        {
            extractedField = 1;
            seconds -= 128;
        }

        // Handle drop frame offset for 30p/60p
        if (timebase is 30 or 60)
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

        // Handle drop frame offset for 30p/60p BEFORE BCD conversion
        if (Timebase is 30 or 60)
        {
            frames += 64;
        }

        // Convert integers to BCD (Binary Coded Decimal) format first
        bytes[0] = IntToBcd(frames);  // Frames
        bytes[1] = IntToBcd(seconds); // Seconds
        bytes[2] = IntToBcd(minutes); // Minutes
        bytes[3] = IntToBcd(hours);   // Hours

        // AFTER BCD conversion, set the status bits for progressive formats
        if (Timebase is 48 or 50 or 60)
        {
            if (Timebase == 50 && Field == 1)
            {
                bytes[3] |= 0x80; // Set 0x80 bit in hours byte for 50p field 1
            }
            else if (Timebase == 48 && Field == 1)
            {
                bytes[1] |= 0x80; // Set 0x80 bit in seconds byte for 48p field 1
            }
            else if (Timebase == 60 && Field == 1)
            {
                bytes[3] |= 0x80; // Set 0x80 bit in hours byte for 60p field 1
            }
        }
    }

    /// <summary>
    /// Efficiently converts an integer to BCD (Binary Coded Decimal) byte
    /// Inverse of BcdToInt method
    /// </summary>
    /// <param name="value">Integer value to convert (0-99)</param>
    /// <returns>BCD byte representation</returns>
    private static byte IntToBcd(int value)
    {
        if (value < 0 || value > 99)
            throw new ArgumentOutOfRangeException(nameof(value), "Value must be between 0 and 99 for BCD conversion");
        
        return (byte)(((value / 10) << 4) | (value % 10));
    }

    /// <summary>
    /// Efficiently converts a BCD (Binary Coded Decimal) byte to integer
    /// Much faster than string conversion approach
    /// </summary>
    /// <param name="bcd">BCD byte to convert</param>
    /// <returns>Integer representation</returns>
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
    /// <returns>The timecode as a string</returns>
    public override string ToString()
    {
        // If 50p, add field as ".00" or ".01"
        var field = Timebase >= 48 ? $".{Field:D2}" : "";
        return $"{Hours:D2}:{Minutes:D2}:{Seconds:D2}:{Frames:D2}{field}";
    }

    // ++ Operator
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

    // -- Operator
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

    // + Operator
    public static Timecode operator +(Timecode timecode, int frames)
    {
        ArgumentNullException.ThrowIfNull(timecode);
        
        var result = new Timecode(timecode.Hours, timecode.Minutes, timecode.Seconds, timecode.Frames, timecode.Timebase, timecode.DropFrame)
        {
            FrameNumber = timecode.FrameNumber + frames
        };
        return result;
    }

    // - Operator
    public static Timecode operator -(Timecode timecode, int frames)
    {
        ArgumentNullException.ThrowIfNull(timecode);
        
        var result = new Timecode(timecode.Hours, timecode.Minutes, timecode.Seconds, timecode.Frames, timecode.Timebase, timecode.DropFrame)
        {
            FrameNumber = timecode.FrameNumber - frames
        };
        return result;
    }

    // + Operator for two timecodes
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

    // - Operator for two timecodes
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

    // Equality operators
    public static bool operator ==(Timecode? left, Timecode? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.FrameNumber == right.FrameNumber && left.Timebase == right.Timebase && left.DropFrame == right.DropFrame;
    }

    public static bool operator !=(Timecode? left, Timecode? right)
    {
        return !(left == right);
    }

    // Comparison operators
    public static bool operator <(Timecode? left, Timecode? right)
    {
        if (left is null || right is null)
            throw new ArgumentNullException(left is null ? nameof(left) : nameof(right));
        if (left.Timebase != right.Timebase || left.DropFrame != right.DropFrame)
            throw new ArgumentException("Cannot compare timecodes with different timebases or drop frame settings");
        return left.FrameNumber < right.FrameNumber;
    }

    public static bool operator >(Timecode? left, Timecode? right)
    {
        if (left is null || right is null)
            throw new ArgumentNullException(left is null ? nameof(left) : nameof(right));
        if (left.Timebase != right.Timebase || left.DropFrame != right.DropFrame)
            throw new ArgumentException("Cannot compare timecodes with different timebases or drop frame settings");
        return left.FrameNumber > right.FrameNumber;
    }

    public static bool operator <=(Timecode? left, Timecode? right)
    {
        return left < right || left == right;
    }

    public static bool operator >=(Timecode? left, Timecode? right)
    {
        return left > right || left == right;
    }

    // Override Equals and GetHashCode
    public override bool Equals(object? obj)
    {
        return obj is Timecode other && this == other;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(FrameNumber, Timebase, DropFrame);
    }
}