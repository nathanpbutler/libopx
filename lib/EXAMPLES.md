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

## BIN

```csharp
using nathanbutlerDEV.libopx.Formats;

// Open the BIN file
var bin = new BIN("temp/rick.bin");

// Parse the BIN file
bin.Parse();

// Display the results
Console.WriteLine($"{bin.Packets.Count} packets found.");

// Iterate through the packets and display their contents
foreach (var packet in bin.Packets)
{
    Console.WriteLine(packet);
}
```

## BIN (proposed new method)

```csharp
using nathanbutlerDEV.libopx.Formats;

// Open the BIN file
var bin = new BIN("temp/rick.bin");

// Parse the BIN file using the new method with specified parameters
foreach (var packet in bin.Parse(8, Constants.CAPTION_ROWS))
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
