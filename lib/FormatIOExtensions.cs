using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;

namespace nathanbutlerDEV.libopx;

/// <summary>
/// Extension methods for FormatIO providing MXF-specific operations.
/// </summary>
public static class FormatIOExtensions
{
    // Store MXF-specific options using ConditionalWeakTable for non-leaking association
    private static readonly ConditionalWeakTable<FormatIO, MxfOptions> _mxfOptions = new();

    private class MxfOptions
    {
        public bool DemuxMode { get; set; }
        public bool UseKeyNames { get; set; }
        public bool KlvMode { get; set; }
        public bool PrintProgress { get; set; }
        public bool Verbose { get; set; }
        public List<KeyType> RequiredKeys { get; set; } = new();
    }

    private static MxfOptions GetOrCreateOptions(FormatIO io)
    {
        return _mxfOptions.GetOrCreateValue(io);
    }

    #region Fluent Configuration Methods

    /// <summary>
    /// Configures extraction to demux all keys to separate files.
    /// MXF-specific option.
    /// </summary>
    /// <param name="io">The FormatIO instance</param>
    /// <param name="demuxMode">Whether to enable demux mode</param>
    /// <returns>The FormatIO instance for method chaining</returns>
    public static FormatIO WithDemuxMode(this FormatIO io, bool demuxMode = true)
    {
        GetOrCreateOptions(io).DemuxMode = demuxMode;
        return io;
    }

    /// <summary>
    /// Configures extraction to use key names instead of hex identifiers for output files.
    /// MXF-specific option, only applies when DemuxMode is true.
    /// </summary>
    /// <param name="io">The FormatIO instance</param>
    /// <param name="useKeyNames">Whether to use key names</param>
    /// <returns>The FormatIO instance for method chaining</returns>
    public static FormatIO WithKeyNames(this FormatIO io, bool useKeyNames = true)
    {
        GetOrCreateOptions(io).UseKeyNames = useKeyNames;
        return io;
    }

    /// <summary>
    /// Configures extraction to include KLV headers in output files.
    /// MXF-specific option.
    /// </summary>
    /// <param name="io">The FormatIO instance</param>
    /// <param name="klvMode">Whether to include KLV headers</param>
    /// <returns>The FormatIO instance for method chaining</returns>
    public static FormatIO WithKlvMode(this FormatIO io, bool klvMode = true)
    {
        GetOrCreateOptions(io).KlvMode = klvMode;
        return io;
    }

    /// <summary>
    /// Configures which key types to extract from MXF files.
    /// MXF-specific option.
    /// </summary>
    /// <param name="io">The FormatIO instance</param>
    /// <param name="keys">Key types to extract (Data, Video, Audio, System, TimecodeComponent)</param>
    /// <returns>The FormatIO instance for method chaining</returns>
    public static FormatIO WithKeys(this FormatIO io, params KeyType[] keys)
    {
        var options = GetOrCreateOptions(io);
        options.RequiredKeys.Clear();
        options.RequiredKeys.AddRange(keys);
        return io;
    }

    /// <summary>
    /// Configures whether to print progress during MXF operations.
    /// </summary>
    /// <param name="io">The FormatIO instance</param>
    /// <param name="printProgress">Whether to print progress</param>
    /// <returns>The FormatIO instance for method chaining</returns>
    public static FormatIO WithProgress(this FormatIO io, bool printProgress = true)
    {
        GetOrCreateOptions(io).PrintProgress = printProgress;
        return io;
    }

    /// <summary>
    /// Configures verbose output for MXF operations.
    /// </summary>
    /// <param name="io">The FormatIO instance</param>
    /// <param name="verbose">Whether to enable verbose output</param>
    /// <returns>The FormatIO instance for method chaining</returns>
    public static FormatIO WithVerbose(this FormatIO io, bool verbose = true)
    {
        GetOrCreateOptions(io).Verbose = verbose;
        return io;
    }

    #endregion

    #region Extract Operations

