using System;
using Xunit;
using nathanbutlerDEV.libopx;

namespace libopx.Tests
{
    public class TimecodeTests
    {
        [Fact]
        public void CreateFileFromTimecodeBytes()
        {
            // Start at 23:59:59:00 and to to 00:00:01:00, adding each byte array to a list
            byte[] bytes = new byte[400];
            var startTimecode = new Timecode(23, 59, 59, 0, 25, false);
            for (var i = 0; i < 50; i++)
            {
                startTimecode.ToBytes(bytes.AsSpan(i * 4, 4));
                startTimecode++;
            }
            File.WriteAllBytes("timecode25.bin", bytes);
            Assert.True(File.Exists("timecode25.bin"));

            bytes = new byte[400];
            startTimecode = new Timecode(23, 59, 59, 0, 48, false);
            for (var i = 0; i < 96; i++)
            {
                startTimecode.ToBytes(bytes.AsSpan(i * 4, 4));
                startTimecode++;
            }

            File.WriteAllBytes("timecode48.bin", bytes);
            Assert.True(File.Exists("timecode48.bin"));

            bytes = new byte[400];
            startTimecode = new Timecode(23, 59, 59, 0, 50, false);
            for (var i = 0; i < 100; i++)
            {
                startTimecode.ToBytes(bytes.AsSpan(i * 4, 4));
                startTimecode++;
            }

            File.WriteAllBytes("timecode50.bin", bytes);
            Assert.True(File.Exists("timecode50.bin"));
        }
    }
}