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
// Ensure the file path is correct and the file exists
var bin = new BIN(@"samples\test.bin");

// Parse the BIN file
bin.Parse();

// Display the results
Console.WriteLine($"{bin.Packets.Count} packets found.");
```