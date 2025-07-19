//	Name:   Map from Teletext character sets to Unicode
//	Date:   2025 (C# port of vhs-teletext python version)
//	Original Author: Rebecca Bettencourt <support@kreativekorp.com>
//	C# Port: GitHub Copilot

using System;

namespace nathanbutlerDEV.libopx;

/// <summary>
/// Maps Teletext character sets (G0, G1, G2, G3) to Unicode characters
/// </summary>
public static class TeletextCharsets
{
    /// <summary>
    /// Teletext G0 character set mappings to Unicode
    /// </summary>
    public static readonly Dictionary<string, Dictionary<int, char>> G0 = new()
    {
        ["default"] = new Dictionary<int, char>
        {
            [0x20] = '\u0020', // SPACE
            [0x21] = '\u0021', // EXCLAMATION MARK
            [0x22] = '\u0022', // QUOTATION MARK
            [0x23] = '\u00A3', // POUND SIGN
            [0x24] = '\u0024', // DOLLAR SIGN
            [0x25] = '\u0025', // PERCENT SIGN
            [0x26] = '\u0026', // AMPERSAND
            [0x27] = '\u0027', // APOSTROPHE
            [0x28] = '\u0028', // LEFT PARENTHESIS
            [0x29] = '\u0029', // RIGHT PARENTHESIS
            [0x2A] = '\u002A', // ASTERISK
            [0x2B] = '\u002B', // PLUS SIGN
            [0x2C] = '\u002C', // COMMA
            [0x2D] = '\u002D', // HYPHEN-MINUS
            [0x2E] = '\u002E', // FULL STOP
            [0x2F] = '\u002F', // SOLIDUS
            [0x30] = '\u0030', // DIGIT ZERO
            [0x31] = '\u0031', // DIGIT ONE
            [0x32] = '\u0032', // DIGIT TWO
            [0x33] = '\u0033', // DIGIT THREE
            [0x34] = '\u0034', // DIGIT FOUR
            [0x35] = '\u0035', // DIGIT FIVE
            [0x36] = '\u0036', // DIGIT SIX
            [0x37] = '\u0037', // DIGIT SEVEN
            [0x38] = '\u0038', // DIGIT EIGHT
            [0x39] = '\u0039', // DIGIT NINE
            [0x3A] = '\u003A', // COLON
            [0x3B] = '\u003B', // SEMICOLON
            [0x3C] = '\u003C', // LESS-THAN SIGN
            [0x3D] = '\u003D', // EQUALS SIGN
            [0x3E] = '\u003E', // GREATER-THAN SIGN
            [0x3F] = '\u003F', // QUESTION MARK
            [0x40] = '\u0040', // COMMERCIAL AT
            [0x41] = '\u0041', // LATIN CAPITAL LETTER A
            [0x42] = '\u0042', // LATIN CAPITAL LETTER B
            [0x43] = '\u0043', // LATIN CAPITAL LETTER C
            [0x44] = '\u0044', // LATIN CAPITAL LETTER D
            [0x45] = '\u0045', // LATIN CAPITAL LETTER E
            [0x46] = '\u0046', // LATIN CAPITAL LETTER F
            [0x47] = '\u0047', // LATIN CAPITAL LETTER G
            [0x48] = '\u0048', // LATIN CAPITAL LETTER H
            [0x49] = '\u0049', // LATIN CAPITAL LETTER I
            [0x4A] = '\u004A', // LATIN CAPITAL LETTER J
            [0x4B] = '\u004B', // LATIN CAPITAL LETTER K
            [0x4C] = '\u004C', // LATIN CAPITAL LETTER L
            [0x4D] = '\u004D', // LATIN CAPITAL LETTER M
            [0x4E] = '\u004E', // LATIN CAPITAL LETTER N
            [0x4F] = '\u004F', // LATIN CAPITAL LETTER O
            [0x50] = '\u0050', // LATIN CAPITAL LETTER P
            [0x51] = '\u0051', // LATIN CAPITAL LETTER Q
            [0x52] = '\u0052', // LATIN CAPITAL LETTER R
            [0x53] = '\u0053', // LATIN CAPITAL LETTER S
            [0x54] = '\u0054', // LATIN CAPITAL LETTER T
            [0x55] = '\u0055', // LATIN CAPITAL LETTER U
            [0x56] = '\u0056', // LATIN CAPITAL LETTER V
            [0x57] = '\u0057', // LATIN CAPITAL LETTER W
            [0x58] = '\u0058', // LATIN CAPITAL LETTER X
            [0x59] = '\u0059', // LATIN CAPITAL LETTER Y
            [0x5A] = '\u005A', // LATIN CAPITAL LETTER Z
            [0x5B] = '\u2190', // LEFTWARDS ARROW
            [0x5C] = '\u00BD', // VULGAR FRACTION ONE HALF
            [0x5D] = '\u2192', // RIGHTWARDS ARROW
            [0x5E] = '\u2191', // UPWARDS ARROW
            [0x5F] = '\u0023', // NUMBER SIGN
            [0x60] = '\u2014', // EM DASH
            [0x61] = '\u0061', // LATIN SMALL LETTER A
            [0x62] = '\u0062', // LATIN SMALL LETTER B
            [0x63] = '\u0063', // LATIN SMALL LETTER C
            [0x64] = '\u0064', // LATIN SMALL LETTER D
            [0x65] = '\u0065', // LATIN SMALL LETTER E
            [0x66] = '\u0066', // LATIN SMALL LETTER F
            [0x67] = '\u0067', // LATIN SMALL LETTER G
            [0x68] = '\u0068', // LATIN SMALL LETTER H
            [0x69] = '\u0069', // LATIN SMALL LETTER I
            [0x6A] = '\u006A', // LATIN SMALL LETTER J
            [0x6B] = '\u006B', // LATIN SMALL LETTER K
            [0x6C] = '\u006C', // LATIN SMALL LETTER L
            [0x6D] = '\u006D', // LATIN SMALL LETTER M
            [0x6E] = '\u006E', // LATIN SMALL LETTER N
            [0x6F] = '\u006F', // LATIN SMALL LETTER O
            [0x70] = '\u0070', // LATIN SMALL LETTER P
            [0x71] = '\u0071', // LATIN SMALL LETTER Q
            [0x72] = '\u0072', // LATIN SMALL LETTER R
            [0x73] = '\u0073', // LATIN SMALL LETTER S
            [0x74] = '\u0074', // LATIN SMALL LETTER T
            [0x75] = '\u0075', // LATIN SMALL LETTER U
            [0x76] = '\u0076', // LATIN SMALL LETTER V
            [0x77] = '\u0077', // LATIN SMALL LETTER W
            [0x78] = '\u0078', // LATIN SMALL LETTER X
            [0x79] = '\u0079', // LATIN SMALL LETTER Y
            [0x7A] = '\u007A', // LATIN SMALL LETTER Z
            [0x7B] = '\u00BC', // VULGAR FRACTION ONE QUARTER
            [0x7C] = '\u2016', // DOUBLE VERTICAL LINE
            [0x7D] = '\u00BE', // VULGAR FRACTION THREE QUARTERS
            [0x7E] = '\u00F7', // DIVISION SIGN
            [0x7F] = '\u25A0', // BLACK SQUARE
        },
        ["cyr"] = new Dictionary<int, char>
        {
            [0x20] = '\u0020', // SPACE
            [0x21] = '\u0021', // EXCLAMATION MARK
            [0x22] = '\u0022', // QUOTATION MARK
            [0x23] = '\u00A3', // POUND SIGN
            [0x24] = '\u0024', // DOLLAR SIGN
            [0x25] = '\u0025', // PERCENT SIGN
            [0x26] = '\u044B', // CYRILLIC SMALL LETTER YERU
            [0x27] = '\u0027', // APOSTROPHE
            [0x28] = '\u0028', // LEFT PARENTHESIS
            [0x29] = '\u0029', // RIGHT PARENTHESIS
            [0x2A] = '\u002A', // ASTERISK
            [0x2B] = '\u002B', // PLUS SIGN
            [0x2C] = '\u002C', // COMMA
            [0x2D] = '\u002D', // HYPHEN-MINUS
            [0x2E] = '\u002E', // FULL STOP
            [0x2F] = '\u002F', // SOLIDUS
            [0x30] = '\u0030', // DIGIT ZERO
            [0x31] = '\u0031', // DIGIT ONE
            [0x32] = '\u0032', // DIGIT TWO
            [0x33] = '\u0033', // DIGIT THREE
            [0x34] = '\u0034', // DIGIT FOUR
            [0x35] = '\u0035', // DIGIT FIVE
            [0x36] = '\u0036', // DIGIT SIX
            [0x37] = '\u0037', // DIGIT SEVEN
            [0x38] = '\u0038', // DIGIT EIGHT
            [0x39] = '\u0039', // DIGIT NINE
            [0x3A] = '\u003A', // COLON
            [0x3B] = '\u003B', // SEMICOLON
            [0x3C] = '\u003C', // LESS-THAN SIGN
            [0x3D] = '\u003D', // EQUALS SIGN
            [0x3E] = '\u003E', // GREATER-THAN SIGN
            [0x3F] = '\u003F', // QUESTION MARK
            [0x40] = '\u042E', // CYRILLIC CAPITAL LETTER YU
            [0x41] = '\u0410', // CYRILLIC CAPITAL LETTER A
            [0x42] = '\u0411', // CYRILLIC CAPITAL LETTER BE
            [0x43] = '\u0426', // CYRILLIC CAPITAL LETTER TSE
            [0x44] = '\u0414', // CYRILLIC CAPITAL LETTER DE
            [0x45] = '\u0415', // CYRILLIC CAPITAL LETTER IE
            [0x46] = '\u0424', // CYRILLIC CAPITAL LETTER EF
            [0x47] = '\u0413', // CYRILLIC CAPITAL LETTER GHE
            [0x48] = '\u0425', // CYRILLIC CAPITAL LETTER HA
            [0x49] = '\u0418', // CYRILLIC CAPITAL LETTER I
            [0x4A] = '\u0419', // CYRILLIC CAPITAL LETTER SHORT I
            [0x4B] = '\u041A', // CYRILLIC CAPITAL LETTER KA
            [0x4C] = '\u041B', // CYRILLIC CAPITAL LETTER EL
            [0x4D] = '\u041C', // CYRILLIC CAPITAL LETTER EM
            [0x4E] = '\u041D', // CYRILLIC CAPITAL LETTER EN
            [0x4F] = '\u041E', // CYRILLIC CAPITAL LETTER O
            [0x50] = '\u041F', // CYRILLIC CAPITAL LETTER PE
            [0x51] = '\u042F', // CYRILLIC CAPITAL LETTER YA
            [0x52] = '\u0420', // CYRILLIC CAPITAL LETTER ER
            [0x53] = '\u0421', // CYRILLIC CAPITAL LETTER ES
            [0x54] = '\u0422', // CYRILLIC CAPITAL LETTER TE
            [0x55] = '\u0423', // CYRILLIC CAPITAL LETTER U
            [0x56] = '\u0416', // CYRILLIC CAPITAL LETTER ZHE
            [0x57] = '\u0412', // CYRILLIC CAPITAL LETTER BE
            [0x58] = '\u042C', // CYRILLIC CAPITAL LETTER SOFT SIGN
            [0x59] = '\u042A', // CYRILLIC CAPITAL LETTER HARD SIGN
            [0x5A] = '\u0417', // CYRILLIC CAPITAL LETTER ZE
            [0x5B] = '\u0428', // CYRILLIC CAPITAL LETTER SHA
            [0x5C] = '\u042D', // CYRILLIC CAPITAL LETTER E
            [0x5D] = '\u0429', // CYRILLIC CAPITAL LETTER SHCHA
            [0x5E] = '\u0427', // CYRILLIC CAPITAL LETTER CHA
            [0x5F] = '\u042B', // CYRILLIC CAPITAL LETTER YERU
            [0x60] = '\u044E', // CYRILLIC SMALL LETTER YU
            [0x61] = '\u0430', // CYRILLIC SMALL LETTER A
            [0x62] = '\u0431', // CYRILLIC SMALL LETTER BE
            [0x63] = '\u0446', // CYRILLIC SMALL LETTER TSE
            [0x64] = '\u0434', // CYRILLIC SMALL LETTER DE
            [0x65] = '\u0435', // CYRILLIC SMALL LETTER IE
            [0x66] = '\u0444', // CYRILLIC SMALL LETTER EF
            [0x67] = '\u0433', // CYRILLIC SMALL LETTER GHE
            [0x68] = '\u0445', // CYRILLIC SMALL LETTER HA
            [0x69] = '\u0438', // CYRILLIC SMALL LETTER I
            [0x6A] = '\u0439', // CYRILLIC SMALL LETTER SHORT I
            [0x6B] = '\u043A', // CYRILLIC SMALL LETTER KA
            [0x6C] = '\u043B', // CYRILLIC SMALL LETTER EL
            [0x6D] = '\u043C', // CYRILLIC SMALL LETTER EM
            [0x6E] = '\u043D', // CYRILLIC SMALL LETTER EN
            [0x6F] = '\u043E', // CYRILLIC SMALL LETTER O
            [0x70] = '\u043F', // CYRILLIC SMALL LETTER PE
            [0x71] = '\u044F', // CYRILLIC SMALL LETTER YA
            [0x72] = '\u0440', // CYRILLIC SMALL LETTER ER
            [0x73] = '\u0441', // CYRILLIC SMALL LETTER ES
            [0x74] = '\u0442', // CYRILLIC SMALL LETTER TE
            [0x75] = '\u0443', // CYRILLIC SMALL LETTER U
            [0x76] = '\u0436', // CYRILLIC SMALL LETTER ZHE
            [0x77] = '\u0432', // CYRILLIC SMALL LETTER BE
            [0x78] = '\u044C', // CYRILLIC SMALL LETTER SOFT SIGN
            [0x79] = '\u044A', // CYRILLIC SMALL LETTER HARD SIGN
            [0x7A] = '\u0437', // CYRILLIC SMALL LETTER ZE
            [0x7B] = '\u0448', // CYRILLIC SMALL LETTER SHA
            [0x7C] = '\u044D', // CYRILLIC SMALL LETTER E
            [0x7D] = '\u0449', // CYRILLIC SMALL LETTER SHCHA
            [0x7E] = '\u0447', // CYRILLIC SMALL LETTER CHE
            [0x7F] = '\u25A0', // BLACK SQUARE
        }
    };

