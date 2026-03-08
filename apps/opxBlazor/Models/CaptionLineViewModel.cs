using nathanbutlerDEV.libopx.Models;

namespace opxBlazor.Models;

/// <summary>
/// View model representing a single caption line for display in the filter results grid.
/// </summary>
public class CaptionLineViewModel
{
    public string Timecode { get; init; } = "";
    public int Magazine { get; init; }
    public int Row { get; init; }
    public List<ColorSpan> ColorSpans { get; init; } = [];
}
