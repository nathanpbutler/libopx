using nathanbutlerDEV.libopx;
using simpleRestriper.Models;

namespace simpleRestriper.Services;

public class RestripeService
{
    /// <summary>
    /// Maximum bytes to search for TimecodeComponent in MXF header.
    /// MXF metadata typically resides in the first 128KB of the file.
    /// </summary>
    private const int MaxTimecodeSearchBytes = 128000;

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
                using var io = FormatIO.Open(filePath);

                // Get TimecodeComponent from MXF metadata via first packet's timecode
                // FormatIO.Open automatically reads the TimecodeComponent for MXF files
                var firstPacket = io.ParsePackets().FirstOrDefault();

                if (firstPacket?.Timecode != null)
                {
                    fileInfo.Timebase = firstPacket.Timecode.Timebase;
                    fileInfo.SmpteTimecode = firstPacket.Timecode;

                    // TimecodeComponent is read during FormatIO.Open for MXF files
                    // and stored in ParseOptions.StartTimecode - we need to read it separately
                    fileInfo.TimecodeComponent = ReadTimecodeComponent(filePath);
                }
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
    /// Reads the TimecodeComponent metadata from an MXF file.
    /// </summary>
    private static Timecode? ReadTimecodeComponent(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var keyBuffer = new byte[16]; // KLV key size

            // Search for TimecodeComponent in MXF header
            while (stream.Position < MaxTimecodeSearchBytes)
            {
                var keyBytesRead = stream.Read(keyBuffer, 0, 16);
                if (keyBytesRead != 16) break;

                var keyType = Keys.GetKeyType(keyBuffer.AsSpan(0, 16));
                var length = ReadBerLength(stream);
                if (length < 0) break;

                if (keyType == KeyType.TimecodeComponent)
                {
                    var data = new byte[length];
                    var dataBytesRead = stream.Read(data, 0, length);
                    if (dataBytesRead != length) break;

                    var tc = TimecodeComponent.Parse(data);
                    return new Timecode(
                        tc.StartTimecode,
                        tc.RoundedTimecodeTimebase,
                        tc.DropFrame);
                }
                else
                {
                    stream.Seek(length, SeekOrigin.Current);
                }
            }
        }
        catch
        {
            // Ignore errors reading TimecodeComponent
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
