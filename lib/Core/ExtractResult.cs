namespace nathanbutlerDEV.libopx.Core;

/// <summary>
/// Contains the results of an MXF essence extraction operation.
/// </summary>
public class ExtractResult
{
    /// <summary>
    /// Dictionary mapping KeyType to extracted file paths.
    /// Used when extracting specific key types.
    /// </summary>
    public Dictionary<KeyType, string> ExtractedFiles { get; } = new();

    /// <summary>
    /// Dictionary mapping hex key identifiers to file paths.
    /// Used in demux mode when extracting all discovered keys.
    /// </summary>
    public Dictionary<string, string> DemuxedFiles { get; } = new();

    /// <summary>
    /// Whether the extraction completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if extraction failed, or null if successful.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Total number of bytes extracted across all files.
    /// </summary>
    public long TotalBytesExtracted { get; set; }

    /// <summary>
    /// Gets the total number of files extracted.
    /// </summary>
    public int FileCount => ExtractedFiles.Count + DemuxedFiles.Count;
}
