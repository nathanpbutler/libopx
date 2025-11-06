using System.Buffers;
using System.Runtime.CompilerServices;
using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;

namespace nathanbutlerDEV.libopx.Handlers;

/// <summary>
/// Handler for parsing MPEG Transport Stream (TS) format files with support for extracting teletext data.
/// Handles PAT/PMT parsing, PID filtering, DVB teletext extraction, and automatic stream detection.
/// </summary>
public class TSHandler : IPacketFormatHandler
{
    // Configuration properties
    private readonly int[]? _pids;
    private readonly bool _autoDetect;
    private readonly bool _verbose;
    private int _frameRate;

    // Internal state for TS parsing
    private readonly Dictionary<int, byte[]> _pesBuffers = [];
    private readonly Dictionary<int, int> _continuityCounters = [];
    private readonly HashSet<int> _pmtPIDs = [];
    private HashSet<int> _teletextPIDs = [];
    private HashSet<int> _videoPIDs = [];
    private int _packetSize = 0; // Detected packet size (188 or 192 bytes)
    private bool _frameRateDetected = false; // Flag to track if frame rate has been auto-detected
    private long? _firstPTS = null; // First PTS value for normalization

    /// <inheritdoc />
    public Format InputFormat => Format.TS;

    /// <inheritdoc />
    public Format[] ValidOutputs => [Format.T42, Format.VBI, Format.VBI_DOUBLE, Format.RCWT, Format.STL];

    /// <summary>
    /// Gets or sets the frame rate/timebase for PTS-to-timecode conversion.
    /// This is used when converting PTS timestamps to timecodes. Common values: 24, 25, 30, 48, 50, 60.
    /// </summary>
    public int FrameRate
    {
        get => _frameRate;
        set => _frameRate = value;
    }

    /// <summary>
    /// Creates a new instance of TSHandler.
    /// </summary>
    /// <param name="pids">Optional array of PIDs to filter. If null, auto-detection is used.</param>
    /// <param name="autoDetect">Whether to auto-detect teletext PIDs from PAT/PMT tables. Default is true.</param>
    /// <param name="verbose">Whether to enable verbose output for debugging. Default is false.</param>
    /// <param name="frameRate">The frame rate/timebase for PTS-to-timecode conversion. Default is 25.</param>
    public TSHandler(int[]? pids = null, bool autoDetect = true, bool verbose = false, int frameRate = 25)
    {
        _pids = pids;
        _autoDetect = autoDetect;
        _verbose = verbose;
        _frameRate = frameRate;
    }

