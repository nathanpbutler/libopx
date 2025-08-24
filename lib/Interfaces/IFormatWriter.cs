namespace nathanbutlerDEV.libopx.Interfaces;

/// <summary>
/// Interface for format writers that support writing data and blank lines.
/// </summary>
public interface IFormatWriter
{
    /// <summary>
    /// Writes the specified data bytes to the output.
    /// </summary>
    /// <param name="data">The data bytes to write.</param>
    void Write(byte[] data);
    
    /// <summary>
    /// Writes blank data to the output for filtered lines.
    /// </summary>
    void WriteBlank();
}