using System;

namespace nathanbutlerDEV.libopx;

public class TimecodeComponent
{
    public byte[] InstanceID { get; set; } = new byte[16];
    public byte[] ComponentDataDefinition { get; set; } = new byte[16];
    public int ComponentLength { get; set; }
    public int StartTimecode { get; set; }
    public int RoundedTimecodeTimebase { get; private set; }
    public bool DropFrame { get; private set; }

    /// <summary>
    /// Parse the timecode component
    /// </summary>
    /// <param name="timecodeComponentBytes">The timecode component bytes</param>
    /// <returns>The timecode component</returns>
    public static TimecodeComponent Parse(byte[] timecodeComponentBytes)
    {
        var timecodeComponent = new TimecodeComponent();
        var t = 0;
        while (t < timecodeComponentBytes.Length)
        {
            // Read the tag
            var tagBytes = timecodeComponentBytes[t..(t + 2)];
            t += 2;
            if (BitConverter.IsLittleEndian)
                Array.Reverse(tagBytes);
            var tag = BitConverter.ToUInt16(tagBytes, 0);
            var tagLengthBytes = timecodeComponentBytes[t..(t + 2)];
            t += 2;
            if (BitConverter.IsLittleEndian)
                Array.Reverse(tagLengthBytes);
            var tagLength = BitConverter.ToUInt16(tagLengthBytes, 0);
            var tagValueBytes = timecodeComponentBytes[t..(t + tagLength)];
            if (BitConverter.IsLittleEndian)
                Array.Reverse(tagValueBytes);
            var tagValue = tagLength switch {
                8 => BitConverter.ToInt64(tagValueBytes, 0),
                4 => BitConverter.ToInt32(tagValueBytes, 0),
                2 => BitConverter.ToInt16(tagValueBytes, 0),
                1 => tagValueBytes[0],
                _ => 0
            };
            t += tagLength;
            var tagName = GetTagName(tag);
            switch (tagName)
            {
                case "Instance ID":
                    timecodeComponent.InstanceID = tagValueBytes;
                    break;
                case "Component Data Definition":
                    timecodeComponent.ComponentDataDefinition = tagValueBytes;
                    break;
                case "Component Length":
                    timecodeComponent.ComponentLength = (int)tagValue;
                    break;
                case "Start Timecode":
                    timecodeComponent.StartTimecode = (int)tagValue;
                    break;
                case "Rounded Timecode Timebase":
                    timecodeComponent.RoundedTimecodeTimebase = (int)tagValue;
                    break;
                case "Drop Frame":
                    timecodeComponent.DropFrame = tagValue == 1;
                    break;
            }
        }
        
        return timecodeComponent;
    }
    
    private static string GetTagName(int tag)
    {
        return tag switch
        {
            0x3C0A => "Instance ID",
            0x0201 => "Component Data Definition",
            0x0202 => "Component Length",
            0x1501 => "Start Timecode",
            0x1502 => "Rounded Timecode Timebase",
            0x1503 => "Drop Frame",
            _ => "Unknown"
        };
    }
}