    /// <summary>
    /// Teletext G1 character set mapping to Unicode
    /// </summary>
    public static readonly Dictionary<int, char> G1 = new()
    {
        [0x20] = '\u00A0', // NO-BREAK SPACE; unification of EMPTY BLOCK SEXTANT
        [0x21] = '\uFB00', // BLOCK SEXTANT-1
        [0x22] = '\uFB01', // BLOCK SEXTANT-2
        [0x23] = '\uFB02', // BLOCK SEXTANT-12
        [0x24] = '\uFB03', // BLOCK SEXTANT-3
        [0x25] = '\uFB04', // BLOCK SEXTANT-13
        [0x26] = '\uFB05', // BLOCK SEXTANT-23
        [0x27] = '\uFB06', // BLOCK SEXTANT-123
        [0x28] = '\uFB07', // BLOCK SEXTANT-4
        [0x29] = '\uFB08', // BLOCK SEXTANT-14
        [0x2A] = '\uFB09', // BLOCK SEXTANT-24
        [0x2B] = '\uFB0A', // BLOCK SEXTANT-124
        [0x2C] = '\uFB0B', // BLOCK SEXTANT-34
        [0x2D] = '\uFB0C', // BLOCK SEXTANT-134
        [0x2E] = '\uFB0D', // BLOCK SEXTANT-234
        [0x2F] = '\uFB0E', // BLOCK SEXTANT-1234
        [0x30] = '\uFB0F', // BLOCK SEXTANT-5
        [0x31] = '\uFB10', // BLOCK SEXTANT-15
        [0x32] = '\uFB11', // BLOCK SEXTANT-25
        [0x33] = '\uFB12', // BLOCK SEXTANT-125
        [0x34] = '\uFB13', // BLOCK SEXTANT-35
        [0x35] = '\u258C', // LEFT HALF BLOCK; unification of BLOCK SEXTANT-135
        [0x36] = '\uFB14', // BLOCK SEXTANT-235
        [0x37] = '\uFB15', // BLOCK SEXTANT-1235
        [0x38] = '\uFB16', // BLOCK SEXTANT-45
        [0x39] = '\uFB17', // BLOCK SEXTANT-145
        [0x3A] = '\uFB18', // BLOCK SEXTANT-245
        [0x3B] = '\uFB19', // BLOCK SEXTANT-1245
        [0x3C] = '\uFB1A', // BLOCK SEXTANT-345
        [0x3D] = '\uFB1B', // BLOCK SEXTANT-1345
        [0x3E] = '\uFB1C', // BLOCK SEXTANT-2345
        [0x3F] = '\uFB1D', // BLOCK SEXTANT-12345
        [0x40] = '\u0040', // COMMERCIAL AT
        [0x41] = '\u0041', // LATIN CAPITAL LETTER A
        [0x42] = '\u0042', // LATIN CAPITAL LETTER B
        [0x43] = '\u0043', // LATIN CAPITAL LETTER C
        [0x44] = '\u0044', // LATIN CAPITAL LETTER D
        [0x45] = '\u0045', // LATIN CAPITAL LETTER E
        [0x46] = '\u0046', // LATIN CAPITAL LETTER F
        [0x47] = '\u0047', // LATIN CAPITAL LETTER G
        [0x48] = '\u0048', // LATIN CAPITAL LETTER H
        [0x49] = '\u0049', // LATIN CAPITAL LETTER I
        [0x4A] = '\u004A', // LATIN CAPITAL LETTER J
        [0x4B] = '\u004B', // LATIN CAPITAL LETTER K
        [0x4C] = '\u004C', // LATIN CAPITAL LETTER L
        [0x4D] = '\u004D', // LATIN CAPITAL LETTER M
        [0x4E] = '\u004E', // LATIN CAPITAL LETTER N
        [0x4F] = '\u004F', // LATIN CAPITAL LETTER O
        [0x50] = '\u0050', // LATIN CAPITAL LETTER P
        [0x51] = '\u0051', // LATIN CAPITAL LETTER Q
        [0x52] = '\u0052', // LATIN CAPITAL LETTER R
        [0x53] = '\u0053', // LATIN CAPITAL LETTER S
        [0x54] = '\u0054', // LATIN CAPITAL LETTER T
        [0x55] = '\u0055', // LATIN CAPITAL LETTER U
        [0x56] = '\u0056', // LATIN CAPITAL LETTER V
        [0x57] = '\u0057', // LATIN CAPITAL LETTER W
        [0x58] = '\u0058', // LATIN CAPITAL LETTER X
        [0x59] = '\u0059', // LATIN CAPITAL LETTER Y
        [0x5A] = '\u005A', // LATIN CAPITAL LETTER Z
        [0x5B] = '\u2190', // LEFTWARDS ARROW
        [0x5C] = '\u00BD', // VULGAR FRACTION ONE HALF
        [0x5D] = '\u2192', // RIGHTWARDS ARROW
        [0x5E] = '\u2191', // UPWARDS ARROW
        [0x5F] = '\u0023', // NUMBER SIGN
        [0x60] = '\uFB1E', // BLOCK SEXTANT-6
        [0x61] = '\uFB1F', // BLOCK SEXTANT-16
        [0x62] = '\uFB20', // BLOCK SEXTANT-26
        [0x63] = '\uFB21', // BLOCK SEXTANT-126
        [0x64] = '\uFB22', // BLOCK SEXTANT-36
        [0x65] = '\uFB23', // BLOCK SEXTANT-136
        [0x66] = '\uFB24', // BLOCK SEXTANT-236
        [0x67] = '\uFB25', // BLOCK SEXTANT-1236
        [0x68] = '\uFB26', // BLOCK SEXTANT-46
        [0x69] = '\uFB27', // BLOCK SEXTANT-146
        [0x6A] = '\u2590', // RIGHT HALF BLOCK; unification of BLOCK SEXTANT-246
        [0x6B] = '\uFB28', // BLOCK SEXTANT-1246
        [0x6C] = '\uFB29', // BLOCK SEXTANT-346
        [0x6D] = '\uFB2A', // BLOCK SEXTANT-1346
        [0x6E] = '\uFB2B', // BLOCK SEXTANT-2346
        [0x6F] = '\uFB2C', // BLOCK SEXTANT-12346
        [0x70] = '\uFB2D', // BLOCK SEXTANT-56
        [0x71] = '\uFB2E', // BLOCK SEXTANT-156
        [0x72] = '\uFB2F', // BLOCK SEXTANT-256
        [0x73] = '\uFB30', // BLOCK SEXTANT-1256
        [0x74] = '\uFB31', // BLOCK SEXTANT-356
        [0x75] = '\uFB32', // BLOCK SEXTANT-1356
        [0x76] = '\uFB33', // BLOCK SEXTANT-2356
        [0x77] = '\uFB34', // BLOCK SEXTANT-12356
        [0x78] = '\uFB35', // BLOCK SEXTANT-456
        [0x79] = '\uFB36', // BLOCK SEXTANT-1456
        [0x7A] = '\uFB37', // BLOCK SEXTANT-2456
        [0x7B] = '\uFB38', // BLOCK SEXTANT-12456
        [0x7C] = '\uFB39', // BLOCK SEXTANT-3456
        [0x7D] = '\uFB3A', // BLOCK SEXTANT-13456
        [0x7E] = '\uFB3B', // BLOCK SEXTANT-23456
        [0x7F] = '\u2588', // FULL BLOCK; unification of BLOCK SEXTANT-123456
    };

