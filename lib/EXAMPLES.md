# Example usage of the library

## MXF

```csharp
using nathanbutlerDEV.libopx.Formats;

// Open the MXF file
// Ensure the file path is correct and the file exists
var mxf = new MXF(@"samples\test.mxf");

// Set the required keys
mxf.AddRequiredKey("system");
mxf.AddRequiredKey("data");

// Parse the MXF file
mxf.Parse();

// Display the results
Console.WriteLine($"{mxf.SMPTETimecodes.Count} SMPTE timecodes found.");
Console.WriteLine($"{mxf.Packets.Count} packets found.");
```

## MXF.MXFData (Extracted MXF Data)

```csharp
using nathanbutlerDEV.libopx.Formats;

// Open the extracted MXF data file (*.bin)
// Note: .bin files are extracted MXF data streams, not a standalone format
var mxfData = new MXF.MXFData("temp/rick.bin");

// Parse the extracted MXF data using the new method with specified parameters
foreach (var packet in mxfData.Parse(8, Constants.CAPTION_ROWS))
{
    // Process each packet as needed
    Console.WriteLine(packet);
}
```

## VBI

<!-- markdownlint-disable MD013 -->

```csharp
using nathanbutlerDEV.libopx.Formats;

// Open the VBI file
var vbi = new VBI("temp/input.vbi"); // VBI type is determined by the file extension (vbi/vbid) if not specified.

// Parse the VBI file using the new method with specified parameters
foreach (var line in vbi.Parse(8, Constants.CAPTION_ROWS))
{
    // Process each line as needed
    Console.WriteLine(line);
}
```