    /// <inheritdoc />
    public IEnumerable<Packet> Parse(Stream inputStream, ParseOptions options)
    {
        ArgumentNullException.ThrowIfNull(inputStream);
        ArgumentNullException.ThrowIfNull(options);

        // Reset state for new parsing session
        ResetState();

        // Detect packet size first
        var packetSize = DetectPacketSize(inputStream);

        // Use default rows if not specified
        var rows = options.Rows ?? Constants.DEFAULT_ROWS;

        // Determine PID filtering strategy
        var targetPIDs = options.PIDs ?? _pids;
        if (targetPIDs != null)
        {
            // Manual PID filtering
            _teletextPIDs = new HashSet<int>(targetPIDs);
            if (_verbose)
                Console.Error.WriteLine($"TS: Using manual PID filtering: {string.Join(", ", targetPIDs)}");
        }
        else if (_verbose)
        {
            Console.Error.WriteLine("TS: Using auto-detection for teletext PIDs");
        }

        var outputFormat = options.OutputFormat;

        // Initialize RCWT state if needed
        if (outputFormat == Format.RCWT)
        {
            Functions.ResetRCWTHeader();
        }

        int packetNumber = 0;
        var fallbackTimecode = new Timecode(0, _frameRate);
        var tsBuffer = new byte[packetSize];

        while (inputStream.Read(tsBuffer, 0, packetSize) == packetSize)
        {
            // Validate sync byte
            if (tsBuffer[0] != Constants.TS_SYNC_BYTE)
            {
                // Try to resync - look for next sync byte
                continue;
            }

            // Parse TS header
            var pid = ((tsBuffer[1] & 0x1F) << 8) | tsBuffer[2];
            var payloadStart = (tsBuffer[1] & Constants.TS_PAYLOAD_START_INDICATOR) != 0;
            var hasAdaptation = (tsBuffer[3] & Constants.TS_ADAPTATION_FIELD_FLAG) != 0;
            var hasPayload = (tsBuffer[3] & Constants.TS_PAYLOAD_FLAG) != 0;
            var continuityCounter = tsBuffer[3] & 0x0F;

            if (!hasPayload)
                continue;

            // Calculate payload offset (payload is within the first 188 bytes)
            int payloadOffset = Constants.TS_HEADER_SIZE;
            if (hasAdaptation)
            {
                int adaptationLength = tsBuffer[4];
                payloadOffset += 1 + adaptationLength;
            }

            if (payloadOffset >= Constants.TS_PACKET_SIZE)
                continue;

            var payload = new byte[Constants.TS_PACKET_SIZE - payloadOffset];
            Array.Copy(tsBuffer, payloadOffset, payload, 0, payload.Length);

            // Process PAT (PID 0)
            if (pid == Constants.TS_PAT_PID && _autoDetect)
            {
                ProcessPAT(payload, payloadStart);
                continue;
            }

            // Process PMT
            if (_pmtPIDs.Contains(pid) && _autoDetect)
            {
                ProcessPMT(payload, payloadStart);

                // After processing PMT, attempt frame rate detection if not already done
                if (!_frameRateDetected && _videoPIDs.Count > 0)
                {
                    DetectFrameRateFromVideo(inputStream);
                }

                continue;
            }

            // Process teletext PIDs
            if (_teletextPIDs.Contains(pid))
            {
                // Accumulate PES data
                AccumulatePES(pid, payload, payloadStart, continuityCounter);

                // Try to process complete PES packet into a Packet object
                var packet = ProcessPESPacketToPacket(pid, ref packetNumber, ref fallbackTimecode, options.Magazine, rows);
                if (packet != null)
                {
                    yield return packet;
                }
            }
        }

        // Flush any remaining data
        foreach (var pid in _teletextPIDs)
        {
            var packet = ProcessPESPacketToPacket(pid, ref packetNumber, ref fallbackTimecode, options.Magazine, rows);
            if (packet != null)
            {
                yield return packet;
            }
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Packet> ParseAsync(
        Stream inputStream,
        ParseOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputStream);
        ArgumentNullException.ThrowIfNull(options);

        // Reset state for new parsing session
        ResetState();

        // Detect packet size first
        var packetSize = DetectPacketSize(inputStream);

        // Use default rows if not specified
        var rows = options.Rows ?? Constants.DEFAULT_ROWS;

        // Determine PID filtering strategy
        var targetPIDs = options.PIDs ?? _pids;
        if (targetPIDs != null)
        {
            _teletextPIDs = new HashSet<int>(targetPIDs);
            if (_verbose)
                Console.Error.WriteLine($"TS: Using manual PID filtering: {string.Join(", ", targetPIDs)}");
        }
        else if (_verbose)
        {
            Console.Error.WriteLine("TS: Using auto-detection for teletext PIDs");
        }

        var outputFormat = options.OutputFormat;

        // Initialize RCWT state if needed
        if (outputFormat == Format.RCWT)
        {
            Functions.ResetRCWTHeader();
        }

        int packetNumber = 0;
        var fallbackTimecode = new Timecode(0, _frameRate);

        // Use ArrayPool for better memory management
        var arrayPool = ArrayPool<byte>.Shared;
        var tsBuffer = arrayPool.Rent(packetSize);

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var bufferMemory = tsBuffer.AsMemory(0, packetSize);
                var bytesRead = await inputStream.ReadAsync(bufferMemory, cancellationToken);

                if (bytesRead != packetSize)
                    break;

                // Validate sync byte
                if (tsBuffer[0] != Constants.TS_SYNC_BYTE)
                {
                    continue;
                }

                // Parse TS header
                var pid = ((tsBuffer[1] & 0x1F) << 8) | tsBuffer[2];
                var payloadStart = (tsBuffer[1] & Constants.TS_PAYLOAD_START_INDICATOR) != 0;
                var hasAdaptation = (tsBuffer[3] & Constants.TS_ADAPTATION_FIELD_FLAG) != 0;
                var hasPayload = (tsBuffer[3] & Constants.TS_PAYLOAD_FLAG) != 0;
                var continuityCounter = tsBuffer[3] & 0x0F;

                if (!hasPayload)
                    continue;

                // Calculate payload offset (payload is within the first 188 bytes)
                int payloadOffset = Constants.TS_HEADER_SIZE;
                if (hasAdaptation)
                {
                    int adaptationLength = tsBuffer[4];
                    payloadOffset += 1 + adaptationLength;
                }

                if (payloadOffset >= Constants.TS_PACKET_SIZE)
                    continue;

                var payload = tsBuffer.AsSpan(payloadOffset, Constants.TS_PACKET_SIZE - payloadOffset).ToArray();

                // Process PAT
                if (pid == Constants.TS_PAT_PID && _autoDetect)
                {
                    ProcessPAT(payload, payloadStart);
                    continue;
                }

                // Process PMT
                if (_pmtPIDs.Contains(pid) && _autoDetect)
                {
                    ProcessPMT(payload, payloadStart);

                    // After processing PMT, attempt frame rate detection if not already done
                    if (!_frameRateDetected && _videoPIDs.Count > 0)
                    {
                        DetectFrameRateFromVideo(inputStream);
                    }

                    continue;
                }

                // Process teletext PIDs
                if (_teletextPIDs.Contains(pid))
                {
                    AccumulatePES(pid, payload, payloadStart, continuityCounter);

                    // Try to process complete PES packet into a Packet object
                    var packet = ProcessPESPacketToPacket(pid, ref packetNumber, ref fallbackTimecode, options.Magazine, rows);
                    if (packet != null)
                    {
                        yield return packet;
                    }
                }
            }

            // Flush remaining data
            foreach (var pid in _teletextPIDs)
            {
                var packet = ProcessPESPacketToPacket(pid, ref packetNumber, ref fallbackTimecode, options.Magazine, rows);
                if (packet != null)
                {
                    yield return packet;
                }
            }
        }
        finally
        {
            arrayPool.Return(tsBuffer);
        }
    }

    /// <summary>
    /// Resets internal state for a new parsing session.
    /// </summary>
    private void ResetState()
    {
        _pesBuffers.Clear();
        _continuityCounters.Clear();
        _pmtPIDs.Clear();
        _teletextPIDs.Clear();
        _videoPIDs.Clear();
        _packetSize = 0;
        _frameRateDetected = false;
        _firstPTS = null;
    }

    /// <summary>
    /// Detects the TS packet size by looking for consistent sync byte patterns.
    /// Supports 188-byte (standard) and 192-byte (with timecode) packet sizes.
    /// </summary>
    /// <returns>The detected packet size (188 or 192), or 188 if detection fails</returns>
    private int DetectPacketSize(Stream inputStream)
    {
        if (_packetSize != 0)
            return _packetSize; // Already detected

        // Try to detect packet size by looking for sync bytes
        const int detectionBufferSize = 1880; // Enough for ~10 packets of either size
        var buffer = new byte[detectionBufferSize];

        if (!inputStream.CanSeek)
        {
            // Can't seek, assume standard 188-byte packets
            if (_verbose)
                Console.Error.WriteLine("TS: Stream not seekable, assuming 188-byte packets");
            _packetSize = Constants.TS_PACKET_SIZE;
            return _packetSize;
        }

        var startPosition = inputStream.Position;
        var bytesRead = inputStream.Read(buffer, 0, detectionBufferSize);

        if (bytesRead < Constants.TS_PACKET_SIZE_WITH_TIMECODE * 2)
        {
            // Not enough data, assume standard
            inputStream.Position = startPosition;
            _packetSize = Constants.TS_PACKET_SIZE;
            return _packetSize;
        }

        // Look for first sync byte
        int syncPos = -1;
        for (int i = 0; i < bytesRead; i++)
        {
            if (buffer[i] == Constants.TS_SYNC_BYTE)
            {
                syncPos = i;
                break;
            }
        }

        if (syncPos == -1)
        {
            // No sync byte found
            inputStream.Position = startPosition;
            _packetSize = Constants.TS_PACKET_SIZE;
            return _packetSize;
        }

        // Check for 188-byte pattern
        int matches188 = 0;
        int checks188 = 0;
        for (int i = syncPos; i + Constants.TS_PACKET_SIZE < bytesRead; i += Constants.TS_PACKET_SIZE)
        {
            checks188++;
            if (buffer[i] == Constants.TS_SYNC_BYTE &&
                (i + Constants.TS_PACKET_SIZE >= bytesRead || buffer[i + Constants.TS_PACKET_SIZE] == Constants.TS_SYNC_BYTE))
            {
                matches188++;
            }
        }

        // Check for 192-byte pattern
        int matches192 = 0;
        int checks192 = 0;
        for (int i = syncPos; i + Constants.TS_PACKET_SIZE_WITH_TIMECODE < bytesRead; i += Constants.TS_PACKET_SIZE_WITH_TIMECODE)
        {
            checks192++;
            if (buffer[i] == Constants.TS_SYNC_BYTE &&
                (i + Constants.TS_PACKET_SIZE_WITH_TIMECODE >= bytesRead || buffer[i + Constants.TS_PACKET_SIZE_WITH_TIMECODE] == Constants.TS_SYNC_BYTE))
            {
                matches192++;
            }
        }

        // Determine packet size based on best match ratio
        double ratio188 = checks188 > 0 ? (double)matches188 / checks188 : 0;
        double ratio192 = checks192 > 0 ? (double)matches192 / checks192 : 0;

        if (ratio192 > ratio188 && ratio192 >= 0.8)
        {
            _packetSize = Constants.TS_PACKET_SIZE_WITH_TIMECODE;
            if (_verbose)
                Console.Error.WriteLine($"TS: Detected 192-byte packets (with timecode suffix)");
        }
        else
        {
            _packetSize = Constants.TS_PACKET_SIZE;
            if (_verbose)
                Console.Error.WriteLine($"TS: Detected 188-byte packets (standard)");
        }

        // Reset stream position
        inputStream.Position = startPosition;
        return _packetSize;
    }

    /// <summary>
    /// Auto-detects the video frame rate by analyzing PTS deltas from video PES packets.
    /// Sets the FrameRate property if detection is successful.
    /// </summary>
    /// <returns>True if frame rate was detected and set, false otherwise</returns>
    private bool DetectFrameRateFromVideo(Stream inputStream)
    {
        if (_frameRateDetected || _videoPIDs.Count == 0)
            return false;

        if (!inputStream.CanSeek)
        {
            if (_verbose)
                Console.Error.WriteLine("TS: Stream not seekable, cannot auto-detect frame rate");
            return false;
        }

        var startPosition = inputStream.Position;
        var ptsValues = new List<long>();
        var packetSize = _packetSize > 0 ? _packetSize : Constants.TS_PACKET_SIZE;
        var tsBuffer = new byte[packetSize];
        int packetsRead = 0;
        const int maxPacketsToScan = 5000; // Scan up to 5000 packets to find video PTS

        try
        {
            while (packetsRead < maxPacketsToScan && ptsValues.Count < 5)
            {
                if (inputStream.Read(tsBuffer, 0, packetSize) != packetSize)
                    break;

                packetsRead++;

                // Validate sync byte
                if (tsBuffer[0] != Constants.TS_SYNC_BYTE)
                    continue;

                // Parse TS header
                var pid = ((tsBuffer[1] & 0x1F) << 8) | tsBuffer[2];
                var payloadStart = (tsBuffer[1] & Constants.TS_PAYLOAD_START_INDICATOR) != 0;
                var hasPayload = (tsBuffer[3] & Constants.TS_PAYLOAD_FLAG) != 0;

                if (!hasPayload || !_videoPIDs.Contains(pid))
                    continue;

                // Only process packets that start a new PES packet
                if (!payloadStart)
                    continue;

                // Calculate payload offset
                int payloadOffset = Constants.TS_HEADER_SIZE;
                if ((tsBuffer[3] & Constants.TS_ADAPTATION_FIELD_FLAG) != 0)
                {
                    int adaptationLength = tsBuffer[4];
                    payloadOffset += 1 + adaptationLength;
                }

                if (payloadOffset + 14 > Constants.TS_PACKET_SIZE)
                    continue;

                // Check for PES start code
                if (tsBuffer[payloadOffset] != 0x00 || tsBuffer[payloadOffset + 1] != 0x00 || tsBuffer[payloadOffset + 2] != 0x01)
                    continue;

                // Try to extract PTS from this video PES packet
                if (TryExtractPTS(tsBuffer.AsSpan(payloadOffset, Constants.TS_PACKET_SIZE - payloadOffset).ToArray(), out long? pts) && pts.HasValue)
                {
                    // Only add if significantly different from last PTS (avoid duplicates)
                    if (ptsValues.Count == 0 || Math.Abs(pts.Value - ptsValues[^1]) > 1000)
                    {
                        ptsValues.Add(pts.Value);
                        if (_verbose)
                            Console.Error.WriteLine($"TS: Video PTS #{ptsValues.Count}: {pts.Value}");
                    }
                }
            }

            // Need at least 2 PTS values to calculate delta
            if (ptsValues.Count < 2)
            {
                if (_verbose)
                    Console.Error.WriteLine($"TS: Could not detect frame rate - only found {ptsValues.Count} video PTS values");
                return false;
            }

            // Sort PTS values (video frames can be out of presentation order due to B-frames)
            ptsValues.Sort();

            // Calculate average PTS delta
            var deltas = new List<long>();
            for (int i = 1; i < ptsValues.Count; i++)
            {
                long delta = ptsValues[i] - ptsValues[i - 1];
                // Handle PTS wraparound (33-bit counter)
                if (delta < 0)
                    delta += Constants.TS_PTS_MAX_VALUE;

                // Only include reasonable deltas (filter out duplicates or outliers)
                if (delta > 0 && delta < 10000) // Reasonable range for 24-60 fps
                    deltas.Add(delta);
            }

            // Need at least one valid delta
            if (deltas.Count == 0)
            {
                if (_verbose)
                    Console.Error.WriteLine("TS: Could not detect frame rate - no valid PTS deltas found");
                return false;
            }

            // Use minimum delta instead of average - this represents the actual frame interval
            // (some PES packets may not have PTS, causing larger deltas)
            long minDelta = deltas.Min();

            // Calculate frame rate: fps = 90000 / pts_delta
            double calculatedFps = (double)Constants.TS_PTS_CLOCK_FREQUENCY / minDelta;

            // Round to nearest standard frame rate
            int detectedFrameRate = calculatedFps switch
            {
                >= 23.0 and < 24.5 => 24,
                >= 24.5 and < 27.5 => 25,
                >= 27.5 and < 32.5 => 30,
                >= 45.0 and < 49.0 => 48,
                >= 49.0 and < 55.0 => 50,
                >= 55.0 and < 65.0 => 60,
                _ => 25 // Default fallback
            };

            _frameRate = detectedFrameRate;
            _frameRateDetected = true;

            if (_verbose)
                Console.Error.WriteLine($"TS: Auto-detected frame rate: {detectedFrameRate} fps (calculated: {calculatedFps:F2}, min PTS delta: {minDelta})");

            return true;
        }
        finally
        {
            // Reset stream position
            inputStream.Position = startPosition;
        }
    }

    /// <summary>
    /// Processes PAT (Program Association Table) to find PMT PIDs.
    /// </summary>
    private void ProcessPAT(byte[] payload, bool payloadStart)
    {
        if (!payloadStart || payload.Length < 8)
            return;

        // Skip pointer field if present
        int offset = payload[0] + 1;

        if (offset + 8 > payload.Length)
            return;

        // Parse PAT
        int tableId = payload[offset];
        if (tableId != 0x00) // PAT table ID
            return;

        int sectionLength = ((payload[offset + 1] & 0x0F) << 8) | payload[offset + 2];
        int programInfoStart = offset + 8;
        int programInfoEnd = Math.Min(offset + 3 + sectionLength - 4, payload.Length); // -4 for CRC

        // Extract PMT PIDs
        for (int i = programInfoStart; i + 3 < programInfoEnd; i += 4)
        {
            int programNumber = (payload[i] << 8) | payload[i + 1];
            int pmtPid = ((payload[i + 2] & 0x1F) << 8) | payload[i + 3];

            if (programNumber != 0) // Ignore network PID
            {
                _pmtPIDs.Add(pmtPid);
                if (_verbose)
                    Console.Error.WriteLine($"TS: Found PMT PID {pmtPid} for program {programNumber}");
            }
        }
    }

    /// <summary>
    /// Processes PMT (Program Map Table) to find teletext stream PIDs.
    /// </summary>
    private void ProcessPMT(byte[] payload, bool payloadStart)
    {
        if (!payloadStart || payload.Length < 12)
            return;

        // Skip pointer field
        int offset = payload[0] + 1;

        if (offset + 12 > payload.Length)
            return;

        int tableId = payload[offset];
        if (tableId != 0x02) // PMT table ID
            return;

        int sectionLength = ((payload[offset + 1] & 0x0F) << 8) | payload[offset + 2];
        int programInfoLength = ((payload[offset + 10] & 0x0F) << 8) | payload[offset + 11];
        int streamInfoStart = offset + 12 + programInfoLength;
        int streamInfoEnd = Math.Min(offset + 3 + sectionLength - 4, payload.Length);

        // Parse stream information
        int i = streamInfoStart;
        while (i + 4 < streamInfoEnd)
        {
            int streamType = payload[i];
            int elementaryPid = ((payload[i + 1] & 0x1F) << 8) | payload[i + 2];
            int esInfoLength = ((payload[i + 3] & 0x0F) << 8) | payload[i + 4];

            if (_verbose)
                Console.Error.WriteLine($"TS: Found stream type 0x{streamType:X2} on PID {elementaryPid}");

            // Check if this is a teletext stream
            if (streamType == Constants.TS_STREAM_TYPE_TELETEXT)
            {
                _teletextPIDs.Add(elementaryPid);
                if (_verbose)
                    Console.Error.WriteLine($"TS: *** Found teletext stream on PID {elementaryPid} ***");
            }
            // Check if this is a video stream (for frame rate detection)
            else if (streamType == Constants.TS_STREAM_TYPE_MPEG1_VIDEO ||
                     streamType == Constants.TS_STREAM_TYPE_MPEG2_VIDEO ||
                     streamType == Constants.TS_STREAM_TYPE_H264_VIDEO ||
                     streamType == Constants.TS_STREAM_TYPE_H265_VIDEO)
            {
                _videoPIDs.Add(elementaryPid);
                if (_verbose)
                    Console.Error.WriteLine($"TS: Found video stream (type 0x{streamType:X2}) on PID {elementaryPid}");
            }

            i += 5 + esInfoLength;
        }

        if (_verbose && _teletextPIDs.Count == 0)
        {
            Console.Error.WriteLine("TS: Warning - No teletext streams found in PMT");
        }
    }

    /// <summary>
    /// Accumulates PES packet data for a given PID.
    /// </summary>
    private void AccumulatePES(int pid, byte[] payload, bool payloadStart, int continuityCounter)
    {
        if (payloadStart)
        {
            // Start new PES packet
            _pesBuffers[pid] = [.. payload];
            _continuityCounters[pid] = continuityCounter;
        }
        else
        {
            // Append to existing PES buffer
            if (_pesBuffers.TryGetValue(pid, out var existing))
            {
                var combined = new byte[existing.Length + payload.Length];
                Array.Copy(existing, 0, combined, 0, existing.Length);
                Array.Copy(payload, 0, combined, existing.Length, payload.Length);
                _pesBuffers[pid] = combined;
            }
        }
    }

    /// <summary>
    /// Processes a complete PES packet and creates a Packet object containing all teletext lines.
    /// </summary>
    /// <param name="pid">The PID of the teletext stream</param>
    /// <param name="packetNumber">Reference to the packet number counter</param>
    /// <param name="fallbackTimecode">Reference to the fallback timecode for packets without PTS</param>
    /// <param name="magazine">Optional magazine filter</param>
    /// <param name="rows">Optional row filter</param>
    /// <returns>A Packet object containing all lines from this PES packet, or null if no valid data</returns>
    private Packet? ProcessPESPacketToPacket(int pid, ref int packetNumber, ref Timecode fallbackTimecode, int? magazine = null, int[]? rows = null)
    {
        if (!_pesBuffers.TryGetValue(pid, out var pesData) || pesData.Length == 0)
            return null;

        if (_verbose && pesData.Length > 0)
            Console.Error.WriteLine($"TS: Processing PES packet from PID {pid}, buffer size: {pesData.Length}");

        // Check for PES start code
        if (pesData.Length < 6 || pesData[0] != 0x00 || pesData[1] != 0x00 || pesData[2] != 0x01)
        {
            if (_verbose)
                Console.Error.WriteLine($"TS: PID {pid} - Invalid PES start code");
            _pesBuffers[pid] = [];
            return null;
        }

        // Try to extract PTS for timecode
        Timecode packetTimecode;
        if (TryExtractPTS(pesData, out long? pts) && pts.HasValue)
        {
            packetTimecode = ConvertPTSToTimecode(pts.Value);
        }
        else
        {
            // Fallback to frame-based incrementing
            packetTimecode = fallbackTimecode;
            fallbackTimecode = fallbackTimecode.GetNext();
        }

        // Parse PES header
        int pesHeaderLength = pesData[8];
        int pesDataStart = 9 + pesHeaderLength;

        if (pesDataStart + 1 >= pesData.Length)
        {
            if (_verbose)
                Console.Error.WriteLine($"TS: PID {pid} - Not enough data after PES header");
            _pesBuffers[pid] = [];
            return null;
        }

        // Check data identifier
        int dataIdentifier = pesData[pesDataStart];
        if (dataIdentifier != Constants.TS_DATA_IDENTIFIER_EBU_TELETEXT)
        {
            if (_verbose)
                Console.Error.WriteLine($"TS: PID {pid} - Wrong data identifier (expected 0x{Constants.TS_DATA_IDENTIFIER_EBU_TELETEXT:X2}, got 0x{dataIdentifier:X2})");
            _pesBuffers[pid] = [];
            return null;
        }

        // Extract all T42 data units from this PES packet
        var lines = new List<Line>();
        int offset = pesDataStart + 1;
        int lineNumber = packetNumber;

        while (offset + Constants.TS_TELETEXT_DATA_UNIT_SIZE <= pesData.Length)
        {
            int dataUnitId = pesData[offset];
            int dataUnitLength = pesData[offset + 1];

            // Check for teletext data unit (0x02 or 0x03)
            if ((dataUnitId == Constants.TS_DATA_UNIT_ID_TELETEXT || dataUnitId == Constants.TS_DATA_UNIT_ID_TELETEXT_SUBTITLE)
                && (dataUnitLength == Constants.T42_LINE_SIZE || dataUnitLength == 44))
            {
                // Extract T42 data (skip 2-byte data unit header, then skip 2 framing bytes if length is 44)
                int dataStart = offset + 2;
                if (dataUnitLength == 44)
                {
                    // Skip the 2 framing bytes (field parity + line offset / framing code)
                    dataStart += 2;
                }

                var t42Data = new byte[Constants.T42_LINE_SIZE];
                Array.Copy(pesData, dataStart, t42Data, 0, Constants.T42_LINE_SIZE);

                // Reverse bits in each byte (LSB-first to MSB-first conversion)
                for (int i = 0; i < t42Data.Length; i++)
                {
                    t42Data[i] = ReverseBits(t42Data[i]);
                }

                // Create line from T42 data
                if (!Array.TrueForAll(t42Data, b => b == 0))
                {
                    var line = new Line
                    {
                        LineNumber = lineNumber++,
                        LineTimecode = packetTimecode,
                    };

                    // Use ParseLine to handle format conversion
                    var outputFormat = Format.T42; // Default to T42, will be set later if needed
                    line.ParseLine(t42Data, outputFormat);

                    // Apply filtering
                    bool includeThisLine = true;
                    if (magazine.HasValue && line.Magazine != magazine.Value)
                        includeThisLine = false;
                    if (rows != null && !rows.Contains(line.Row))
                        includeThisLine = false;

                    if (includeThisLine)
                        lines.Add(line);
                }
            }

            offset += 2 + dataUnitLength;
        }

        // Clear processed data
        _pesBuffers[pid] = [];

        if (lines.Count == 0)
            return null;

        // Update packet number for next packet
        packetNumber = lineNumber;

        // Create packet with proper header (2-byte line count)
        int lineCount = lines.Count;
        byte[] header = [(byte)(lineCount >> 8), (byte)(lineCount & 0xFF)];

        var packet = new Packet(header)
        {
            Timecode = packetTimecode,
            Lines = lines
        };

        if (_verbose)
            Console.Error.WriteLine($"TS: Created packet with {lines.Count} lines, timecode: {packetTimecode}");

        return packet;
    }

    /// <summary>
    /// Reverses the bits in a byte (LSB-first to MSB-first conversion).
    /// This is needed because teletext data in MPEG-TS is transmitted LSB-first.
    /// </summary>
    /// <param name="b">The byte to reverse</param>
    /// <returns>The byte with bits reversed</returns>
    private static byte ReverseBits(byte b)
    {
        // Reverse bits using lookup table approach (fastest method)
        b = (byte)((b * 0x0202020202UL & 0x010884422010UL) % 1023);
        return b;
    }

    /// <summary>
    /// Extracts PTS (Presentation Time Stamp) from PES header if present.
    /// </summary>
    /// <param name="pesData">The PES packet data</param>
    /// <param name="pts">Output PTS value in 90kHz ticks, or null if not present</param>
    /// <returns>True if PTS was found and extracted, false otherwise</returns>
    private bool TryExtractPTS(byte[] pesData, out long? pts)
    {
        pts = null;

        // Check minimum PES header size
        if (pesData.Length < Constants.TS_PES_PTS_OFFSET + Constants.TS_PTS_SIZE)
            return false;

        // Check PTS/DTS flags in PES header (byte 7)
        byte ptsFlags = (byte)(pesData[Constants.TS_PES_FLAGS_OFFSET] & Constants.TS_PES_PTS_DTS_FLAGS);

        // Check if PTS is present (0x80 = PTS only, 0xC0 = both PTS and DTS)
        if (ptsFlags != Constants.TS_PES_PTS_FLAG && ptsFlags != Constants.TS_PES_PTS_AND_DTS_FLAG)
            return false;

        // Extract PTS value (33-bit timestamp encoded in 5 bytes)
        // Format: xxxx1PPP 1PPPPPPP PPPPPPP1 1PPPPPPP PPPPPPP1
        // where x = marker bits (0010 or 0011), P = PTS bits, 1 = marker bit
        byte[] ptsBytes = new byte[Constants.TS_PTS_SIZE];
        Array.Copy(pesData, Constants.TS_PES_PTS_OFFSET, ptsBytes, 0, Constants.TS_PTS_SIZE);

        // Validate marker bits
        if ((ptsBytes[0] & 0x01) != 0x01 ||
            (ptsBytes[2] & 0x01) != 0x01 ||
            (ptsBytes[4] & 0x01) != 0x01)
        {
            if (_verbose)
                Console.Error.WriteLine("TS: PTS marker bits invalid");
            return false;
        }

        // Extract 33-bit PTS value
        long ptsValue = 0;

        // Bits 32-30 (3 bits from byte 0)
        ptsValue |= ((long)(ptsBytes[0] & 0x0E) >> 1) << 30;

        // Bits 29-15 (15 bits from bytes 1-2)
        ptsValue |= ((long)ptsBytes[1] << 22);
        ptsValue |= ((long)(ptsBytes[2] & 0xFE) << 14);

        // Bits 14-0 (15 bits from bytes 3-4)
        ptsValue |= ((long)ptsBytes[3] << 7);
        ptsValue |= ((long)(ptsBytes[4] & 0xFE) >> 1);

        pts = ptsValue;

        if (_verbose)
            Console.Error.WriteLine($"TS: Extracted PTS: {ptsValue} (90kHz ticks)");

        return true;
    }

    /// <summary>
    /// Converts PTS (Presentation Time Stamp) to a Timecode.
    /// PTS values are normalized to start at 00:00:00:00 by subtracting the first PTS encountered.
    /// </summary>
    /// <param name="pts">PTS value in 90kHz ticks</param>
    /// <param name="timebase">The frame rate/timebase for the timecode (default: use FrameRate property)</param>
    /// <returns>A Timecode object representing the PTS timestamp</returns>
    private Timecode ConvertPTSToTimecode(long pts, int? timebase = null)
    {
        timebase ??= _frameRate;

        // Track the first PTS value for normalization
        if (!_firstPTS.HasValue)
        {
            _firstPTS = pts;
            if (_verbose)
                Console.Error.WriteLine($"TS: First PTS value: {pts}");
        }

        // Normalize PTS by subtracting the first PTS value
        long normalizedPTS = pts - _firstPTS.Value;

        // PTS is in 90kHz ticks (90000 ticks per second)
        // Calculate frame number: (normalized_PTS * frame_rate) / 90000
        // This avoids floating point precision issues
        var frameNumber = (int)(normalizedPTS * timebase.Value / Constants.TS_PTS_CLOCK_FREQUENCY);

        if (_verbose)
            Console.Error.WriteLine($"TS: Converting PTS {pts} (normalized: {normalizedPTS}) to frame {frameNumber} at {timebase}fps");

        return new Timecode(frameNumber, timebase.Value);
    }
}
