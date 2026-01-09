using System.Text.RegularExpressions;
using nathanbutlerDEV.libopx.Enums;

namespace nathanbutlerDEV.libopx.Exporters;

/// <summary>
/// Exports teletext data to EBU STL format with intelligent subtitle merging.
/// Tracks content across frames to detect text changes, buildup, and clearance,
/// producing properly-timed subtitles instead of per-frame entries.
/// </summary>
public partial class STLExporter : IDisposable
{
    /// <summary>
    /// Magazine number to filter by (null = no filter, accepts all magazines)
    /// </summary>
    public int? Magazine { get; set; }

    /// <summary>
    /// Rows to include in output (default: caption rows 1-24, excludes row 0 headers)
    /// </summary>
    public int[] Rows { get; set; } = Constants.CAPTION_ROWS;

    /// <summary>
    /// Enable verbose debug output
    /// </summary>
    public bool Verbose { get; set; }

    // Content-based tracking: normalized text -> tracking info
    private readonly Dictionary<string, TrackedContent> _contentTracker = [];

    // Pending clear buffer: holds recently-cleared content to check for delayed text growth
    // Key = normalized text, Value = (TrackedContent, clearTimecode, framesWaited)
    private readonly Dictionary<string, PendingClear> _pendingClears = [];

    private int _subtitleNumber = 1;
    private Timecode? _lastTimecode;
    private bool _disposed;

    /// <summary>
    /// Number of frames to wait before emitting a cleared subtitle.
    /// This allows detection of text growth across frame gaps.
    /// </summary>
    public int ClearDelayFrames { get; set; } = 30;  // ~1.2 seconds at 25fps

    private class PendingClear
    {
        public TrackedContent Content { get; set; } = null!;
        public Timecode ClearedAt { get; set; } = new(0);
        public int FramesWaited { get; set; }
    }

    /// <summary>
    /// Tracks content across frames for intelligent subtitle merging.
    /// </summary>
    private class TrackedContent
    {
        /// <summary>Current row position (may shift as new content arrives)</summary>
        public int CurrentRow { get; set; }

        /// <summary>Normalized text for comparison</summary>
        public string Text { get; set; } = "";

        /// <summary>Raw T42 data preserving control codes for STL output</summary>
        public byte[] T42Data { get; set; } = [];

        /// <summary>Timecode when this content first appeared</summary>
        public Timecode FirstSeenAt { get; set; } = new(0);

        /// <summary>Timecode when content was last seen (for end time)</summary>
        public Timecode LastSeenAt { get; set; } = new(0);
    }

    /// <summary>
    /// Processes packets and yields STL TTI blocks when subtitles are finalized.
    /// </summary>
    /// <param name="packets">Input packets from teletext parser</param>
    /// <returns>Enumerable of TTI block byte arrays (128 bytes each)</returns>
    public IEnumerable<byte[]> Export(IEnumerable<Packet> packets)
    {
        foreach (var packet in packets)
        {
            foreach (var block in ProcessPacket(packet))
            {
                yield return block;
            }
        }
    }

