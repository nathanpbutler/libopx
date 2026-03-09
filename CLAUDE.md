# libopx Development Notes

## Architecture

- **Handler bypass pattern**: T42Handler and VBIHandler set Line properties (Magazine, Row, Text) directly — they do NOT call `Line.ParseLine()` or `Line.ExtractMetadata()`. When adding new properties to Line that derive from T42 data, you must also update these handlers. ANCHandler, MXFHandler, and TSHandler DO call `ParseLine()`/`ParseLineAsync()`.
- **FormatIO** is the public fluent API; handlers are internal. `FormatIO.Open()` auto-detects format from file extension.
- **Line-based formats**: VBI, VBI_DOUBLE, T42 → `ParseLines()`/`ParseLinesAsync()`
- **Packet-based formats**: ANC, TS, MXF → `ParsePackets()`/`ParsePacketsAsync()`

## Apps

- `opx` — CLI tool (System.CommandLine)
- `simpleRestriper` — Avalonia desktop GUI for MXF restriping (to be replaced by opxBlazor)
- `opxBlazor` — Blazor Server GUI with MudBlazor (unified GUI, Filter page first)

## Build & Test

- `dotnet build libopx.sln` — build everything
- `dotnet test tests/libopx.Tests.csproj` — run all tests
- Pre-existing CS0618 warnings in MXF.cs and tests are expected (obsolete API usage)
