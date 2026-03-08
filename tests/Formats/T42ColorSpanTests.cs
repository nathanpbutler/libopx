using nathanbutlerDEV.libopx.Formats;
using nathanbutlerDEV.libopx.Models;

namespace nathanbutlerDEV.libopx.Tests.Formats;

public class T42ColorSpanTests
{
    [Fact]
    public void GetColorSpans_EmptyBytes_ReturnsEmptyList()
    {
        var result = T42.GetColorSpans([], false);
        Assert.Empty(result);
    }

    [Fact]
    public void GetColorSpans_HeaderRow_ReturnsPageNumberAndText()
    {
        byte[] sampleLine = T42.Sample.Take(42).Skip(2).ToArray();
        var spans = T42.GetColorSpans(sampleLine, isHeaderRow: true, magazine: 8, pageNumber: "01");
        Assert.NotEmpty(spans);
        Assert.Contains("P801", spans[0].Text);
        Assert.Equal(TeletextColor.White, spans[0].Foreground);
        Assert.Equal(TeletextColor.Black, spans[0].Background);
    }

    [Fact]
    public void GetColorSpans_DataRow_DefaultsWhiteOnBlack()
    {
        byte[] data = Enumerable.Repeat((byte)0xC1, 40).ToArray();
        var spans = T42.GetColorSpans(data, isHeaderRow: false);
        Assert.NotEmpty(spans);
        foreach (var span in spans)
        {
            Assert.Equal(TeletextColor.White, span.Foreground);
            Assert.Equal(TeletextColor.Black, span.Background);
        }
    }

    [Fact]
    public void GetColorSpans_WithColorControlCode_ChangesColor()
    {
        byte[] data = new byte[40];
        data[0] = 0x02; // Green foreground control code
        for (int i = 1; i < 40; i++)
            data[i] = 0x41;
        var spans = T42.GetColorSpans(data, isHeaderRow: false);
        Assert.NotEmpty(spans);
        var greenSpans = spans.Where(s => s.Foreground == TeletextColor.Green && s.Text.Contains('A'));
        Assert.NotEmpty(greenSpans);
    }
}
