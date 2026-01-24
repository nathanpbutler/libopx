using System;
using System.Collections.Generic;
using System.Linq;

namespace nathanbutlerDEV.libopx;

/// <summary>
/// Helper methods for parsing and processing filter parameters.
/// Provides utilities for row parsing, page number parsing, and filter configuration.
/// </summary>
public static class FilterHelpers
{
    /// <summary>
    /// Parses a page number string and extracts magazine and page components.
    /// Supports 2-digit format ("01") or 3-digit format with magazine ("801").
    /// </summary>
    /// <param name="pageString">Page number string (e.g., "01" or "801")</param>
    /// <returns>Tuple of (magazine, pageHex) where magazine is null for 2-digit format</returns>
    /// <exception cref="ArgumentException">Thrown when the page string format is invalid</exception>
    public static (int? magazine, string? pageHex) ParsePageNumber(string? pageString)
    {
        if (string.IsNullOrEmpty(pageString))
            return (null, null);

        // Remove any whitespace
        pageString = pageString.Trim();

        if (pageString.Length == 3)
        {
            // 3-digit format: "801" = magazine 8, page 01
            if (!int.TryParse(pageString[0].ToString(), out int mag) || mag < 1 || mag > 8)
            {
                throw new ArgumentException($"Invalid magazine number in page '{pageString}'. Magazine must be 1-8.");
            }

            string pageHex = pageString.Substring(1, 2).ToLowerInvariant();
            if (!IsValidHex(pageHex))
            {
                throw new ArgumentException($"Invalid page number '{pageHex}' in '{pageString}'. Page must be 00-FF (hex).");
            }

            return (mag, pageHex);
        }
        else if (pageString.Length == 2)
        {
            // 2-digit format: "01" = page 01 (any magazine)
            string pageHex = pageString.ToLowerInvariant();
            if (!IsValidHex(pageHex))
            {
                throw new ArgumentException($"Invalid page number '{pageHex}'. Page must be 00-FF (hex).");
            }

            return (null, pageHex);
        }

        throw new ArgumentException($"Invalid page number format: '{pageString}'. Use 2-digit (e.g., '01') or 3-digit (e.g., '801').");
    }

    /// <summary>
    /// Validates that a string is valid hexadecimal (00-FF).
    /// </summary>
    /// <param name="hex">The hex string to validate</param>
    /// <returns>True if valid hex between 00-FF, false otherwise</returns>
    private static bool IsValidHex(string hex)
    {
        return hex.Length == 2 &&
               int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int val) &&
               val >= 0 && val <= 255;
    }

    /// <summary>
    /// Parses a string of rows into an array of integers, handling both single numbers and ranges
    /// (e.g., "1,2,5-8,15" becomes [1, 2, 5, 6, 7, 8, 15]).
    /// </summary>
    /// <param name="rowsString">The string representation of the rows</param>
    /// <returns>An array of integers representing the parsed rows</returns>
    public static int[] ParseRowsString(string rowsString)
    {
        var rows = new List<int>();
        var parts = rowsString.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Contains('-'))
            {
                // Handle range like "5-8"
                var rangeParts = trimmed.Split('-');
                if (rangeParts.Length == 2 &&
                    int.TryParse(rangeParts[0], out int start) &&
                    int.TryParse(rangeParts[1], out int end))
                {
                    for (int i = start; i <= end; i++)
                    {
                        if (i >= 0 && i <= 31)
                            rows.Add(i);
                    }
                }
            }
            else if (int.TryParse(trimmed, out int row))
            {
                // Handle single number
                if (row >= 0 && row <= 31)
                    rows.Add(row);
            }
        }

        return [.. rows.Distinct().OrderBy(r => r)];
    }

    /// <summary>
    /// Determines which rows to use based on options.
    /// </summary>
    /// <param name="rowsString">Explicit rows string (e.g., "1,2,5-8")</param>
    /// <param name="useCaps">Whether to use caption rows (1-24) instead of default (0-31)</param>
    /// <returns>Array of row numbers to process</returns>
    public static int[] DetermineRows(string? rowsString, bool useCaps)
    {
        if (!string.IsNullOrEmpty(rowsString))
        {
            return ParseRowsString(rowsString);
        }

        return useCaps ? Constants.CAPTION_ROWS : Constants.DEFAULT_ROWS;
    }
}
