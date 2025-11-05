using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.libopx.Core;

/// <summary>
/// Base interface for all format handlers.
/// Defines common properties shared by both line-based and packet-based handlers.
/// </summary>
public interface IFormatHandlerBase
{
    /// <summary>
    /// Gets the input format that this handler processes.
    /// </summary>
    Format InputFormat { get; }

    /// <summary>
    /// Gets the array of valid output formats supported by this handler.
    /// </summary>
    Format[] ValidOutputs { get; }
}
