using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;

namespace nathanbutlerDEV.libopx;

/// <summary>
/// Progress reporting helper class for long-running operations
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

/// <summary>
/// Async helper methods for processing operations
/// </summary>
public static class AsyncProcessingHelpers
{
    /// <summary>
    /// Processes VBI file asynchronously with progress reporting
    /// </summary>
    public static async Task<int> ProcessVBIAsync(
        FileInfo? inputFile, 
        int? magazine, 
        int[] rows, 
        int lineCount, 
        Format inputFormat, 
        bool verbose,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var vbi = inputFile != null 
                ? new VBI(inputFile.FullName, inputFormat) 
                : new VBI();
            
            vbi.LineCount = lineCount;
            
            var lineCounter = 0;
            var progressReporter = verbose ? new ProgressReporter("Processing VBI", 5000) : null;
            
            await foreach (var line in vbi.ParseAsync(magazine, rows, cancellationToken))
            {
                Console.WriteLine(line);
                lineCounter++;
                progressReporter?.ReportProgress(lineCounter);
            }
            
            progressReporter?.Complete(lineCounter);
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation was cancelled.");
            return 130; // Standard cancellation exit code
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
    
    /// <summary>
    /// Processes MXF file asynchronously with progress reporting
    /// </summary>
    public static async Task<int> ProcessMXFAsync(
        FileInfo inputFile,
        int? magazine,
        int[] rows,
        bool verbose,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var mxf = new MXF(inputFile)
            {
                Function = Function.Filter,
                Verbose = verbose
            };
            mxf.AddRequiredKey(KeyType.Data);
            
            var packetCounter = 0;
            var progressReporter = verbose ? new ProgressReporter("Processing MXF", 100) : null;
            
            await foreach (var packet in mxf.ParseAsync(magazine, rows, cancellationToken: cancellationToken))
            {
                if (verbose) Console.WriteLine($"Debug: Found packet with {packet.Lines.Count} lines");
                Console.WriteLine(packet);
                
                packetCounter++;
                progressReporter?.ReportProgress(packetCounter);
            }
            
            progressReporter?.Complete(packetCounter);
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation was cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Processes T42 file asynchronously with progress reporting
    /// </summary>
    public static async Task<int> ProcessT42Async(
        FileInfo? inputFile,
        int? magazine,
        int[] rows,
        int lineCount,
        bool verbose,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var t42 = inputFile != null 
                ? new T42(inputFile.FullName) 
                : new T42();
            
            t42.LineCount = lineCount;
            
            var lineCounter = 0;
            var progressReporter = verbose ? new ProgressReporter("Processing T42", 5000) : null;
            
            await foreach (var line in t42.ParseAsync(magazine, rows, cancellationToken))
            {
                Console.WriteLine(line);
                lineCounter++;
                progressReporter?.ReportProgress(lineCounter);
            }
            
            progressReporter?.Complete(lineCounter);
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation was cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Processes BIN file asynchronously with progress reporting
    /// </summary>
    public static async Task<int> ProcessBINAsync(
        FileInfo inputFile,
        int? magazine,
        int[] rows,
        int lineCount,
        bool verbose,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var bin = new BIN(inputFile.FullName);
            
            var lineCounter = 0;
            var progressReporter = verbose ? new ProgressReporter("Processing BIN", 1000) : null;
            
            await foreach (var packet in bin.ParseAsync(magazine, rows, startTimecode: null, cancellationToken))
            {
                Console.WriteLine(packet);
                lineCounter++;
                progressReporter?.ReportProgress(lineCounter);
            }
            
            progressReporter?.Complete(lineCounter);
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation was cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Processes extraction operations asynchronously with progress reporting
    /// </summary>
    public static async Task<int> ProcessExtractAsync(
        FileInfo inputFile,
        string? outputBasePath,
        string? keyString,
        bool demuxMode,
        bool useNames,
        bool klvMode,
        bool verbose,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var progressReporter = verbose ? new ProgressReporter("Extracting MXF", 100) : null;
            
            var result = await Functions.ExtractAsync(inputFile, outputBasePath, keyString, demuxMode, useNames, klvMode, verbose, cancellationToken);
            
            progressReporter?.Complete(1); // Simple completion for extract operations
            return result;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation was cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Processes conversion operations asynchronously with progress reporting
    /// </summary>
    public static async Task<int> ProcessConvertAsync(
        FileInfo? inputFile,
        Format inputFormat,
        Format outputFormat,
        FileInfo? outputFile,
        int? magazine,
        int[] rows,
        int lineCount,
        bool verbose,
        bool keepBlanks,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var progressReporter = verbose ? new ProgressReporter("Converting", 1000) : null;
            
            var result = await Functions.ConvertAsync(inputFile, inputFormat, outputFormat, outputFile, magazine, rows, lineCount, verbose, keepBlanks, cancellationToken);
            
            progressReporter?.Complete(1); // Simple completion for convert operations
            return result;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation was cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Example: Process large VBI file with cancellation support
    /// </summary>
    public static async Task ProcessLargeVBIExample()
    {
        using var cts = new CancellationTokenSource();
        
        // Cancel after 30 seconds
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        
        try
        {
            using var vbi = new VBI("large_file.vbi");
            
            await foreach (var line in vbi.ParseAsync(magazine: 8, rows: [20, 22], cts.Token))
            {
                Console.WriteLine(line);
                
                // Check for user cancellation (Ctrl+C)
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(true);
                    if (keyInfo.Key == ConsoleKey.C && keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        cts.Cancel();
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Operation cancelled by user or timeout.");
        }
    }
}
