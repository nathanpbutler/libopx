using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.libopx.Core;

/// <summary>
/// Interface for format-specific parsing handlers.
/// Defines the contract for parsing different teletext and video formats
/// with support for both synchronous and asynchronous operations.
/// </summary>
public interface IFormatHandler
{
    /// <summary>
    /// Gets the input format that this handler processes.
    /// </summary>
    Format InputFormat { get; }

    /// <summary>
    /// Gets the array of valid output formats supported by this handler.
    /// </summary>
    Format[] ValidOutputs { get; }

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
        [EnumeratorCancellation] CancellationToken cancellationToken = default);
}