    /// <summary>
    /// Teletext G2 character set mapping to Unicode
    /// </summary>
    public static readonly Dictionary<int, char> G2 = new()
    {
        [0x20] = '\u0020', // SPACE
        [0x21] = '\u00A1', // INVERTED EXCLAMATION MARK
        [0x22] = '\u00A2', // CENT SIGN
        [0x23] = '\u00A3', // POUND SIGN
        [0x24] = '\u0024', // DOLLAR SIGN
        [0x25] = '\u00A5', // YEN SIGN
        [0x26] = '\u0023', // NUMBER SIGN
        [0x27] = '\u00A7', // SECTION SIGN
        [0x28] = '\u00A4', // CURRENCY SIGN
        [0x29] = '\u2018', // LEFT SINGLE QUOTATION MARK
        [0x2A] = '\u201C', // LEFT DOUBLE QUOTATION MARK
        [0x2B] = '\u00AB', // LEFT-POINTING DOUBLE ANGLE QUOTATION MARK
        [0x2C] = '\u2190', // LEFTWARDS ARROW
        [0x2D] = '\u2191', // UPWARDS ARROW
        [0x2E] = '\u2192', // RIGHTWARDS ARROW
        [0x2F] = '\u2193', // DOWNWARDS ARROW
        [0x30] = '\u00B0', // DEGREE SIGN
        [0x31] = '\u00B1', // PLUS-MINUS SIGN
        [0x32] = '\u00B2', // SUPERSCRIPT TWO
        [0x33] = '\u00B3', // SUPERSCRIPT THREE
        [0x34] = '\u00D7', // MULTIPLICATION SIGN
        [0x35] = '\u00B5', // MICRO SIGN
        [0x36] = '\u00B6', // PILCROW SIGN
        [0x37] = '\u00B7', // MIDDLE DOT
        [0x38] = '\u00F7', // DIVISION SIGN
        [0x39] = '\u2019', // RIGHT SINGLE QUOTATION MARK
        [0x3A] = '\u201D', // RIGHT DOUBLE QUOTATION MARK
        [0x3B] = '\u00BB', // RIGHT-POINTING DOUBLE ANGLE QUOTATION MARK
        [0x3C] = '\u00BC', // VULGAR FRACTION ONE QUARTER
        [0x3D] = '\u00BD', // VULGAR FRACTION ONE HALF
        [0x3E] = '\u00BE', // VULGAR FRACTION THREE QUARTERS
        [0x3F] = '\u00BF', // INVERTED QUESTION MARK
        [0x40] = '\u00A0', // NO-BREAK SPACE
        [0x41] = '\u02CB', // MODIFIER LETTER GRAVE ACCENT
        [0x42] = '\u02CA', // MODIFIER LETTER ACUTE ACCENT
        [0x43] = '\u02C6', // MODIFIER LETTER CIRCUMFLEX ACCENT
        [0x44] = '\u02DC', // SMALL TILDE
        [0x45] = '\u02C9', // MODIFIER LETTER MACRON
        [0x46] = '\u02D8', // BREVE
        [0x47] = '\u02D9', // DOT ABOVE
        [0x48] = '\u00A8', // DIAERESIS
        [0x49] = '\u02CC', // MODIFIER LETTER LOW VERTICAL LINE
        [0x4A] = '\u02DA', // RING ABOVE
        [0x4B] = '\u00B8', // CEDILLA
        [0x4C] = '\u005F', // LOW LINE
        [0x4D] = '\u02DD', // DOUBLE ACUTE ACCENT
        [0x4E] = '\u02DB', // OGONEK
        [0x4F] = '\u02C7', // CARON
        [0x50] = '\u2500', // BOX DRAWINGS LIGHT HORIZONTAL
        [0x51] = '\u00B9', // SUPERSCRIPT ONE
        [0x52] = '\u00AE', // REGISTERED SIGN
        [0x53] = '\u00A9', // COPYRIGHT SIGN
        [0x54] = '\u2122', // TRADE MARK SIGN
        [0x55] = '\u266A', // EIGHTH NOTE
        [0x56] = '\u20A0', // EURO-CURRENCY SIGN
        [0x57] = '\u2030', // PER MILLE SIGN
        [0x58] = '\u03B1', // GREEK SMALL LETTER ALPHA
        [0x5C] = '\u215B', // VULGAR FRACTION ONE EIGHTH
        [0x5D] = '\u215C', // VULGAR FRACTION THREE EIGHTHS
        [0x5E] = '\u215D', // VULGAR FRACTION FIVE EIGHTHS
        [0x5F] = '\u215E', // VULGAR FRACTION SEVEN EIGHTHS
        [0x60] = '\u03A9', // GREEK CAPITAL LETTER OMEGA
        [0x61] = '\u00C6', // LATIN CAPITAL LETTER AE
        [0x62] = '\u00D0', // LATIN CAPITAL LETTER ETH
        [0x63] = '\u00AA', // FEMININE ORDINAL INDICATOR
        [0x64] = '\u0126', // LATIN CAPITAL LETTER H WITH STROKE
        [0x66] = '\u0132', // LATIN CAPITAL LIGATURE IJ
        [0x67] = '\u013F', // LATIN CAPITAL LETTER L WITH MIDDLE DOT
        [0x68] = '\u0141', // LATIN CAPITAL LETTER L WITH STROKE
        [0x69] = '\u00D8', // LATIN CAPITAL LETTER O WITH STROKE
        [0x6A] = '\u0152', // LATIN CAPITAL LIGATURE OE
        [0x6B] = '\u00BA', // MASCULINE ORDINAL INDICATOR
        [0x6C] = '\u00DE', // LATIN CAPITAL LETTER THORN
        [0x6D] = '\u0166', // LATIN CAPITAL LETTER T WITH STROKE
        [0x6E] = '\u014A', // LATIN CAPITAL LETTER ENG
        [0x6F] = '\u0149', // LATIN SMALL LETTER N PRECEDED BY APOSTROPHE
        [0x70] = '\u0138', // LATIN SMALL LETTER KRA
        [0x71] = '\u00E6', // LATIN SMALL LETTER AE
        [0x72] = '\u0111', // LATIN SMALL LETTER D WITH STROKE
        [0x73] = '\u00F0', // LATIN SMALL LETTER ETH
        [0x74] = '\u0127', // LATIN SMALL LETTER H WITH STROKE
        [0x75] = '\u0131', // LATIN SMALL LETTER DOTLESS I
        [0x76] = '\u0133', // LATIN SMALL LIGATURE IJ
        [0x77] = '\u0140', // LATIN SMALL LETTER L WITH MIDDLE DOT
        [0x78] = '\u0142', // LATIN SMALL LETTER L WITH STROKE
        [0x79] = '\u00F8', // LATIN SMALL LETTER O WITH STROKE
        [0x7A] = '\u0153', // LATIN SMALL LIGATURE OE
        [0x7B] = '\u00DF', // LATIN SMALL LETTER SHARP S
        [0x7C] = '\u00FE', // LATIN SMALL LETTER THORN
        [0x7D] = '\u0167', // LATIN SMALL LETTER T WITH STROKE
        [0x7E] = '\u014B', // LATIN SMALL LETTER ENG
        [0x7F] = '\u25A0', // BLACK SQUARE
    };

