using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.libopx.Core;

/// <summary>
/// Base class providing common I/O functionality for all format parsers.
/// Handles file and stream operations, output routing, and disposal patterns.
/// Child classes implement format-specific parsing logic and properties.
/// </summary>
public abstract class FormatIOBase : IDisposable
{
    /// <summary>
    /// Gets or sets the input file. If null, reads from stdin.
    /// </summary>
    public FileInfo? InputFile { get; set; } = null;

    /// <summary>
    /// Gets or sets the output file. If null, writes to stdout.
    /// </summary>
    public FileInfo? OutputFile { get; set; } = null;

    /// <summary>
    /// Private field for the output stream.
    /// </summary>
    private Stream? _outputStream;

    /// <summary>
    /// Gets or sets the input stream for reading data.
    /// </summary>
    public required Stream Input { get; set; }

    /// <summary>
    /// Gets the output stream for writing processed data.
    /// Lazily initialized to stdout if no OutputFile is set.
    /// </summary>
    public Stream Output => _outputStream ??= OutputFile == null
        ? Console.OpenStandardOutput()
        : OutputFile.Open(FileMode.Create, FileAccess.Write, FileShare.Read);

    /// <summary>
    /// Gets or sets the output format for processed data.
    /// </summary>
    public Format? OutputFormat { get; set; }

    /// <summary>
    /// Gets or sets the function mode for processing. Default is Filter.
    /// </summary>
    public Function Function { get; set; } = Function.Filter;

    /// <summary>
    /// Sets the output file for writing.
    /// </summary>
    /// <param name="outputFile">Path to the output file</param>
    public void SetOutput(string outputFile)
    {
        OutputFile = new FileInfo(outputFile);
    }

    /// <summary>
    /// Sets the output stream for writing.
    /// </summary>
    /// <param name="outputStream">The output stream to use</param>
    /// <exception cref="ArgumentNullException">Thrown if outputStream is null</exception>
    public void SetOutput(Stream outputStream)
    {
        OutputFile = null;
        _outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
    }

    /// <summary>
    /// Disposes the input and output streams if they were opened from files.
    /// Virtual to allow child classes to add format-specific disposal logic.
    /// </summary>
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
        if (InputFile != null)
            Input?.Dispose();
        if (OutputFile != null)
            _outputStream?.Dispose();
    }
}
