using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.libopx.Core;

/// <summary>
/// Configuration options for format parsing operations.
/// Provides filtering and output configuration for format handlers.
/// </summary>
public class ParseOptions
{
    /// <summary>
    /// Gets or sets the optional magazine number filter for teletext data.
    /// When null, all magazines are included.
    /// </summary>
    public int? Magazine { get; set; } = null;

    /// <summary>
    /// Gets or sets the optional array of row numbers to filter.
    /// When null, uses default rows from Constants.DEFAULT_ROWS.
    /// </summary>
    public int[]? Rows { get; set; } = null;

    /// <summary>
    /// Gets or sets the page number filter for teletext data.
    /// Supports 2-digit hex (e.g., "01") or 3-digit with magazine (e.g., "801").
    /// When specified with 3 digits via ParsePageNumber, magazine component overrides Magazine property.
    /// </summary>
    public string? PageNumber { get; set; } = null;

    /// <summary>
    /// Gets or sets whether to use caption row filtering (1-24) with content filtering.
    /// When true, filters to rows 1-24 AND excludes rows with only spaces/control codes.
    /// </summary>
    public bool UseCaps { get; set; } = false;

    /// <summary>
    /// Gets or sets the desired output format.
    /// Determines how the parsed data should be converted.
    /// </summary>
    public Format OutputFormat { get; set; } = Format.T42;

    /// <summary>
    /// Gets or sets the number of lines per frame for timecode incrementation.
    /// Default is 2 lines per frame.
    /// </summary>
    public int LineCount { get; set; } = 2;

    /// <summary>
    /// Gets or sets the starting timecode for packet numbering.
    /// Used by ANC/MXF packet-based formats. When null, defaults to 00:00:00:00.
    /// </summary>
    public Timecode? StartTimecode { get; set; } = null;

    /// <summary>
    /// Gets or sets the PIDs to filter for TS format.
    /// When null, auto-detection is used. TS-specific option.
    /// </summary>
    public int[]? PIDs { get; set; } = null;

    /// <summary>
    /// Gets or sets whether to preserve blank bytes for filtered-out rows.
    /// When true, filtered rows are replaced with blank bytes instead of being omitted.
    /// This preserves stream structure and byte alignment.
    /// </summary>
    public bool KeepBlanks { get; set; } = false;

    #region MXF-Specific Options

    /// <summary>
    /// Gets or sets whether to extract all keys to separate files (demux mode).
    /// MXF-specific option for extraction operations.
    /// </summary>
    public bool DemuxMode { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to use key names instead of hex identifiers for output files.
    /// MXF-specific option, only applies when DemuxMode is true.
    /// </summary>
    public bool UseKeyNames { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to include KLV headers in extracted files.
    /// MXF-specific option for extraction operations.
    /// </summary>
    public bool KlvMode { get; set; } = false;

    /// <summary>
    /// Gets or sets the key types to extract from MXF files.
    /// MXF-specific option. When null, defaults based on DemuxMode.
    /// </summary>
    public List<KeyType>? RequiredKeys { get; set; } = null;

    /// <summary>
    /// Gets or sets whether to print progress during operations.
    /// Useful for long-running MXF operations like restripe.
    /// </summary>
    public bool PrintProgress { get; set; } = false;

    /// <summary>
    /// Gets or sets whether verbose output is enabled.
    /// </summary>
    public bool Verbose { get; set; } = false;

    #endregion

    /// <summary>
    /// Creates a new instance of ParseOptions with default values.
    /// </summary>
    public ParseOptions()
    {
    }

    /// <summary>
    /// Creates a new instance of ParseOptions with specified values.
    /// </summary>
    /// <param name="outputFormat">The desired output format</param>
    /// <param name="magazine">Optional magazine filter</param>
    /// <param name="rows">Optional row filters</param>
    /// <param name="lineCount">Lines per frame for timecode incrementation</param>
    public ParseOptions(Format outputFormat, int? magazine = null, int[]? rows = null, int lineCount = 2)
    {
        OutputFormat = outputFormat;
        Magazine = magazine;
        Rows = rows;
        LineCount = lineCount;
    }

    /// <summary>
    /// Creates a copy of this ParseOptions instance.
    /// </summary>
    /// <returns>A new ParseOptions instance with the same values</returns>
    public ParseOptions Clone()
    {
        return new ParseOptions
        {
            Magazine = Magazine,
            Rows = Rows?.ToArray(),
            PageNumber = PageNumber,
            UseCaps = UseCaps,
            OutputFormat = OutputFormat,
            LineCount = LineCount,
            StartTimecode = StartTimecode,
            PIDs = PIDs?.ToArray(),
            KeepBlanks = KeepBlanks,
            DemuxMode = DemuxMode,
            UseKeyNames = UseKeyNames,
            KlvMode = KlvMode,
            RequiredKeys = RequiredKeys?.ToList(),
            PrintProgress = PrintProgress,
            Verbose = Verbose
        };
    }
}
