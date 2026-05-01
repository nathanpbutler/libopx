using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Models;

namespace libopx.Tests;

public class LineColorSpanTests
{
    [Fact]
    public void Line_ParseLine_PopulatesColorSpans()
    {
        byte[] t42Line = nathanbutlerDEV.libopx.Formats.T42.Sample.Take(42).ToArray();
        var line = new Line();
        line.ParseLine(t42Line, Format.T42);

        Assert.NotNull(line.ColorSpans);
        Assert.NotEmpty(line.ColorSpans);
    }

    [Fact]
    public void Line_ParseLine_UnknownFormat_ReturnsEmptyColorSpans()
    {
        var line = new Line();
        Assert.NotNull(line.ColorSpans);
        Assert.Empty(line.ColorSpans);
    }
}
