using System.Diagnostics;

namespace libopx.Tests;

public class MXFBasicPerformanceTest
{
    private const string TestMxfPath = "test.mxf";

    [Fact]
    public void ParseSMPTETimecodes_BasicPerformanceTest()
    {
        // Arrange
        var stopwatch = new Stopwatch();
        
        // Verify test file exists
        Assert.True(File.Exists(TestMxfPath), $"Test file not found: {TestMxfPath}");

        // Act - This exactly matches your original request
        stopwatch.Start();
        
        using var mxf = new MXF(TestMxfPath);
        var smpteResult = mxf.ParseSMPTETimecodes();
        
        stopwatch.Stop();

        // Assert
        Assert.True(smpteResult, "Should parse at least one SMPTE timecode");
        Assert.True(mxf.SMPTETimecodes.Count > 0, "SMPTETimecodes collection should not be empty");

        // Performance reporting - simplified for quick feedback
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var timecodesPerSecond = mxf.SMPTETimecodes.Count / (stopwatch.Elapsed.TotalSeconds > 0 ? stopwatch.Elapsed.TotalSeconds : 1);

        var output = $"""
            ===== Basic MXF Performance Test Results =====
            Timecodes parsed: {smpteResult}
            Execution time: {elapsedMs} ms ({stopwatch.Elapsed.TotalSeconds:F2} seconds)
            Throughput: {timecodesPerSecond:F1} timecodes/second
            ===============================================
            """;
        
        Console.WriteLine(output);
        
        // Optional: Add performance assertions for regression testing
        // Assert.True(elapsedMs < 35000, $"Performance regression: took {elapsedMs}ms, expected < 35000ms");
        // Assert.True(timecodesPerSecond > 90, $"Throughput regression: {timecodesPerSecond:F1} timecodes/second, expected > 90");
        
        // Clean up
        mxf.Dispose();
    }
}