    /// <summary>
    /// Extracts essence data from an MXF file to separate output files.
    /// The FormatIO instance must be opened from a file (not stdin/stream).
    /// </summary>
    /// <param name="io">The FormatIO instance (must be opened with MXF format from a file)</param>
    /// <param name="outputBasePath">Base path for output files (without extension). If null, uses input file path.</param>
    /// <returns>ExtractResult containing paths to extracted files and operation status</returns>
    /// <exception cref="InvalidOperationException">If FormatIO was not opened from a file or format is not MXF</exception>
    public static ExtractResult ExtractEssence(this FormatIO io, string? outputBasePath = null)
    {
        var result = new ExtractResult();

        try
        {
            // Get the input file path from FormatIO
            var inputPath = io.GetInputFilePath();
            if (string.IsNullOrEmpty(inputPath))
            {
                result.Success = false;
                result.ErrorMessage = "Extract requires FormatIO to be opened from a file. Use FormatIO.Open(filePath) instead of OpenStdin or Open(stream).";
                return result;
            }

            // Validate format is MXF
            var format = io.GetInputFormat();
            if (format != Format.MXF)
            {
                result.Success = false;
                result.ErrorMessage = $"Extract is only supported for MXF format. Current format: {format}";
                return result;
            }

            // Get MXF options
            var options = GetOrCreateOptions(io);

            // Create MXF instance for extraction
            using var mxf = new MXF(inputPath)
            {
                Verbose = options.Verbose,
                PrintProgress = options.PrintProgress,
                DemuxMode = options.DemuxMode,
                UseKeyNames = options.UseKeyNames,
                KlvMode = options.KlvMode,
                OutputBasePath = outputBasePath ?? Path.GetDirectoryName(inputPath) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(inputPath)
            };

            // Add required keys
            foreach (var key in options.RequiredKeys)
            {
                mxf.RequiredKeys.Add(key);
            }

            // Run extraction
            mxf.ExtractEssence();

            result.Success = true;

            // Note: The actual file paths depend on MXF internal logic
            // In a full implementation, MXF would report back the files it created
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Asynchronously extracts essence data from an MXF file to separate output files.
    /// </summary>
    /// <param name="io">The FormatIO instance (must be opened with MXF format from a file)</param>
    /// <param name="outputBasePath">Base path for output files (without extension)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ExtractResult containing paths to extracted files and operation status</returns>
    public static async Task<ExtractResult> ExtractEssenceAsync(this FormatIO io, string? outputBasePath = null, CancellationToken cancellationToken = default)
    {
        // For now, run synchronously on a thread pool thread
        // A full async implementation would require changes to MXF class
        return await Task.Run(() => io.ExtractEssence(outputBasePath), cancellationToken);
    }

    #endregion

    #region Restripe Operations

    /// <summary>
    /// Restripes the MXF file in-place with a new start timecode.
    /// WARNING: This modifies the source file directly.
    /// The FormatIO instance must be opened from a file (not stdin/stream).
    /// </summary>
    /// <param name="io">The FormatIO instance (must be opened with MXF format from a file)</param>
    /// <param name="newStartTimecode">New start timecode in HH:MM:SS:FF format</param>
    /// <exception cref="InvalidOperationException">If FormatIO was not opened from a file or format is not MXF</exception>
    public static void Restripe(this FormatIO io, string newStartTimecode)
    {
        // Get the input file path from FormatIO
        var inputPath = io.GetInputFilePath();
        if (string.IsNullOrEmpty(inputPath))
        {
            throw new InvalidOperationException("Restripe requires FormatIO to be opened from a file. Use FormatIO.Open(filePath) instead of OpenStdin or Open(stream).");
        }

        // Validate format is MXF
        var format = io.GetInputFormat();
        if (format != Format.MXF)
        {
            throw new InvalidOperationException($"Restripe is only supported for MXF format. Current format: {format}");
        }

        // Validate timecode format by attempting to parse it
        try
        {
            _ = new Timecode(newStartTimecode);
        }
        catch (FormatException)
        {
            throw new ArgumentException($"Invalid timecode format: {newStartTimecode}. Expected format: HH:MM:SS:FF", nameof(newStartTimecode));
        }

        // Get MXF options
        var options = GetOrCreateOptions(io);

        // Create MXF instance for restripe (need read-write access)
        using var mxf = new MXF(new FileInfo(inputPath))
        {
            Verbose = options.Verbose,
            PrintProgress = options.PrintProgress,
            Function = Function.Restripe
        };

        // Execute restripe by iterating through Parse with timecode
        foreach (var _ in mxf.Parse(startTimecode: newStartTimecode))
        {
            // Parse handles restripe internally
        }
    }

    /// <summary>
    /// Asynchronously restripes the MXF file in-place with a new start timecode.
    /// WARNING: This modifies the source file directly.
    /// </summary>
    /// <param name="io">The FormatIO instance (must be opened with MXF format from a file)</param>
    /// <param name="newStartTimecode">New start timecode in HH:MM:SS:FF format</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async Task RestripeAsync(this FormatIO io, string newStartTimecode, CancellationToken cancellationToken = default)
    {
        // Get the input file path from FormatIO
        var inputPath = io.GetInputFilePath();
        if (string.IsNullOrEmpty(inputPath))
        {
            throw new InvalidOperationException("Restripe requires FormatIO to be opened from a file. Use FormatIO.Open(filePath) instead of OpenStdin or Open(stream).");
        }

        // Validate format is MXF
        var format = io.GetInputFormat();
        if (format != Format.MXF)
        {
            throw new InvalidOperationException($"Restripe is only supported for MXF format. Current format: {format}");
        }

        // Validate timecode format by attempting to parse it
        try
        {
            _ = new Timecode(newStartTimecode);
        }
        catch (FormatException)
        {
            throw new ArgumentException($"Invalid timecode format: {newStartTimecode}. Expected format: HH:MM:SS:FF", nameof(newStartTimecode));
        }

        // Get MXF options
        var options = GetOrCreateOptions(io);

        // Create MXF instance for restripe
        using var mxf = new MXF(new FileInfo(inputPath))
        {
            Verbose = options.Verbose,
            PrintProgress = options.PrintProgress,
            Function = Function.Restripe
        };

        // Execute restripe asynchronously
        await foreach (var _ in mxf.ParseAsync(startTimecode: newStartTimecode, cancellationToken: cancellationToken))
        {
            // Parse handles restripe internally
        }
    }

    #endregion
}
