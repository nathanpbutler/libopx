using nathanbutlerDEV.libopx;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests;

/// <summary>
/// Unit tests for the FilterHelpers class.
/// Tests page number parsing, row parsing, and filter determination logic.
/// </summary>
public class FilterHelpersTests
{
    #region ParsePageNumber Tests

    [Theory]
    [InlineData("01", null, "01")]           // 2-digit: any magazine, page 01
    [InlineData("ff", null, "ff")]           // 2-digit: any magazine, page ff
    [InlineData("00", null, "00")]           // 2-digit: any magazine, page 00
    [InlineData("801", 8, "01")]             // 3-digit: magazine 8, page 01
    [InlineData("1ff", 1, "ff")]             // 3-digit: magazine 1, page ff
    [InlineData("350", 3, "50")]             // 3-digit: magazine 3, page 50
    [InlineData("7AB", 7, "ab")]             // 3-digit: magazine 7, page AB (uppercase converted)
    [InlineData("2De", 2, "de")]             // 3-digit: magazine 2, page De (mixed case)
    public void ParsePageNumber_ValidFormats_ReturnsCorrectValues(
        string input, int? expectedMag, string expectedPage)
    {
        // Act
        var (mag, page) = FilterHelpers.ParsePageNumber(input);

        // Assert
        Assert.Equal(expectedMag, mag);
        Assert.Equal(expectedPage, page);
    }

    [Theory]
    [InlineData("0")]                        // Too short
    [InlineData("1234")]                     // Too long
    [InlineData("8100")]                     // Too long (4 digits)
    [InlineData("901")]                      // Invalid magazine (9)
    [InlineData("001")]                      // Invalid magazine (0)
    [InlineData("8GG")]                      // Invalid hex in page
    [InlineData("XY")]                       // Invalid hex (2-digit)
    [InlineData("1XY")]                      // Invalid hex (3-digit)
    [InlineData("8-1")]                      // Contains dash
    public void ParsePageNumber_InvalidFormats_ThrowsException(string input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => FilterHelpers.ParsePageNumber(input));
    }

    [Fact]
    public void ParsePageNumber_NullInput_ReturnsNulls()
    {
        // Act
        var (mag, page) = FilterHelpers.ParsePageNumber(null);

        // Assert
        Assert.Null(mag);
        Assert.Null(page);
    }

    [Fact]
    public void ParsePageNumber_EmptyString_ReturnsNulls()
    {
        // Act
        var (mag, page) = FilterHelpers.ParsePageNumber("");

        // Assert
        Assert.Null(mag);
        Assert.Null(page);
    }

    [Theory]
    [InlineData("  01  ", null, "01")]       // Whitespace trimmed
    [InlineData(" 801 ", 8, "01")]           // Whitespace trimmed (3-digit)
    public void ParsePageNumber_WhitespaceInput_TrimsAndParses(
        string input, int? expectedMag, string expectedPage)
    {
        // Act
        var (mag, page) = FilterHelpers.ParsePageNumber(input);

        // Assert
        Assert.Equal(expectedMag, mag);
        Assert.Equal(expectedPage, page);
    }

    [Theory]
    [InlineData("901", "Magazine must be 1-8")]
    [InlineData("001", "Magazine must be 1-8")]
    public void ParsePageNumber_InvalidMagazine_ThrowsWithMessage(string input, string expectedMessage)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => FilterHelpers.ParsePageNumber(input));
        Assert.Contains(expectedMessage, ex.Message);
    }

    #endregion

    #region ParseRowsString Tests

    [Theory]
    [InlineData("1", new[] { 1 })]
    [InlineData("1,2,3", new[] { 1, 2, 3 })]
    [InlineData("5-8", new[] { 5, 6, 7, 8 })]
    [InlineData("1,5-8,15", new[] { 1, 5, 6, 7, 8, 15 })]
    [InlineData("0,31", new[] { 0, 31 })]
    [InlineData("20-24", new[] { 20, 21, 22, 23, 24 })]
    public void ParseRowsString_ValidInput_ReturnsCorrectRows(string input, int[] expected)
    {
        // Act
        var result = FilterHelpers.ParseRowsString(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("3,1,2", new[] { 1, 2, 3 })]           // Out of order, gets sorted
    [InlineData("1,1,1", new[] { 1 })]                 // Duplicates removed
    [InlineData("1,2-4,3", new[] { 1, 2, 3, 4 })]      // Overlapping ranges deduplicated
    public void ParseRowsString_UnorderedOrDuplicates_ReturnsSortedUnique(string input, int[] expected)
    {
        // Act
        var result = FilterHelpers.ParseRowsString(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("32")]                                  // Out of range (0-31)
    [InlineData("0,32")]
    [InlineData("-1")]
    [InlineData("1,50")]
    public void ParseRowsString_OutOfRange_ExcludesInvalidRows(string input)
    {
        // Act
        var result = FilterHelpers.ParseRowsString(input);

        // Assert
        Assert.All(result, row =>
        {
            Assert.InRange(row, 0, 31);
        });
    }

    [Theory]
    [InlineData("  1 , 2 , 3  ")]                      // Whitespace around commas
    [InlineData(" 5 - 8 ")]                            // Whitespace around range
    [InlineData("1,  2,3")]                            // Mixed whitespace
    public void ParseRowsString_Whitespace_TrimsCorrectly(string input)
    {
        // Act
        var result = FilterHelpers.ParseRowsString(input);

        // Assert
        Assert.NotEmpty(result);
        Assert.All(result, row => Assert.InRange(row, 0, 31));
    }

    [Theory]
    [InlineData("abc")]                                 // Invalid number
    [InlineData("1,abc,3")]                             // Mixed valid/invalid
    [InlineData("1-abc")]                               // Invalid range end
    public void ParseRowsString_InvalidNumbers_SkipsInvalid(string input)
    {
        // Act
        var result = FilterHelpers.ParseRowsString(input);

        // Assert - Should only contain valid parsed numbers
        Assert.All(result, row => Assert.InRange(row, 0, 31));
    }

    #endregion

    #region DetermineRows Tests

    [Fact]
    public void DetermineRows_NoRowsString_NoCaps_ReturnsDefaultRows()
    {
        // Act
        var result = FilterHelpers.DetermineRows(null, useCaps: false);

        // Assert
        Assert.Equal(Constants.DEFAULT_ROWS, result);
    }

    [Fact]
    public void DetermineRows_NoRowsString_WithCaps_ReturnsCaptionRows()
    {
        // Act
        var result = FilterHelpers.DetermineRows(null, useCaps: true);

        // Assert
        Assert.Equal(Constants.CAPTION_ROWS, result);
    }

    [Theory]
    [InlineData("1,2,3", false)]
    [InlineData("1,2,3", true)]
    public void DetermineRows_WithRowsString_ReturnsCustomRows_IgnoresCapsFlag(string rowsString, bool useCaps)
    {
        // Arrange
        var expected = new[] { 1, 2, 3 };

        // Act
        var result = FilterHelpers.DetermineRows(rowsString, useCaps);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void DetermineRows_EmptyRowsString_NoCaps_ReturnsDefaultRows()
    {
        // Act
        var result = FilterHelpers.DetermineRows("", useCaps: false);

        // Assert
        Assert.Equal(Constants.DEFAULT_ROWS, result);
    }

    [Fact]
    public void DetermineRows_EmptyRowsString_WithCaps_ReturnsCaptionRows()
    {
        // Act
        var result = FilterHelpers.DetermineRows("", useCaps: true);

        // Assert
        Assert.Equal(Constants.CAPTION_ROWS, result);
    }

    #endregion
}
