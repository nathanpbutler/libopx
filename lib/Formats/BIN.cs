using System;

namespace nathanbutlerDEV.libopx.Formats;

public class BIN : IDisposable
{
    public FileInfo? Input { get; set; } = null; // If null, read from stdin
    public FileInfo? Output { get; set; } = null; // If null, write to stdout
    private Stream? _inputStream;
    private Stream? _outputStream;
    public Stream InputStream => _inputStream ??= Input == null ? Console.OpenStandardInput() : Input.OpenRead();
    public Stream OutputStream => _outputStream ??= Output == null ? Console.OpenStandardOutput() : Output.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
    public List<Packet> Packets { get; set; } = []; // List of packets in the BIN file

    /// <summary>
    /// Constructor for BIN format
    /// </summary>
    /// <param name="inputFile">Path to the input BIN file</param>
    /// <exception cref="FileNotFoundException">Thrown if the specified file does not exist</exception>
    /// <exception cref="InvalidDataException">Thrown if the file is not a

    public BIN(string inputFile)
    {
        Input = new FileInfo(inputFile);

        if (!Input.Exists)
        {
            throw new FileNotFoundException("The specified BIN file does not exist.", inputFile);
        }

        InputStream.Seek(0, SeekOrigin.Begin); // Reset stream position to the beginning
    }

    public async Task<int> Parse()
    {
        await Task.Delay(100); // Simulate some asynchronous operation
        // Here you would implement the actual parsing logic for the BIN file.
        return 0; // Return an integer status code, e.g., 0 for success
    }

    ~BIN()
    {
        _inputStream?.Dispose();
        _outputStream?.Dispose();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        // Dispose of the streams if they are not null
        _inputStream?.Dispose();
        _outputStream?.Dispose();
    }
}
