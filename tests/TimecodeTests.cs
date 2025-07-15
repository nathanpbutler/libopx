using System;
using Xunit;
using nathanbutlerDEV.libopx;

namespace libopx.Tests
{
    public class TimecodeTests
    {
        [Fact]
        public void TestIncrementOperator_InterlacedFormat()
        {
            // Test case from user (progressive)
            var timecode = new Timecode(23, 59, 59, 0, 50, false);
            Assert.Equal("23:59:59:00.00", timecode.ToString());
            Assert.Equal(4319950, timecode.FrameNumber);
            
            timecode++;
            Assert.Equal("23:59:59:01.01", timecode.ToString());
            Assert.Equal(4319951, timecode.FrameNumber);
        }
        
        [Fact]
        public void TestWrapAroundMidnight_InterlacedFormat()
        {
            // Test wrapping at midnight (progressive)
            var tc = new Timecode(23, 59, 59, 49, 50, false);
            Assert.Equal("23:59:59:49.01", tc.ToString());
            Assert.Equal(4319999, tc.FrameNumber);
            
            tc++;
            Assert.Equal("00:00:00:00.00", tc.ToString());
            Assert.Equal(0, tc.FrameNumber);
        }
        
        [Fact]
        public void TestWrapAroundMidnight_25fps()
        {
            // Test with 25fps (progressive)
            var tc = new Timecode(23, 59, 59, 24, 25, false);
            Assert.Equal("23:59:59:24", tc.ToString());
            Assert.Equal(2159999, tc.FrameNumber);
            
            tc++;
            Assert.Equal("00:00:00:00", tc.ToString());
            Assert.Equal(0, tc.FrameNumber);
        }
        
        [Fact]
        public void TestFrameNumberCalculation_BasicCases()
        {
            // Test basic frame number calculations
            var tc1 = new Timecode(0, 0, 0, 0, 25, false);
            Assert.Equal(0, tc1.FrameNumber);
            
            var tc2 = new Timecode(0, 0, 1, 0, 25, false);
            Assert.Equal(25, tc2.FrameNumber);
            
            var tc3 = new Timecode(0, 1, 0, 0, 25, false);
            Assert.Equal(1500, tc3.FrameNumber);
            
            var tc4 = new Timecode(1, 0, 0, 0, 25, false);
            Assert.Equal(90000, tc4.FrameNumber);
        }
        
        [Fact]
        public void TestFrameNumberCalculation_ProgressiveFormats()
        {
            // Test 50p format (progressive)
            var tc1 = new Timecode(0, 0, 0, 0, 50, false);
            Assert.Equal(0, tc1.FrameNumber);
            
            var tc2 = new Timecode(0, 0, 0, 1, 50, false);
            Assert.Equal(1, tc2.FrameNumber);
            
            var tc3 = new Timecode(0, 0, 0, 2, 50, false);
            Assert.Equal(2, tc3.FrameNumber);
            
            var tc4 = new Timecode(0, 0, 1, 1, 50, false);
            Assert.Equal(100, tc4.FrameNumber);
        }
        
        [Fact]
        public void TestDropFrameCalculation_30fps()
        {
            // Test drop frame for 30fps
            var tc1 = new Timecode(0, 1, 0, 0, 30, true);
            // At 1 minute, we should have dropped 2 frames
            // Total frames = 1 * 60 * 30 = 1800
            // Drop frames = 2
            // Expected = 1800 - 2 = 1798
            Assert.Equal(1798, tc1.FrameNumber);
            
            var tc2 = new Timecode(0, 10, 0, 0, 30, true);
            // At 10 minutes (tenth minute), no frames dropped for this minute
            // Total frames = 10 * 60 * 30 = 18000
            // Drop frames = 9 * 2 = 18 (for minutes 1-9)
            // Expected = 18000 - 18 = 17982
            Assert.Equal(17982, tc2.FrameNumber);
        }
        
        [Fact]
        public void TestDropFrameCalculation_60fps()
        {
            // Test drop frame for 60fps (progressive)
            var tc = new Timecode(0, 1, 0, 0, 60, true);
            // At 1 minute, we should have dropped 4 frames
            // Total frames = 1 * 60 * 60 = 3600
            // Drop frames = 4
            // Expected = 7200 - 4 = 7196
            Assert.Equal(7196, tc.FrameNumber);
        }
    }
}