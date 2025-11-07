using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Core;

/// <summary>
/// Unit tests for the ParseOptions class.
/// Tests configuration options for format parsing operations.
/// </summary>
public class ParseOptionsTests
{
    [Fact]
    public void Constructor_Default_SetsDefaultValues()
    {
        // Arrange & Act
        var options = new ParseOptions();

        // Assert
        Assert.Null(options.Magazine);
        Assert.Null(options.Rows);
        Assert.Equal(Format.T42, options.OutputFormat);
        Assert.Equal(2, options.LineCount);
    }

    [Fact]
    public void Constructor_WithParameters_SetsValues()
    {
        // Arrange
        var magazine = 1;
        var rows = new[] { 1, 2, 3 };
        var outputFormat = Format.VBI;
        var lineCount = 4;

        // Act
        var options = new ParseOptions(outputFormat, magazine, rows, lineCount);

        // Assert
        Assert.Equal(magazine, options.Magazine);
        Assert.Equal(rows, options.Rows);
        Assert.Equal(outputFormat, options.OutputFormat);
        Assert.Equal(lineCount, options.LineCount);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        // Arrange
        var options = new ParseOptions();
        var magazine = 2;
        var rows = new[] { 5, 6, 7 };
        var outputFormat = Format.VBI_DOUBLE;
        var lineCount = 3;

        // Act
        options.Magazine = magazine;
        options.Rows = rows;
        options.OutputFormat = outputFormat;
        options.LineCount = lineCount;

        // Assert
        Assert.Equal(magazine, options.Magazine);
        Assert.Equal(rows, options.Rows);
        Assert.Equal(outputFormat, options.OutputFormat);
        Assert.Equal(lineCount, options.LineCount);
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        // Arrange
        var original = new ParseOptions
        {
            Magazine = 1,
            Rows = new[] { 1, 2, 3 },
            OutputFormat = Format.VBI,
            LineCount = 4
        };

        // Act
        var clone = original.Clone();

        // Modify the clone
        clone.Magazine = 2;
        clone.Rows = new[] { 4, 5, 6 };
        clone.OutputFormat = Format.T42;
        clone.LineCount = 5;

        // Assert - original should be unchanged
        Assert.Equal(1, original.Magazine);
        Assert.Equal(new[] { 1, 2, 3 }, original.Rows);
        Assert.Equal(Format.VBI, original.OutputFormat);
        Assert.Equal(4, original.LineCount);

        // Assert - clone should have new values
        Assert.Equal(2, clone.Magazine);
        Assert.Equal([4, 5, 6], clone.Rows);
        Assert.Equal(Format.T42, clone.OutputFormat);
        Assert.Equal(5, clone.LineCount);
    }

    [Fact]
    public void Clone_WithNullRows_WorksCorrectly()
    {
        // Arrange
        var original = new ParseOptions
        {
            Magazine = 1,
            Rows = null,
            OutputFormat = Format.VBI,
            LineCount = 4
        };

        // Act
        var clone = original.Clone();

        // Assert
        Assert.Null(clone.Rows);
        Assert.Equal(original.Magazine, clone.Magazine);
        Assert.Equal(original.OutputFormat, clone.OutputFormat);
        Assert.Equal(original.LineCount, clone.LineCount);
    }

    [Fact]
    public void Clone_RowsArrayIsIndependent()
    {
        // Arrange
        var rows = new[] { 1, 2, 3 };
        var original = new ParseOptions { Rows = rows };

        // Act
        var clone = original.Clone();
        clone.Rows![0] = 99; // Modify the cloned array

        // Assert - original array should be unchanged
        Assert.Equal(1, original.Rows![0]);
        Assert.Equal(99, clone.Rows[0]);
    }
}
