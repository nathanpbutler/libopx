namespace nathanbutlerDEV.libopx.Models;

/// <summary>
/// Represents the 8 standard teletext colors as defined by the ETS 300 706 specification.
/// Values correspond to the 3-bit color codes used in teletext control characters.
/// </summary>
public enum TeletextColor : byte
{
    /// <summary>Black (0)</summary>
    Black = 0,
    /// <summary>Red (1)</summary>
    Red = 1,
    /// <summary>Green (2)</summary>
    Green = 2,
    /// <summary>Yellow (3)</summary>
    Yellow = 3,
    /// <summary>Blue (4)</summary>
    Blue = 4,
    /// <summary>Magenta (5)</summary>
    Magenta = 5,
    /// <summary>Cyan (6)</summary>
    Cyan = 6,
    /// <summary>White (7)</summary>
    White = 7
}
