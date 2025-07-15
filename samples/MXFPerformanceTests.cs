using System.Diagnostics;

namespace libopx.Tests;

public class MXFPerformanceTests
{
    private const string TestMxfPath = "test.mxf";

    [Fact]
    public async Task ParseSMPTETimecodesAsync_MeasurePerformance()
    {
        // Arrange
        var stopwatch = new Stopwatch();
        
        // Verify test file exists
        Assert.True(File.Exists(TestMxfPath), $"Test file not found: {TestMxfPath}");

        // Act
        stopwatch.Start();
        
        using var mxf = new MXF(TestMxfPath);
        var smpteResult = await mxf.ParseSMPTETimecodesAsync();
        
        stopwatch.Stop();

        // Assert
        Assert.True(smpteResult > 0, "Should parse at least one SMPTE timecode");
        Assert.True(mxf.SMPTETimecodes.Count > 0, "SMPTETimecodes collection should not be empty");
        Assert.Equal(smpteResult, mxf.SMPTETimecodes.Count);

        // Performance reporting
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var elapsedTicks = stopwatch.ElapsedTicks;
        var timecodesPerSecond = smpteResult / (stopwatch.Elapsed.TotalSeconds > 0 ? stopwatch.Elapsed.TotalSeconds : 1);

        // Output performance metrics for analysis
        var output = $"""
            ===== MXF Performance Test Results =====
            File: {TestMxfPath}
            Timecodes parsed: {smpteResult}
            Execution time: {elapsedMs} ms ({stopwatch.Elapsed.TotalSeconds:F3} seconds)
            High-precision ticks: {elapsedTicks}
            Throughput: {timecodesPerSecond:F2} timecodes/second
            Average time per timecode: {(double)elapsedMs / smpteResult:F3} ms
            ==========================================
            """;
        
        // This will be visible in test output
        Console.WriteLine(output);
        
        // Also write to a performance log file for tracking improvements over time
        var logPath = "performance_log.txt";
        var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{smpteResult},{elapsedMs},{timecodesPerSecond:F2}\n";
        await File.AppendAllTextAsync(logPath, logEntry);
    }

    [Fact]
    public async Task ParseSMPTETimecodesSync_MeasurePerformance()
    {
        // Arrange
        var stopwatch = new Stopwatch();
        
        // Verify test file exists
        Assert.True(File.Exists(TestMxfPath), $"Test file not found: {TestMxfPath}");

        // Act
        stopwatch.Start();
        
        using var mxf = new MXF(TestMxfPath);
        var smpteResult = await mxf.ParseSMPTETimecodesAsync(); // Use async version
        
        stopwatch.Stop();

        // Assert
        Assert.True(smpteResult > 0, "Should parse at least one SMPTE timecode");
        Assert.True(mxf.SMPTETimecodes.Count > 0, "SMPTETimecodes collection should not be empty");
        Assert.Equal(smpteResult, mxf.SMPTETimecodes.Count);

        // Performance reporting
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var timecodesPerSecond = smpteResult / (stopwatch.Elapsed.TotalSeconds > 0 ? stopwatch.Elapsed.TotalSeconds : 1);

        var output = $"""
            ===== MXF Sync Performance Test Results =====
            File: {TestMxfPath}
            Timecodes parsed: {smpteResult}
            Execution time: {elapsedMs} ms ({stopwatch.Elapsed.TotalSeconds:F3} seconds)
            Throughput: {timecodesPerSecond:F2} timecodes/second
            Average time per timecode: {(double)elapsedMs / smpteResult:F3} ms
            ==============================================
            """;
        
        Console.WriteLine(output);
    }

    [Theory]
    [InlineData(2)]
    public async Task ParseSMPTETimecodesAsync_MultipleRuns_AveragePerformance(int numberOfRuns)
    {
        // Arrange
        var times = new List<long>();
        var results = new List<int>();
        
        Assert.True(File.Exists(TestMxfPath), $"Test file not found: {TestMxfPath}");

        // Act - Run multiple times to get average performance
        for (int i = 0; i < numberOfRuns; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            using var mxf = new MXF(TestMxfPath);
            var smpteResult = await mxf.ParseSMPTETimecodesAsync();
            
            stopwatch.Stop();
            
            times.Add(stopwatch.ElapsedMilliseconds);
            results.Add(smpteResult);
        }

        // Assert and analyze
        var averageTime = times.Average();
        var minTime = times.Min();
        var maxTime = times.Max();
        var averageResult = results.Average();

        var output = $"""
            ===== MXF Multiple Runs Performance Analysis =====
            Number of runs: {numberOfRuns}
            Average timecodes parsed: {averageResult:F1}
            Average execution time: {averageTime:F2} ms
            Minimum execution time: {minTime} ms
            Maximum execution time: {maxTime} ms
            Standard deviation: {CalculateStandardDeviation(times):F2} ms
            ===================================================
            """;
        
        Console.WriteLine(output);

        // All runs should return the same number of timecodes
        Assert.True(results.All(r => r == results.First()), "All runs should return the same number of timecodes");
    }

    private static double CalculateStandardDeviation(IEnumerable<long> values)
    {
        var average = values.Average();
        var sumOfSquaresOfDifferences = values.Select(val => (val - average) * (val - average)).Sum();
        return Math.Sqrt(sumOfSquaresOfDifferences / values.Count());
    }
}
