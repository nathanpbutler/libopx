using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.libopx.Core;

/// <summary>
/// Interface for packet-based format handlers (TS, MXF, ANC).
/// Defines the contract for parsing formats that produce Packet objects
/// rather than Line objects.
/// </summary>
public interface IPacketFormatHandler
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
    /// Parses the input stream and returns an enumerable of packets with optional filtering.
    /// </summary>
    /// <param name="inputStream">The stream to read format data from</param>
    /// <param name="options">Parsing options including filters and output format</param>
    /// <returns>An enumerable of parsed packets matching the filter criteria</returns>
    IEnumerable<Packet> Parse(Stream inputStream, ParseOptions options);

    /// <summary>
    /// Asynchronously parses the input stream and returns an async enumerable of packets with optional filtering.
    /// Provides better performance for large files with non-blocking I/O operations.
    /// </summary>
    /// <param name="inputStream">The stream to read format data from</param>
    /// <param name="options">Parsing options including filters and output format</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>An async enumerable of parsed packets matching the filter criteria</returns>
    IAsyncEnumerable<Packet> ParseAsync(
        Stream inputStream,
        ParseOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);
}