    /// <summary>
    /// Async version of Export for streaming scenarios.
    /// </summary>
    public async IAsyncEnumerable<byte[]> ExportAsync(
        IAsyncEnumerable<Packet> packets,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var packet in packets.WithCancellation(cancellationToken))
        {
            foreach (var block in ProcessPacket(packet))
            {
                yield return block;
            }
        }
    }

    /// <summary>
    /// Flushes any remaining tracked content as final subtitles.
    /// Call this at end of stream to emit pending subtitles.
    /// </summary>
    /// <returns>TTI blocks for any remaining content</returns>
    public IEnumerable<byte[]> Flush()
    {
        var endTimecode = _lastTimecode ?? new Timecode(0);

        // Emit any pending clears first
        foreach (var (text, pending) in _pendingClears)
        {
            if (Verbose)
            {
                Console.Error.WriteLine($"DEBUG: Flushing pending clear: \"{text}\" ({pending.Content.FirstSeenAt} -> {pending.ClearedAt})");
            }

            yield return CreateTTIBlock(pending.Content, pending.ClearedAt);
        }
        _pendingClears.Clear();

        // Then emit any still-active content
        foreach (var kvp in _contentTracker)
        {
            var tracked = kvp.Value;
            if (Verbose)
            {
                Console.Error.WriteLine($"DEBUG: Flushing active content: \"{tracked.Text}\" ({tracked.FirstSeenAt} -> {endTimecode})");
            }

            yield return CreateTTIBlock(tracked, endTimecode);
        }
        _contentTracker.Clear();
    }

    /// <summary>
    /// Creates the GSI (General Subtitle Information) header block.
    /// </summary>
    /// <returns>1024-byte GSI block</returns>
    public static byte[] CreateGSIHeader()
    {
        var gsi = new byte[Constants.STL_GSI_BLOCK_SIZE];

        // Code Page Number (CPN) - bytes 0-2: "437" (DOS Latin)
        "437"u8.CopyTo(gsi);

        // Disk Format Code (DFC) - bytes 3-10: "STL25.01"
        "STL25.01"u8.CopyTo(gsi.AsSpan(3));

        // Display Standard Code (DSC) - byte 11: "1" (Open subtitles)
        gsi[11] = (byte)'1';

        // Character Code Table (CCT) - bytes 12-13: "00" (Latin)
        "00"u8.CopyTo(gsi.AsSpan(12));

        // Language Code (LC) - bytes 14-15: "EN"
        "EN"u8.CopyTo(gsi.AsSpan(14));

        // Original Programme Title (OPT) - bytes 16-47: 32 chars padded
        var title = "libopx teletext conversion".PadRight(32);
        System.Text.Encoding.ASCII.GetBytes(title).AsSpan(0, 32).CopyTo(gsi.AsSpan(16));

        // Fill remaining with spaces
        for (int i = 48; i < Constants.STL_GSI_BLOCK_SIZE; i++)
        {
            gsi[i] = 0x20;
        }

        // Total Number of Subtitles (TNS) - bytes 225-229: "00001" (placeholder)
        "00001"u8.CopyTo(gsi.AsSpan(225));

        // Total Number of Subtitle Groups (TNG) - bytes 230-232: "001"
        "001"u8.CopyTo(gsi.AsSpan(230));

        // Maximum Number of Displayable Characters (MNC) - bytes 233-234: "40"
        "40"u8.CopyTo(gsi.AsSpan(233));

        // Maximum Number of Displayable Rows (MNR) - bytes 235-236: "23"
        "23"u8.CopyTo(gsi.AsSpan(235));

        // Time Code Status (TCS) - byte 237: "1" (intended)
        gsi[237] = (byte)'1';

        // Time Code Start-of-Programme (TCP) - bytes 238-245: "00000000"
        "00000000"u8.CopyTo(gsi.AsSpan(238));

        // Time Code First In-Cue (TCF) - bytes 246-253: "00000000"
        "00000000"u8.CopyTo(gsi.AsSpan(246));

        // Total Number of Disks (TND) - byte 254: "1"
        gsi[254] = (byte)'1';

        // Disk Sequence Number (DSN) - byte 255: "1"
        gsi[255] = (byte)'1';

        return gsi;
    }

    /// <summary>
    /// Process a single packet and emit any finalized subtitles.
    /// </summary>
    private IEnumerable<byte[]> ProcessPacket(Packet packet)
    {
        var currentTimecode = packet.Timecode;
        _lastTimecode = currentTimecode;

        // Collect current frame's content (normalized text -> line)
        var currentFrameContent = new Dictionary<string, Line>();

        foreach (var line in packet.Lines)
        {
            // Skip row 0 (headers) and rows not in our filter
            if (line.Row == 0 || !Rows.Contains(line.Row))
                continue;

            // Skip if magazine filter is set and doesn't match
            if (Magazine.HasValue && line.Magazine != Magazine.Value)
                continue;

            var normalizedText = NormalizeText(line.Text);

            // Skip empty/whitespace-only lines
            if (string.IsNullOrEmpty(normalizedText))
                continue;

            // Use the most recent line if same text appears multiple times
            currentFrameContent[normalizedText] = line;
        }

        // Check for content that needs updating or is new
        foreach (var (text, line) in currentFrameContent)
        {
            if (_contentTracker.TryGetValue(text, out var tracked))
            {
                // Same content still present - update position and timestamp
                if (tracked.CurrentRow != line.Row && Verbose)
                {
                    Console.Error.WriteLine($"DEBUG: Content shifted from row {tracked.CurrentRow} to {line.Row}: \"{text}\"");
                }
                tracked.CurrentRow = line.Row;
                tracked.T42Data = line.Data;
                tracked.LastSeenAt = currentTimecode;
            }
            else
            {
                // Check if this is growing text from ACTIVE content
                var existingKey = FindGrowingContent(text);
                if (existingKey != null && _contentTracker.TryGetValue(existingKey, out var growingFrom))
                {
                    if (Verbose)
                    {
                        Console.Error.WriteLine($"DEBUG: Text growing: \"{existingKey}\" -> \"{text}\"");
                    }

                    // Update the tracker with new longer text
                    _contentTracker.Remove(existingKey);
                    _contentTracker[text] = new TrackedContent
                    {
                        CurrentRow = line.Row,
                        Text = text,
                        T42Data = line.Data,
                        FirstSeenAt = growingFrom.FirstSeenAt,  // Keep original start time
                        LastSeenAt = currentTimecode
                    };
                }
                // Check if this is growing text from PENDING CLEAR content (handles frame gaps)
                else if (FindGrowingPendingClear(text) is { } pendingKey)
                {
                    var pending = _pendingClears[pendingKey];
                    if (Verbose)
                    {
                        Console.Error.WriteLine($"DEBUG: Text growing from pending clear: \"{pendingKey}\" -> \"{text}\"");
                    }

                    // Restore from pending clear with new longer text
                    _pendingClears.Remove(pendingKey);
                    _contentTracker[text] = new TrackedContent
                    {
                        CurrentRow = line.Row,
                        Text = text,
                        T42Data = line.Data,
                        FirstSeenAt = pending.Content.FirstSeenAt,  // Keep original start time
                        LastSeenAt = currentTimecode
                    };
                }
                else
                {
                    // Completely new content
                    if (Verbose)
                    {
                        Console.Error.WriteLine($"DEBUG: New content on row {line.Row}: \"{text}\"");
                    }

                    _contentTracker[text] = new TrackedContent
                    {
                        CurrentRow = line.Row,
                        Text = text,
                        T42Data = line.Data,
                        FirstSeenAt = currentTimecode,
                        LastSeenAt = currentTimecode
                    };
                }
            }
        }

        // Find content that's no longer present - move to pending clear instead of emitting immediately
        var clearedContent = _contentTracker.Keys
            .Where(k => !currentFrameContent.ContainsKey(k))
            .ToList();

        foreach (var text in clearedContent)
        {
            var tracked = _contentTracker[text];
            _contentTracker.Remove(text);

            // Move to pending clear buffer
            _pendingClears[text] = new PendingClear
            {
                Content = tracked,
                ClearedAt = currentTimecode,
                FramesWaited = 0
            };

            if (Verbose)
            {
                Console.Error.WriteLine($"DEBUG: Content cleared, pending emit: \"{text}\"");
            }
        }

        // Increment wait counter for all pending clears and emit those that have waited long enough
        var pendingToEmit = new List<string>();
        foreach (var (text, pending) in _pendingClears)
        {
            pending.FramesWaited++;
            if (pending.FramesWaited >= ClearDelayFrames)
            {
                pendingToEmit.Add(text);
            }
        }

        foreach (var text in pendingToEmit)
        {
            var pending = _pendingClears[text];
            _pendingClears.Remove(text);

            if (Verbose)
            {
                Console.Error.WriteLine($"DEBUG: Emitting delayed subtitle: \"{text}\" ({pending.Content.FirstSeenAt} -> {pending.ClearedAt})");
            }

            yield return CreateTTIBlock(pending.Content, pending.ClearedAt);
        }
    }

    /// <summary>
    /// Finds a pending clear item that the new text is growing from.
    /// </summary>
    private string? FindGrowingPendingClear(string newText)
    {
        foreach (var existingKey in _pendingClears.Keys)
        {
            if (IsTextGrowing(existingKey, newText))
            {
                return existingKey;
            }
        }
        return null;
    }

    /// <summary>
    /// Finds an existing tracked content key that the new text is growing from.
    /// </summary>
    private string? FindGrowingContent(string newText)
    {
        foreach (var existingKey in _contentTracker.Keys)
        {
            if (IsTextGrowing(existingKey, newText))
            {
                return existingKey;
            }
        }
        return null;
    }

    /// <summary>
    /// Normalizes text for comparison by stripping ANSI codes and normalizing whitespace.
    /// </summary>
    public static string NormalizeText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        // Remove ANSI escape sequences
        var result = AnsiEscapeRegex().Replace(text, "");

        // Trim and normalize internal whitespace
        result = result.Trim();
        result = MultipleSpacesRegex().Replace(result, " ");

        return result;
    }

    /// <summary>
    /// Determines if currentText is an incremental growth of previousText.
    /// Handles word-by-word buildup like "thought" -> "thought we" -> "thought we would".
    /// </summary>
    public static bool IsTextGrowing(string previousText, string currentText)
    {
        if (string.IsNullOrEmpty(previousText))
            return !string.IsNullOrEmpty(currentText);

        var prev = NormalizeText(previousText);
        var curr = NormalizeText(currentText);

        if (string.IsNullOrEmpty(prev))
            return !string.IsNullOrEmpty(curr);

        // Current must be longer than previous
        if (curr.Length <= prev.Length)
            return false;

        // Simple prefix match: "Hello" -> "Hello world"
        if (curr.StartsWith(prev, StringComparison.Ordinal))
            return true;

        // Word-level growth check
        var prevWords = prev.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currWords = curr.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (currWords.Length < prevWords.Length)
            return false;

        // All previous words must be present at the start (allowing last word to grow)
        for (int i = 0; i < prevWords.Length - 1; i++)
        {
            if (i >= currWords.Length || currWords[i] != prevWords[i])
                return false;
        }

        // Last previous word must be prefix of corresponding current word
        if (prevWords.Length > 0)
        {
            var lastPrevWord = prevWords[^1];
            var correspondingCurrWord = currWords[prevWords.Length - 1];

            if (!correspondingCurrWord.StartsWith(lastPrevWord, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Creates a TTI (Text and Timing Information) block for the subtitle.
    /// </summary>
    private byte[] CreateTTIBlock(TrackedContent content, Timecode endTimecode)
    {
        var tti = new byte[Constants.STL_TTI_BLOCK_SIZE];

        // Byte 0: Subtitle Group Number (SGN) - always 0x00
        tti[0] = Constants.STL_SUBTITLE_GROUP;

        // Bytes 1-2: Subtitle Number (SN) - big-endian
        tti[1] = (byte)((_subtitleNumber >> 8) & 0xFF);
        tti[2] = (byte)(_subtitleNumber & 0xFF);
        _subtitleNumber++;

        // Byte 3: Extension Block Number (EBN) - 0xFF = not part of extension
        tti[3] = 0xFF;

        // Byte 4: Cumulative Status (CS)
        tti[4] = Constants.STL_CUMULATIVE_STATUS;

        // Bytes 5-8: Time Code In (TCI)
        var tciBytes = EncodeTimecodeToSTL(content.FirstSeenAt);
        Array.Copy(tciBytes, 0, tti, 5, 4);

        // Bytes 9-12: Time Code Out (TCO)
        var tcoBytes = EncodeTimecodeToSTL(endTimecode);
        Array.Copy(tcoBytes, 0, tti, 9, 4);

        // Byte 13: Vertical Position (VP) - row number
        tti[13] = content.CurrentRow is >= 0 and <= 31 ? (byte)content.CurrentRow : (byte)0;

        // Byte 14: Justification Code (JC) - 0x02 = left-justified
        tti[14] = 0x02;

        // Byte 15: Comment Flag (CF) - 0x00 = contains subtitle data
        tti[15] = 0x00;

        // Bytes 16-127: Text Field (TF) - 112 bytes
        var textData = ExtractSTLTextData(content.T42Data, content.CurrentRow);
        Array.Copy(textData, 0, tti, 16, Math.Min(textData.Length, Constants.STL_TEXT_FIELD_SIZE));

        // Fill remaining text field with STL space character
        for (int i = 16 + textData.Length; i < Constants.STL_TTI_BLOCK_SIZE; i++)
        {
            tti[i] = 0x8F;
        }

        if (Verbose)
        {
            Console.Error.WriteLine($"DEBUG: Created TTI block #{_subtitleNumber - 1}, Row: {content.CurrentRow}, " +
                                   $"TCI: {content.FirstSeenAt}, TCO: {endTimecode}");
        }

        return tti;
    }

    /// <summary>
    /// Encodes a timecode to STL format (4 bytes in BCD format).
    /// </summary>
    private static byte[] EncodeTimecodeToSTL(Timecode timecode)
    {
        return
        [
            (byte)((timecode.Hours / 10 << 4) | (timecode.Hours % 10)),
            (byte)((timecode.Minutes / 10 << 4) | (timecode.Minutes % 10)),
            (byte)((timecode.Seconds / 10 << 4) | (timecode.Seconds % 10)),
            (byte)((timecode.Frames / 10 << 4) | (timecode.Frames % 10))
        ];
    }

    /// <summary>
    /// Extracts and converts T42 text data to STL format.
    /// Strips parity bits and remaps control codes to STL equivalents.
    /// </summary>
    private static byte[] ExtractSTLTextData(byte[] t42Data, int row)
    {
        if (t42Data.Length < Constants.T42_LINE_SIZE)
        {
            // Pad with zeros if needed
            var padded = new byte[Constants.T42_LINE_SIZE];
            Array.Copy(t42Data, padded, Math.Min(t42Data.Length, Constants.T42_LINE_SIZE));
            t42Data = padded;
        }

        var stlText = new List<byte>();

        // Row 0 (header): Skip first 10 bytes, parse last 32
        // Rows 1-24 (captions): Skip first 2 bytes (mag/row), parse remaining 40
        int startIndex = row == 0 ? 10 : 2;

        for (int i = startIndex; i < t42Data.Length && stlText.Count < Constants.STL_TEXT_FIELD_SIZE; i++)
        {
            byte b = t42Data[i];

            // Strip parity bit (bit 7)
            byte stripped = (byte)(b & 0x7F);

            if (stripped == Constants.T42_BLOCK_START_BYTE)
            {
                // T42 Start Box -> STL Start Box
                stlText.Add(Constants.STL_START_BOX);
            }
            else if (stripped == Constants.T42_NORMAL_HEIGHT)
            {
                // T42 Normal Height -> STL End Box
                stlText.Add(Constants.STL_END_BOX);
            }
            else if (stripped <= 7)
            {
                // T42 color codes (0-7) map directly to STL alpha color codes
                stlText.Add(stripped);
            }
            else if (stripped is >= 0x20 and <= 0x7F)
            {
                // Displayable ASCII characters
                stlText.Add(stripped);
            }
            else if (stripped == 0x00)
            {
                // Null byte -> space
                stlText.Add(0x20);
            }
            else
            {
                // Other control codes -> space
                stlText.Add(0x20);
            }
        }

        return [.. stlText];
    }

    [GeneratedRegex(@"\x1b\[[0-9;]*m")]
    private static partial Regex AnsiEscapeRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleSpacesRegex();

    /// <summary>
    /// Disposes of the exporter and clears any tracked content.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _contentTracker.Clear();
        _pendingClears.Clear();
        GC.SuppressFinalize(this);
    }
}
