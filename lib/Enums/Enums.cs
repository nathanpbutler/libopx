using System;

namespace nathanbutlerDEV.libopx.Enums;

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
    /// ANC (Ancillary Data - extracted MXF data stream, use ANC class)
    ANC,
    /// MXF
    MXF,
    /// RCWT (Raw Captions With Time)
    RCWT,
    /// STL (EBU-t3264 Subtitle Exchange Format)
    STL,
    /// TS (MPEG Transport Stream)
    TS,
    /// TODO: mp4, etc...
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