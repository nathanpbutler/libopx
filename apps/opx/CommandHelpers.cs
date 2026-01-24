using System.CommandLine;
using System.CommandLine.Completions;
using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.opx;

public static class CommandHelpers
{
    /// <summary>
    /// Creates completion source for input format options.
    /// </summary>
    public static Func<CompletionContext, IEnumerable<CompletionItem>> CreateInputFormatCompletionSource()
    {
        return _ =>
        [
            new CompletionItem("bin", "MXF data stream"),
            new CompletionItem("vbi", "VBI format"),
            new CompletionItem("vbid", "VBI format (double width)"),
            new CompletionItem("t42", "T42 format"),
            new CompletionItem("mxf", "MXF container format"),
            new CompletionItem("ts", "MPEG Transport Stream")
        ];
    }

    /// <summary>
    /// Creates completion source for output format options.
    /// </summary>
    public static Func<CompletionContext, IEnumerable<CompletionItem>> CreateOutputFormatCompletionSource()
    {
        return _ =>
        [
            new CompletionItem("vbi", "VBI format"),
            new CompletionItem("vbid", "VBI format (double width)"),
            new CompletionItem("t42", "T42 format"),
            new CompletionItem("rcwt", "RCWT format (Raw Captions With Time)"),
            new CompletionItem("stl", "EBU STL format (EBU-Tech 3264)")
        ];
    }

    /// <summary>
    /// Creates completion source for extract key options.
    /// </summary>
    public static Func<CompletionContext, IEnumerable<CompletionItem>> CreateKeyCompletionSource()
    {
        return _ =>
        [
            new CompletionItem("v", "Video stream"),
            new CompletionItem("a", "Audio stream"),
            new CompletionItem("d", "Data stream"),
            new CompletionItem("s", "System stream"),
            new CompletionItem("t", "TimecodeComponent stream")
        ];
    }

    /// <summary>
    /// Parses a string of rows into an array of integers, handling both single numbers and ranges
    /// (e.g., "1,2,5-8,15" becomes [1, 2, 5, 6, 7, 8, 15]).
    /// </summary>
    /// <param name="rowsString">The string representation of the rows.</param>
    /// <returns>An array of integers representing the parsed rows.</returns>
    public static int[] ParseRowsString(string rowsString)
    {
        return FilterHelpers.ParseRowsString(rowsString);
    }

    /// <summary>
    /// Validates input file and handles stdin/file path logic.
    /// </summary>
    /// <param name="inputFilePath">Input file path or null/stdin indicators.</param>
    /// <returns>Validated FileInfo or null for stdin.</returns>
    /// <exception cref="FileNotFoundException">If specified file doesn't exist.</exception>
    public static FileInfo? ValidateInputFile(string? inputFilePath)
    {
        if (string.IsNullOrEmpty(inputFilePath) || 
            inputFilePath.Equals("-", StringComparison.OrdinalIgnoreCase) || 
            inputFilePath.Equals("stdin", StringComparison.OrdinalIgnoreCase))
        {
            return null; // Read from stdin
        }

        var fullPath = Path.GetFullPath(inputFilePath);
        var inputFile = new FileInfo(fullPath);
        
        if (!inputFile.Exists)
        {
            throw new FileNotFoundException($"Input file '{inputFile.FullName}' does not exist.");
        }

        return inputFile;
    }

    /// <summary>
    /// Handles output file path logic for stdout/file output.
    /// </summary>
    /// <param name="outputFilePath">Output file path or null/stdout indicators.</param>
    /// <returns>FileInfo for file output or null for stdout.</returns>
    public static FileInfo? ParseOutputFile(string? outputFilePath)
    {
        if (string.IsNullOrEmpty(outputFilePath) || 
            outputFilePath.Equals("-", StringComparison.OrdinalIgnoreCase) || 
            outputFilePath.Equals("stdout", StringComparison.OrdinalIgnoreCase))
        {
            return null; // Write to stdout
        }

        return new FileInfo(Path.GetFullPath(outputFilePath));
    }

    /// <summary>
    /// Determines format with format string taking precedence over file extension.
    /// </summary>
    /// <param name="formatString">Explicit format string.</param>
    /// <param name="file">File to extract extension from.</param>
    /// <param name="defaultFormat">Default format if neither specified.</param>
    /// <returns>Parsed format.</returns>
    public static Format DetermineFormat(string? formatString, FileInfo? file, Format defaultFormat)
    {
        if (!string.IsNullOrEmpty(formatString))
        {
            return Functions.ParseFormat(formatString);
        }
        
        if (file != null)
        {
            return Functions.ParseFormat(Path.GetExtension(file.Name).ToLowerInvariant());
        }

        return defaultFormat;
    }

    /// <summary>
    /// Determines format with file extension taking precedence over format string (for filter command).
    /// </summary>
    /// <param name="file">File to extract extension from.</param>
    /// <param name="formatString">Explicit format string as fallback.</param>
    /// <param name="defaultFormat">Default format if neither specified.</param>
    /// <returns>Parsed format.</returns>
    public static Format DetermineFormatFromFile(FileInfo? file, string? formatString, Format defaultFormat)
    {
        if (file != null)
        {
            return Functions.ParseFormat(Path.GetExtension(file.Name).ToLowerInvariant());
        }
        
        if (!string.IsNullOrEmpty(formatString))
        {
            return Functions.ParseFormat(formatString);
        }

        return defaultFormat;
    }

    /// <summary>
    /// Determines which rows to use based on options.
    /// </summary>
    /// <param name="rowsString">Explicit rows string.</param>
    /// <param name="useCaps">Whether to use caption rows.</param>
    /// <returns>Array of row numbers to process.</returns>
    public static int[] DetermineRows(string? rowsString, bool useCaps)
    {
        return FilterHelpers.DetermineRows(rowsString, useCaps);
    }

    /// <summary>
    /// Determines which magazines to use based on specified magazine number.
    /// </summary>
    /// <param name="magazine">Specified magazine number or null for all magazines.</param>
    /// <returns>Array of magazine numbers to process.</returns>
    public static int[] DetermineMagazines(int? magazine)
    {
        return magazine.HasValue ? [magazine.Value] : Constants.DEFAULT_MAGAZINES;
    }

    /// <summary>
    /// Parses a string of MPEG-TS PIDs into an array of integers.
    /// </summary>
    /// <param name="pidsString">Comma-separated PID numbers (e.g., "70" or "70,71,72").</param>
    /// <returns>Array of PIDs or null if not specified.</returns>
    public static int[]? ParsePidsString(string? pidsString)
    {
        if (string.IsNullOrWhiteSpace(pidsString))
            return null;

        var pids = new List<int>();
        var parts = pidsString.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (int.TryParse(trimmed, out int pid))
            {
                if (pid >= 0 && pid <= 8191) // Valid PID range
                    pids.Add(pid);
            }
        }

        return pids.Count > 0 ? [.. pids.Distinct()] : null;
    }
}