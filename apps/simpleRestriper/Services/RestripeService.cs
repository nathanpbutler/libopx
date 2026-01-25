using nathanbutlerDEV.libopx;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Formats;
using simpleRestriper.Models;

namespace simpleRestriper.Services;

public class RestripeService
{
    // Constants for reading SMPTE timecode from System packets
    private const int KlvKeySize = 16;
    private const int SystemMetadataPackOffset = 41;
    private const int SystemMetadataSetOffset = 12;
    private const int SmpteTimecodeSize = 4;

    /// <summary>
    /// Loads MXF file metadata including TimecodeComponent and first SMPTE timecode.
    /// </summary>
    public async Task<MxfFileInfo> LoadFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var fileInfo = new MxfFileInfo { FilePath = filePath };

            try
            {
                // Use MXF class to read TimecodeComponent from header (~128KB)
                using var mxf = new MXF(new FileInfo(filePath));

                fileInfo.TimecodeComponent = mxf.StartTimecode;
                fileInfo.Timebase = mxf.StartTimecode.Timebase;

                // Read first SMPTE timecode by scanning for first System key
                var firstSmpte = ReadFirstSmpteTimecode(filePath, mxf.StartTimecode.Timebase, mxf.StartTimecode.DropFrame);
                fileInfo.SmpteTimecode = firstSmpte ?? mxf.StartTimecode;
            }
            catch (Exception ex)
            {
                fileInfo.Status = RestripeStatus.Error;
                fileInfo.ErrorMessage = ex.Message;
            }

            return fileInfo;
        }, cancellationToken);
    }

    /// <summary>
    /// Reads the first SMPTE timecode from an MXF file by scanning for the first System key.
    /// Much faster than parsing all packets.
    /// </summary>
    private static Timecode? ReadFirstSmpteTimecode(string filePath, int timebase, bool dropFrame)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var keyBuffer = new byte[KlvKeySize];
            var smpteBuffer = new byte[SmpteTimecodeSize];

            while (stream.Position < stream.Length)
            {
                var keyBytesRead = stream.Read(keyBuffer, 0, KlvKeySize);
                if (keyBytesRead != KlvKeySize) break;

                var keyType = Keys.GetKeyType(keyBuffer.AsSpan());
                var length = ReadBerLength(stream);
                if (length < 0) break;

                if (keyType == KeyType.System)
                {
                    // Determine offset to SMPTE timecode within System packet
                    int offset;
                    if (length >= SystemMetadataPackOffset + SmpteTimecodeSize)
                        offset = SystemMetadataPackOffset;
                    else if (length >= SystemMetadataSetOffset + SmpteTimecodeSize)
                        offset = SystemMetadataSetOffset;
                    else
                    {
                        stream.Seek(length, SeekOrigin.Current);
                        continue;
                    }

                    // Skip to SMPTE timecode position and read it
                    stream.Seek(offset, SeekOrigin.Current);
                    var smpteRead = stream.Read(smpteBuffer, 0, SmpteTimecodeSize);
                    if (smpteRead == SmpteTimecodeSize)
                    {
                        return Timecode.FromBytes(smpteBuffer, timebase, dropFrame);
                    }
                    break;
                }
                else
                {
                    stream.Seek(length, SeekOrigin.Current);
                }
            }
        }
        catch
        {
            // Fall back to TimecodeComponent if SMPTE read fails
        }

        return null;
    }

    private static int ReadBerLength(Stream input)
    {
        var firstByte = input.ReadByte();
        if (firstByte < 0) return -1;

        if ((firstByte & 0x80) == 0)
            return firstByte;

        var numLengthBytes = firstByte & 0x7F;
        if (numLengthBytes > 4 || numLengthBytes == 0) return -1;

        var lengthBytes = new byte[numLengthBytes];
        var bytesRead = input.Read(lengthBytes, 0, numLengthBytes);
        if (bytesRead != numLengthBytes) return -1;

        int length = 0;
        for (int i = 0; i < numLengthBytes; i++)
        {
            length = (length << 8) | lengthBytes[i];
        }

        return length;
    }

    /// <summary>
    /// Restripes an MXF file with the new timecode.
    /// </summary>
    public async Task RestripeAsync(MxfFileInfo file, string newTimecode, CancellationToken cancellationToken = default)
    {
        file.Status = RestripeStatus.Processing;
        file.ErrorMessage = null;

        try
        {
            using var io = FormatIO.Open(file.FilePath)
                .WithProgress(false)
                .WithVerbose(false);

            await io.RestripeAsync(newTimecode, cancellationToken);

            file.Status = RestripeStatus.Success;

            // Re-read the file to update displayed timecodes
            var updated = await LoadFileAsync(file.FilePath, cancellationToken);
            file.TimecodeComponent = updated.TimecodeComponent;
            file.SmpteTimecode = updated.SmpteTimecode;
        }
        catch (OperationCanceledException)
        {
            file.Status = RestripeStatus.Pending;
            throw;
        }
        catch (Exception ex)
        {
            file.Status = RestripeStatus.Error;
            file.ErrorMessage = ex.Message;
        }
    }
}
