using System;

namespace nathanbutlerDEV.libopx.Enums;

/// <summary>
/// The format of the Line in a Packet.
/// This is used to determine how the line should be processed or displayed.
/// </summary>
[Obsolete("Use Format enum instead")]
public enum LineFormat
{
    /// VBI
    VBI,
    /// VBI (double)
    VBI_DOUBLE,
    /// T42
    T42,
    /// BIN (MXF data)
    BIN,
    /// RCWT (Raw Caption With Timing)
    RCWT,
    /// TODO: mp4, ts, mpeg, etc...
    /// None
    Unknown
}

/// <summary>
/// The format of the Line in a Packet.
/// This is used to determine how the line should be processed or displayed.
/// </summary>
public enum Format
{
    /// VBI
    VBI,
    /// VBI (double)
    VBI_DOUBLE,
    /// T42
    T42,
    /// BIN (MXF data)
    BIN,
    /// MXF
    MXF,
    /// RCWT (Raw Caption With Timing)
    RCWT,
    /// STL (EBU-t3264 Subtitle Exchange Format)
    STL,
    /// TODO: mp4, ts, mpeg, etc...
    /// None
    Unknown
}

/// <summary>
/// The task to perform.
/// </summary>
public enum Function
{
    /// Extract
    Extract,
    /// Filter (outputs OP-42/OP-47 teletext data to console)
    Filter,
    /// Restripe (rewrites MXF with new timecodes)
    Restripe
}