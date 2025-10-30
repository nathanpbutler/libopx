# libopx Copilot Guide

## Big Picture
- Solution targets .NET 9; `lib/` holds the core library, `apps/opx/` is the CLI wrapper, and `tests/` contains xUnit plus memory benchmarks.
- Parsers in `lib/Formats/` stream teletext/MXF/TS data (`MXF`, `MXFData`, `VBI`, `T42`, `TS`) into `Packet` and `Line` objects; conversions and timecode math live in `Functions.cs`, `Line.cs`, and `Timecode*.cs`.
- CLI commands in `apps/opx/Commands.cs` wrap the library through System.CommandLine, mirroring filter/convert/extract/restripe workflows described in `apps/opx/README.md`.

## Patterns & Constraints
- Preserve existing public APIs and wire formats unless the task explicitly allows breaking changes; check `lib/Functions.cs`, `Line.cs`, and parser signatures before editing.
- Maintain sync/async parity: every `Parse(...)` method has a matching `ParseAsync(...)` using `IAsyncEnumerable` and `ArrayPool`; update both paths when altering behavior.
- Keep processing single-pass and low-allocation; reuse buffers and avoid temporary collections—follow `T42.Parse[Async]` and `VBI.Parse[Async]` as examples.
- When adding filtering or conversion logic, honor magazine/row semantics handled via `Constants.CAPTION_ROWS`, `Constants.DEFAULT_ROWS`, and `Line.SetCachedType()`.

## Critical Format Conversions
- RCWT support: Full Raw Captions With Time conversion with automatic T42 payload handling, field alternation, and FTS synchronization via `Line.ToRCWT()`.
- STL support: Complete EBU STL (EBU-Tech 3264) subtitle format with GSI header generation, TTI block creation, BCD timecode encoding, and automatic empty line filtering via `Line.ToSTL()`.
- Universal filtering: Magazine and row filtering apply across VBI, T42, MXF streams using `Constants.CAPTION_ROWS` (1-24) vs `Constants.DEFAULT_ROWS` (0-31).

## Developer Workflows
- Build everything with `dotnet build libopx.sln`; build library or CLI individually via `dotnet build lib/libopx.csproj` or `dotnet build apps/opx/opx.csproj`.
- Run unit tests with `dotnet test`; target a suite with `dotnet test --filter FullyQualifiedName~ClassName`.
- Validate allocation-sensitive changes using `dotnet test --filter "MemoryBenchmarkTests"` before claiming performance gains.
- CLI smoke tests mirror README examples, e.g. `dotnet run --project apps/opx -- filter -m 8 -r 20,22 input.vbi`.

## Testing & Samples
- Tests rely on downloadable fixtures; call `await SampleFiles.EnsureAsync()` in new async tests to fetch sample data.
- Expand coverage in `tests/` alongside parser or conversion changes; mirror sync/async cases where practical.
- Prefer verifying pipelines through existing sample filenames (e.g. `input.vbi`, `input.mxf`) rather than hardcoded absolute paths.

## Performance & Memory
- Async parsers use `ArrayPool` and yield ~90-95% allocation reduction vs sync methods—validate with `MemoryBenchmarkTests`.
- Use `Span<byte>`/`ReadOnlySpan<byte>` over array copies; dispose streams promptly with `using`/`await using`.
- Avoid global state except centralized areas (e.g., RCWT header state via `Functions.ResetRCWTHeader()`).

## Tooling Notes
- Teletext character handling is centralized in `TeletextCharset.cs`; extend mappings there if you add new glyph support.
- Timecode math is encapsulated by `Timecode.cs` and `TimecodeComponent.cs`; ensure frame-rate-aware operations remain consistent.
- The CLI’s streaming output depends on `CommandHelpers.cs`; reuse those helpers when adding new commands or options.

## Reference Docs
- Start with `AGENTS.md` for canonical agent guidance; complement with `README.md`, `lib/README.md`, and `apps/opx/README.md` for architecture and command details.
- Example usage snippets live in `lib/EXAMPLES.md`; follow their patterns for new documentation or samples.
