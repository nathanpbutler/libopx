namespace nathanbutlerDEV.libopx.Models;

/// <summary>
/// Represents a span of teletext text with associated foreground and background colors.
/// Used for structured rendering of teletext content in UI applications.
/// </summary>
/// <param name="Text">The text content of this span</param>
/// <param name="Foreground">The foreground (text) color</param>
/// <param name="Background">The background color</param>
public record ColorSpan(string Text, TeletextColor Foreground, TeletextColor Background = TeletextColor.Black);
