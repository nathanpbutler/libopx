using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.libopx.Core;

/// <summary>
/// Interface for line-based format handlers (T42, VBI).
/// Defines the contract for parsing formats that produce Line objects
/// rather than Packet objects.
/// </summary>
public interface ILineFormatHandler : IFormatHandlerBase
{
    /// <summary>
    /// Gets the length of a single line in bytes for this format.
    /// May vary based on format-specific parameters (e.g., VBI vs VBI_DOUBLE).
    /// </summary>
    int LineLength { get; }

    /// <summary>
    /// Parses the input stream and returns an enumerable of lines with optional filtering.
    /// </summary>
    /// <param name="inputStream">The stream to read format data from</param>
    /// <param name="options">Parsing options including filters and output format</param>
    /// <returns>An enumerable of parsed lines matching the filter criteria</returns>
    IEnumerable<Line> Parse(Stream inputStream, ParseOptions options);

    /// <summary>
    /// Asynchronously parses the input stream and returns an async enumerable of lines with optional filtering.
    /// Provides better performance for large files with non-blocking I/O operations.
    /// </summary>
    /// <param name="inputStream">The stream to read format data from</param>
    /// <param name="options">Parsing options including filters and output format</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>An async enumerable of parsed lines matching the filter criteria</returns>
    IAsyncEnumerable<Line> ParseAsync(
        Stream inputStream,
        ParseOptions options,
        CancellationToken cancellationToken = default);
}
