using System;

namespace nathanbutlerDEV.libopx;

/// <summary>
/// Utility classes for async operations and progress reporting.
/// </summary>

/// <summary>
/// Progress reporting helper class for long-running operations.
/// Provides periodic progress updates with item counts, processing rates, and elapsed time.
/// </summary>
public class ProgressReporter : IDisposable
{
    private readonly string _operationName;
    private readonly int _reportInterval;
    private readonly DateTime _startTime;
    private DateTime _lastReportTime;
    private int _lastReportedCount;

    /// <summary>
    /// Initializes a new instance of the ProgressReporter class for tracking operation progress.
    /// </summary>
    /// <param name="operationName">The name of the operation being tracked (e.g., "Filtering", "Converting")</param>
    /// <param name="reportInterval">The minimum number of items to process before reporting progress (default: 1000)</param>
    public ProgressReporter(string operationName, int reportInterval = 1000)
    {
        _operationName = operationName;
        _reportInterval = reportInterval;
        _startTime = DateTime.Now;
        _lastReportTime = _startTime;
        
        Console.Error.WriteLine($"{_operationName} started at {_startTime:HH:mm:ss}");
    }

    /// <summary>
    /// Reports progress for the current operation, displaying item count, processing rate, and elapsed time.
    /// Progress is only reported if the report interval has been reached or 5 seconds have elapsed since the last report.
    /// </summary>
    /// <param name="currentCount">The current number of items that have been processed</param>
    public void ReportProgress(int currentCount)
    {
        if (currentCount - _lastReportedCount >= _reportInterval || 
            (DateTime.Now - _lastReportTime).TotalSeconds >= 5)
        {
            var elapsed = DateTime.Now - _startTime;
            var rate = currentCount / elapsed.TotalSeconds;
            
            Console.Error.WriteLine($"{_operationName}: {currentCount:N0} items processed " +
                                  $"({rate:F0} items/sec, {elapsed:mm\\:ss} elapsed)");
            
            _lastReportTime = DateTime.Now;
            _lastReportedCount = currentCount;
        }
    }

    /// <summary>
    /// Reports the completion of the operation with final statistics including total items processed,
    /// total elapsed time, and average processing rate.
    /// </summary>
    /// <param name="totalCount">The total number of items that were processed during the operation</param>
    public void Complete(int totalCount)
    {
        var totalElapsed = DateTime.Now - _startTime;
        var averageRate = totalCount / totalElapsed.TotalSeconds;
        
        Console.Error.WriteLine($"{_operationName} completed: {totalCount:N0} items processed " +
                              $"in {totalElapsed:mm\\:ss} ({averageRate:F0} items/sec average)");
    }

    /// <summary>
    /// Releases all resources used by the ProgressReporter.
    /// This method follows the IDisposable pattern for use with using statements.
    /// </summary>
    public void Dispose()
    {
        // Nothing to dispose, but implements pattern for using statements
        GC.SuppressFinalize(this);
    }
}
