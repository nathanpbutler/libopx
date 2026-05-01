using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Enums;
using opxBlazor.Models;

namespace opxBlazor.Services;

/// <summary>
/// Service that wraps FormatIO to parse teletext files and return view models for the UI.
/// </summary>
public class FilterService
{
    public async Task<List<CaptionLineViewModel>> FilterAsync(
        string filePath,
        int? magazine,
        int[]? rows,
        string? pageNumber,
        bool useCaps,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CaptionLineViewModel>();

        using var io = FormatIO.Open(filePath);

        var filterRows = rows ?? (useCaps ? Constants.CAPTION_ROWS : Constants.DEFAULT_ROWS);
        io.Filter(magazine, filterRows)
          .WithPageNumber(pageNumber)
          .WithUseCaps(useCaps);

        var inputFormat = io.GetInputFormat();

        if (inputFormat is Format.ANC or Format.TS or Format.MXF)
        {
            await foreach (var packet in io.ParsePacketsAsync(cancellationToken))
            {
                foreach (var line in packet.Lines)
                {
                    results.Add(new CaptionLineViewModel
                    {
                        Timecode = packet.Timecode.ToString(),
                        Magazine = line.Magazine,
                        Row = line.Row,
                        ColorSpans = line.ColorSpans
                    });
                }
            }
        }
        else
        {
            await foreach (var line in io.ParseLinesAsync(cancellationToken))
            {
                results.Add(new CaptionLineViewModel
                {
                    Timecode = line.LineTimecode?.ToString() ?? line.LineNumber.ToString().PadLeft(11),
                    Magazine = line.Magazine,
                    Row = line.Row,
                    ColorSpans = line.ColorSpans
                });
            }
        }

        return results;
    }
}