    /// <summary>
    /// Teletext G3 character set mapping to Unicode
    /// </summary>
    public static readonly Dictionary<int, char> G3 = new()
    {
        [0x20] = '\uFB3C', // LOWER LEFT BLOCK DIAGONAL LOWER MIDDLE LEFT TO LOWER CENTRE
        [0x21] = '\uFB3D', // LOWER LEFT BLOCK DIAGONAL LOWER MIDDLE LEFT TO LOWER RIGHT
        [0x22] = '\uFB3E', // LOWER LEFT BLOCK DIAGONAL UPPER MIDDLE LEFT TO LOWER CENTRE
        [0x23] = '\uFB3F', // LOWER LEFT BLOCK DIAGONAL UPPER MIDDLE LEFT TO LOWER RIGHT
        [0x24] = '\uFB40', // LOWER LEFT BLOCK DIAGONAL UPPER LEFT TO LOWER CENTRE
        [0x25] = '\u25E3', // BLACK LOWER LEFT TRIANGLE
        [0x26] = '\uFB41', // LOWER RIGHT BLOCK DIAGONAL UPPER MIDDLE LEFT TO UPPER CENTRE
        [0x27] = '\uFB42', // LOWER RIGHT BLOCK DIAGONAL UPPER MIDDLE LEFT TO UPPER RIGHT
        [0x28] = '\uFB43', // LOWER RIGHT BLOCK DIAGONAL LOWER MIDDLE LEFT TO UPPER CENTRE
        [0x29] = '\uFB44', // LOWER RIGHT BLOCK DIAGONAL LOWER MIDDLE LEFT TO UPPER RIGHT
        [0x2A] = '\uFB45', // LOWER RIGHT BLOCK DIAGONAL LOWER LEFT TO UPPER CENTRE
        [0x2B] = '\uFB46', // LOWER RIGHT BLOCK DIAGONAL LOWER MIDDLE LEFT TO UPPER MIDDLE RIGHT
        [0x2C] = '\uFB68', // UPPER AND RIGHT AND LOWER TRIANGULAR THREE QUARTERS BLOCK
        [0x2D] = '\uFB69', // LEFT AND LOWER AND RIGHT TRIANGULAR THREE QUARTERS BLOCK
        [0x2E] = '\uFB70', // VERTICAL ONE EIGHTH BLOCK-2
        [0x2F] = '\u2592', // MEDIUM SHADE
        [0x30] = '\uFB47', // LOWER RIGHT BLOCK DIAGONAL LOWER CENTRE TO LOWER MIDDLE RIGHT
        [0x31] = '\uFB48', // LOWER RIGHT BLOCK DIAGONAL LOWER LEFT TO LOWER MIDDLE RIGHT
        [0x32] = '\uFB49', // LOWER RIGHT BLOCK DIAGONAL LOWER CENTRE TO UPPER MIDDLE RIGHT
        [0x33] = '\uFB4A', // LOWER RIGHT BLOCK DIAGONAL LOWER LEFT TO UPPER MIDDLE RIGHT
        [0x34] = '\uFB4B', // LOWER RIGHT BLOCK DIAGONAL LOWER CENTRE TO UPPER RIGHT
        [0x35] = '\u25E2', // BLACK LOWER RIGHT TRIANGLE
        [0x36] = '\uFB4C', // LOWER LEFT BLOCK DIAGONAL UPPER CENTRE TO UPPER MIDDLE RIGHT
        [0x37] = '\uFB4D', // LOWER LEFT BLOCK DIAGONAL UPPER LEFT TO UPPER MIDDLE RIGHT
        [0x38] = '\uFB4E', // LOWER LEFT BLOCK DIAGONAL UPPER CENTRE TO LOWER MIDDLE RIGHT
        [0x39] = '\uFB4F', // LOWER LEFT BLOCK DIAGONAL UPPER LEFT TO LOWER MIDDLE RIGHT
        [0x3A] = '\uFB50', // LOWER LEFT BLOCK DIAGONAL UPPER CENTRE TO LOWER RIGHT
        [0x3B] = '\uFB51', // LOWER LEFT BLOCK DIAGONAL UPPER MIDDLE LEFT TO LOWER MIDDLE RIGHT
        [0x3C] = '\uFB6A', // UPPER AND LEFT AND LOWER TRIANGULAR THREE QUARTERS BLOCK
        [0x3D] = '\uFB6B', // LEFT AND UPPER AND RIGHT TRIANGULAR THREE QUARTERS BLOCK
        [0x3E] = '\uFB75', // VERTICAL ONE EIGHTH BLOCK-7
        [0x3F] = '\u2588', // FULL BLOCK
        [0x40] = '\u2537', // BOX DRAWINGS UP LIGHT AND HORIZONTAL HEAVY
        [0x41] = '\u252F', // BOX DRAWINGS DOWN LIGHT AND HORIZONTAL HEAVY
        [0x42] = '\u251D', // BOX DRAWINGS VERTICAL LIGHT AND RIGHT HEAVY
        [0x43] = '\u2525', // BOX DRAWINGS VERTICAL LIGHT AND LEFT HEAVY
        [0x44] = '\uFBA4', // BOX DRAWINGS LIGHT DIAGONAL UPPER CENTRE TO MIDDLE LEFT TO LOWER CENTRE
        [0x45] = '\uFBA5', // BOX DRAWINGS LIGHT DIAGONAL UPPER CENTRE TO MIDDLE RIGHT TO LOWER CENTRE
        [0x46] = '\uFBA6', // BOX DRAWINGS LIGHT DIAGONAL MIDDLE LEFT TO LOWER CENTRE TO MIDDLE RIGHT
        [0x47] = '\uFBA7', // BOX DRAWINGS LIGHT DIAGONAL MIDDLE LEFT TO UPPER CENTRE TO MIDDLE RIGHT
        [0x48] = '\uFBA0', // BOX DRAWINGS LIGHT DIAGONAL UPPER CENTRE TO MIDDLE LEFT
        [0x49] = '\uFBA1', // BOX DRAWINGS LIGHT DIAGONAL UPPER CENTRE TO MIDDLE RIGHT
        [0x4A] = '\uFBA2', // BOX DRAWINGS LIGHT DIAGONAL MIDDLE LEFT TO LOWER CENTRE
        [0x4B] = '\uFBA3', // BOX DRAWINGS LIGHT DIAGONAL MIDDLE RIGHT TO LOWER CENTRE
        [0x4C] = '\u253F', // BOX DRAWINGS VERTICAL LIGHT AND HORIZONTAL HEAVY
        [0x4D] = '\u2022', // BULLET
        [0x4E] = '\u25CF', // BLACK CIRCLE
        [0x4F] = '\u25CB', // WHITE CIRCLE
        [0x50] = '\u2502', // BOX DRAWINGS LIGHT VERTICAL
        [0x51] = '\u2500', // BOX DRAWINGS LIGHT HORIZONTAL
        [0x52] = '\u250C', // BOX DRAWINGS LIGHT DOWN AND RIGHT
        [0x53] = '\u2510', // BOX DRAWINGS LIGHT DOWN AND LEFT
        [0x54] = '\u2514', // BOX DRAWINGS LIGHT UP AND RIGHT
        [0x55] = '\u2518', // BOX DRAWINGS LIGHT UP AND LEFT
        [0x56] = '\u251C', // BOX DRAWINGS LIGHT VERTICAL AND RIGHT
        [0x57] = '\u2524', // BOX DRAWINGS LIGHT VERTICAL AND LEFT
        [0x58] = '\u252C', // BOX DRAWINGS LIGHT DOWN AND HORIZONTAL
        [0x59] = '\u2534', // BOX DRAWINGS LIGHT UP AND HORIZONTAL
        [0x5A] = '\u253C', // BOX DRAWINGS LIGHT VERTICAL AND HORIZONTAL
        [0x5B] = '\u2B62', // RIGHTWARDS TRIANGLE-HEADED ARROW
        [0x5C] = '\u2B60', // LEFTWARDS TRIANGLE-HEADED ARROW
        [0x5D] = '\u2B61', // UPWARDS TRIANGLE-HEADED ARROW
        [0x5E] = '\u2B63', // DOWNWARDS TRIANGLE-HEADED ARROW
        [0x5F] = '\u00A0', // NO-BREAK SPACE
        [0x60] = '\uFB52', // UPPER RIGHT BLOCK DIAGONAL LOWER MIDDLE LEFT TO LOWER CENTRE
        [0x61] = '\uFB53', // UPPER RIGHT BLOCK DIAGONAL LOWER MIDDLE LEFT TO LOWER RIGHT
        [0x62] = '\uFB54', // UPPER RIGHT BLOCK DIAGONAL UPPER MIDDLE LEFT TO LOWER CENTRE
        [0x63] = '\uFB55', // UPPER RIGHT BLOCK DIAGONAL UPPER MIDDLE LEFT TO LOWER RIGHT
        [0x64] = '\uFB56', // UPPER RIGHT BLOCK DIAGONAL UPPER LEFT TO LOWER CENTRE
        [0x65] = '\u25E5', // BLACK UPPER RIGHT TRIANGLE
        [0x66] = '\uFB57', // UPPER LEFT BLOCK DIAGONAL UPPER MIDDLE LEFT TO UPPER CENTRE
        [0x67] = '\uFB58', // UPPER LEFT BLOCK DIAGONAL UPPER MIDDLE LEFT TO UPPER RIGHT
        [0x68] = '\uFB59', // UPPER LEFT BLOCK DIAGONAL LOWER MIDDLE LEFT TO UPPER CENTRE
        [0x69] = '\uFB5A', // UPPER LEFT BLOCK DIAGONAL LOWER MIDDLE LEFT TO UPPER RIGHT
        [0x6A] = '\uFB5B', // UPPER LEFT BLOCK DIAGONAL LOWER LEFT TO UPPER CENTRE
        [0x6B] = '\uFB5C', // UPPER LEFT BLOCK DIAGONAL LOWER MIDDLE LEFT TO UPPER MIDDLE RIGHT
        [0x6C] = '\uFB6C', // LEFT TRIANGULAR ONE QUARTER BLOCK
        [0x6D] = '\uFB6D', // UPPER TRIANGULAR ONE QUARTER BLOCK
        [0x70] = '\uFB5D', // UPPER LEFT BLOCK DIAGONAL LOWER CENTRE TO LOWER MIDDLE RIGHT
        [0x71] = '\uFB5E', // UPPER LEFT BLOCK DIAGONAL LOWER LEFT TO LOWER MIDDLE RIGHT
        [0x72] = '\uFB5F', // UPPER LEFT BLOCK DIAGONAL LOWER CENTRE TO UPPER MIDDLE RIGHT
        [0x73] = '\uFB60', // UPPER LEFT BLOCK DIAGONAL LOWER LEFT TO UPPER MIDDLE RIGHT
        [0x74] = '\uFB61', // UPPER LEFT BLOCK DIAGONAL LOWER CENTRE TO UPPER RIGHT
        [0x75] = '\u25E4', // BLACK UPPER LEFT TRIANGLE
        [0x76] = '\uFB62', // UPPER RIGHT BLOCK DIAGONAL UPPER CENTRE TO UPPER MIDDLE RIGHT
        [0x77] = '\uFB63', // UPPER RIGHT BLOCK DIAGONAL UPPER LEFT TO UPPER MIDDLE RIGHT
        [0x78] = '\uFB64', // UPPER RIGHT BLOCK DIAGONAL UPPER CENTRE TO LOWER MIDDLE RIGHT
        [0x79] = '\uFB65', // UPPER RIGHT BLOCK DIAGONAL UPPER LEFT TO LOWER MIDDLE RIGHT
        [0x7A] = '\uFB66', // UPPER RIGHT BLOCK DIAGONAL UPPER CENTRE TO LOWER RIGHT
        [0x7B] = '\uFB67', // UPPER RIGHT BLOCK DIAGONAL UPPER MIDDLE LEFT TO LOWER MIDDLE RIGHT
        [0x7C] = '\uFB6E', // RIGHT TRIANGULAR ONE QUARTER BLOCK
        [0x7D] = '\uFB6F', // LOWER TRIANGULAR ONE QUARTER BLOCK
    };

    /// <summary>
    /// Convert a teletext character code to Unicode character
    /// </summary>
    /// <param name="charset">The character set (G0, G1, G2, G3)</param>
    /// <param name="code">The character code</param>
    /// <param name="g0Variant">For G0 charset, specify "default" or "cyr" variant</param>
    /// <returns>Unicode character or null if not found</returns>
    public static char GetUnicodeChar(string charset, int code, string g0Variant = "default")
    {
        return charset.ToUpper() switch
        {
            "G0" => G0.TryGetValue(g0Variant, out Dictionary<int, char>? value) && value.ContainsKey(code) ? value[code] : '\0',
            "G1" => G1.TryGetValue(code, out char value) ? value : '\0',
            "G2" => G2.TryGetValue(code, out char value) ? value : '\0',
            "G3" => G3.TryGetValue(code, out char value) ? value : '\0',
            _ => '\0'
        };
    }
}

/*
Example usage:
var uniChar = TeletextCharsets.GetUnicodeChar("G0", 0xD5); // Returns '\u0020' (SPACE)
*/