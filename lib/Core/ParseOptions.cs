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
            OutputFormat = OutputFormat,
            LineCount = LineCount,
            StartTimecode = StartTimecode,
            PIDs = PIDs?.ToArray()
        };
    }
}
