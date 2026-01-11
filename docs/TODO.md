# libopx v3.0 Implementation TODO

**Status:** v2.3.0 in progress (STL export) | Phase 2 COMPLETE ✅
**Last Updated:** 2026-01-09
**Release Strategy:** Consolidated releases (v2.2.0 ✅, v2.3.0 in progress, v2.4.0, v3.0.0)

---

## Consolidated Release Strategy

To avoid flooding NuGet with rapid incremental releases:

- **v2.2.0** ✅ - Phases 1 + 2: Internal foundation (FormatIOBase + IFormatHandler abstractions)
- **v2.3.0** 🚧 - STL export: Intelligent subtitle merging via STLExporter
- **v2.4.0** - Phase 3: New FormatIO API available with deprecation warnings
- **v3.0.0** - Phase 4: Breaking changes and unified CLI

---

## Phase 1: Extract Common I/O (v2.2.0) ✅ COMPLETE

**Goal:** Internal refactoring without breaking changes

### Completed ✅

- [x] Create `FormatIOBase` abstract class with conditional disposal pattern
- [x] Extract common properties (InputFile, Input, Output, OutputFormat, Function)
- [x] Extract common methods (SetOutput, disposal patterns)
- [x] Refactor VBI, T42, TS, MXF to inherit from FormatIOBase
- [x] Create `lib/Core/FormatIOBase.cs`
- [x] Extract MXFData from nested class to top-level `ANC` class
  - [x] Create `/lib/Formats/ANC.cs` with full ANC implementation
  - [x] Rename Format enum: `MXFData` → `ANC`
  - [x] Update MXF.cs with backward compatibility wrapper (MXF.MXFData extends ANC)
- [x] Update all references from MXFData → ANC
  - [x] Update `/tests/MemoryBenchmarkTests.cs`
  - [x] Update `/lib/Functions.cs` (all switch cases, format parsing)
  - [x] Update `/lib/Line.cs`, `/lib/AsyncHelpers.cs`, `/lib/Formats/VBI.cs`
- [x] Remove debug statements from T42.cs
- [x] Create unit tests (30 new tests added)
  - [x] Create `/tests/Core/FormatIOBaseTests.cs` (19 tests)
  - [x] Create `/tests/Formats/ANCTests.cs` (11 tests)
- [x] Run all existing tests - **33/33 tests passing** ✅
- [x] Remove orphaned `AsyncProcessingHelpers` class
  - [x] Delete 288 lines of unused wrapper methods from `/lib/AsyncHelpers.cs`
  - [x] Keep `ProgressReporter` utility class for future use
  - [x] All async operations now use `Functions.*Async()` methods directly
- [x] Update documentation (TODO.md, NEXT.md)

**Success Criteria:**

- [x] ~400 lines of code removed (duplicated properties/methods) ✅
  - **Result:** ~600 lines eliminated total
    - ~300 lines from SetOutput(), Dispose(), property declarations
    - ~288 lines from removing orphaned AsyncProcessingHelpers
- [x] All 5 format classes inherit from FormatIOBase (VBI, T42, TS, MXF, ANC) ✅
- [x] 100% test coverage maintained - 33/33 tests passing (+30 new tests) ✅
- [x] No breaking changes to public API - MXF.MXFData wrapper preserved ✅

---

## Phase 2: Define Abstractions (v2.2.0) ✅ COMPLETE

**Goal:** Introduce interfaces while maintaining backward compatibility

**Note:** Combined with Phase 1 into v2.2.0 to deliver a complete internal foundation in one release.

### Completed ✅

- [x] Define `IFormatHandler` interface for Line-based formats
- [x] Define `IPacketFormatHandler` interface for Packet-based formats
- [x] Create `FormatRegistry` class
- [x] Extend `ParseOptions` class with StartTimecode and PIDs
- [x] Implement `T42Handler` (Line-based format)
- [x] Implement `VBIHandler` (Line-based format)
- [x] Implement `ANCHandler` (Packet-based format)
- [x] Update `T42.cs` to delegate to T42Handler internally
- [x] Update `VBI.cs` to delegate to VBIHandler internally
- [x] Update `ANC.cs` to delegate to ANCHandler internally
- [x] Add tests for new infrastructure (33 new tests)
- [x] Create `lib/Core/IFormatHandler.cs`
- [x] Create `lib/Core/IPacketFormatHandler.cs`
- [x] Create `lib/Core/FormatRegistry.cs`
- [x] Create `lib/Core/ParseOptions.cs`
- [x] Create `lib/Handlers/T42Handler.cs`
- [x] Create `lib/Handlers/VBIHandler.cs`
- [x] Create `lib/Handlers/ANCHandler.cs`
- [x] Create `/tests/Core/ParseOptionsTests.cs` (13 tests)
- [x] Create `/tests/Core/FormatRegistryTests.cs` (14 tests)
- [x] Create `/tests/Handlers/T42HandlerTests.cs` (15 tests)
- [x] All tests passing - **66/66 tests passing** ✅
- [x] Implement `TSHandler` (Packet-based format)
  - [x] Refactored ~1070 lines of TS parsing logic with internal state
  - [x] Moved `_pesBuffers`, `_continuityCounters`, `_pmtPIDs`, `_teletextPIDs`, `_videoPIDs` into handler
  - [x] Moved `DetectPacketSize`, `DetectFrameRateFromVideo`, `ProcessPAT`, `ProcessPMT` methods
  - [x] TS.cs reduced from ~1000 lines to ~140 lines
  - [x] All existing tests passing (awaiting TS test files for TSHandler-specific tests)
- [x] Create `lib/Handlers/TSHandler.cs`
- [x] Update `TS.cs` to delegate to TSHandler internally
- [x] Implement `MXFHandler` (Packet-based format)
  - [x] Refactored ~1600 lines of MXF parsing logic with internal state
  - [x] Moved KLV parsing, BER length decoding, demux mode, output stream management into handler
  - [x] Moved RestripeTimecodeComponent, RestripeSystemPacket, ExtractPacket, ProcessSystemPacket methods
  - [x] MXF.cs reduced from ~1526 lines to ~467 lines
  - [x] All existing tests passing (awaiting MXF test files for MXFHandler-specific tests)
- [x] Create `lib/Handlers/MXFHandler.cs`
- [x] Update `MXF.cs` to delegate to MXFHandler internally

### Future Work (Handler-Specific Tests)

- [x] Create `/tests/Handlers/TSHandlerTests.cs` ✅
- [x] Create `/tests/Handlers/MXFHandlerTests.cs` ✅
- [x] Create `/tests/Handlers/VBIHandlerTests.cs` ✅
- [x] Create `/tests/Handlers/ANCHandlerTests.cs` ✅

**Implementation Notes:**

- **Line-based formats complete:** T42 and VBI fully integrated with handler pattern ✅
- **Simple packet format complete:** ANC fully integrated with handler pattern ✅
- **Complex packet format (TS) complete:** TSHandler fully integrated with ~1070 lines encapsulated ✅
- **Complex packet format (MXF) complete:** MXFHandler fully integrated with ~1600 lines encapsulated ✅

**Success Criteria:**

- [x] IFormatHandler interface fully tested ✅
- [x] IPacketFormatHandler interface created ✅
- [x] FormatRegistry can register/retrieve handlers ✅
- [x] All 5 format handlers implemented (T42, VBI, ANC, TS, MXF) ✅
- [x] All 5 format classes delegate to handlers ✅
- [x] All existing tests still pass - 66/66 tests passing ✅
- [x] No breaking changes to public API ✅
- [x] All format handlers complete and consistent ✅
- [x] Phase 2 complete - v2.2.0 ready for release ✅

---

## Phase 3: Centralize Conversions & New API (v2.4.0)

**Goal:** Move all format conversion logic to FormatConverter + introduce new FormatIO public API + add MXF video VBI extraction

**Note:** v2.3.0 released with STL export support. Phase 3 combines conversion centralization with new API introduction.

### Core Conversion Tasks ✅ COMPLETE

- [x] Create `FormatConverter` static class ✅
- [x] Move `VBI.ToT42()` → `FormatConverter.VBIToT42()` ✅
- [x] Move `T42.ToVBI()` → `FormatConverter.T42ToVBI()` ✅
- [x] Move `Line.ToRCWT()` → `FormatConverter.T42ToRCWT()` ✅
- [x] Move `Line.ToSTL()` → `FormatConverter.T42ToSTL()` ✅
- [x] Add VBI/VBI_DOUBLE helper methods (`VBIToVBIDouble()`, `VBIDoubleToVBI()`) ✅
- [x] Consolidate duplicate `EncodeTimecodeToSTL()` methods ✅
- [x] Consolidate duplicate `ExtractSTLTextData()` methods ✅
- [x] Update all format handlers to use FormatConverter ✅
- [x] Add `[Obsolete]` attributes to old methods ✅
- [x] Create comprehensive test suite (28 tests in FormatConverterTests.cs) ✅
- [x] Update documentation (CHANGELOG.md) ✅

### FFmpeg.AutoGen Integration (CLI-only)

- [ ] Add FFmpeg.AutoGen NuGet package to opx project
- [ ] Create `VideoVBIExtractor` class in opx/Core/
- [ ] Implement PAL line extraction (default: 28:2)
- [ ] Implement NTSC line extraction (optional)
- [ ] Integrate with existing ParseOptions
- [ ] Add `--extract-vbi` command-line option
- [ ] Add `--vbi-lines` command-line option
- [ ] Add tests with sample MXF files
- [ ] Create integration tests with MXF video files
- [ ] Update documentation in docs/NEXT.md
- [ ] Add example usage in README.md

### FormatIO Public API ✅ COMPLETE

- [x] Implement complete `FormatIO` class with fluent API ✅
- [x] Implement `Open()`, `OpenStdin()`, `Open(Stream)` methods ✅
- [x] Implement `ParseLines()`, `ParsePackets()`, and async variants ✅
- [x] Implement `ConvertTo()` fluent method ✅
- [x] Implement `Filter()` fluent method ✅
- [x] Implement `SaveTo()` and `SaveToAsync()` methods ✅
- [x] Implement `WithOptions()`, `WithLineCount()`, `WithStartTimecode()`, `WithPIDs()` for configuration ✅
- [x] Add format auto-detection ✅
- [x] Make old API available alongside new API (both work simultaneously) ✅
- [x] Add deprecation warnings to old API constructors ✅
- [x] Create `lib/FormatIO.cs` (~870 lines) ✅
- [x] Add unit tests for FormatIO class (84 tests in FormatIOTests.cs) ✅
- [x] Add integration tests for all workflows ✅
- [x] Dual parsing modes (ParseLines/ParsePackets) for VBI vertical offset support ✅
- [x] Add FormatRegistry static constructor for automatic handler registration ✅
- [x] Migrate `Functions.Filter()` to use FormatIO API (120 → 60 lines, 50% reduction) ✅
- [x] Migrate `Functions.FilterAsync()` to use FormatIO API (140 → 75 lines, 46% reduction) ✅
- [x] Remove format-specific switch statements in Filter functions ✅

**Success Criteria:**

- [x] All conversion logic in one place (FormatConverter) ✅
- [x] New FormatIO API fully functional alongside old API ✅
- [x] Old methods and constructors still work but show warnings ✅
- [x] All handlers use FormatConverter ✅
- [x] FormatRegistry auto-registers all handlers on first access ✅
- [x] Filter functions migrated to FormatIO with zero deprecation warnings ✅
- [x] Documentation updated with migration examples ✅
- [x] All tests pass (317/317 tests passing) ✅
- [ ] FFmpeg.AutoGen integration working for video VBI extraction (deferred)
- [x] Users have clear migration path from old to new API ✅

---

## Phase 4: Breaking Changes & Unified CLI (v3.0.0) ⚠️ BREAKING

**Goal:** Remove deprecated code, unify CLI commands

**Note:** v2.5.0 skipped - going directly from v2.4.0 to v3.0.0 after sufficient migration period (2-3 months).

### CLI Changes

- [ ] Remove old `filter` command from CLI
- [ ] Remove old `extract` command from CLI
- [ ] Remove old separate `convert` command (replace with unified version)
- [ ] Create new unified `convert` command in CLI
- [ ] Test all command variations

### Library Cleanup

- [ ] Remove deprecated methods from format classes (VBI.ToT42(), T42.ToVBI(), etc.)
- [ ] Remove backward compatibility wrappers

### Documentation & Migration

- [ ] Update all documentation
- [ ] Create `docs/MIGRATION.md` guide
- [ ] Update README.md with v3.0 examples
- [ ] Update `CHANGELOG.md` with v3.0 entry
- [ ] Write blog post / announcement
- [ ] Create side-by-side migration examples

### Testing & Validation

- [ ] Update all tests to new API
- [ ] Remove tests for deprecated code
- [ ] Integration tests for unified `convert` command
- [ ] End-to-end tests for all format pairs
- [ ] Performance validation (compare to baseline)
- [ ] Performance benchmarks show <5% regression

**Success Criteria:**

- [ ] FormatIO API fully functional
- [ ] All format handlers complete
- [ ] New `convert` command works for all use cases
- [ ] Old commands removed
- [ ] Deprecated code removed
- [ ] 100% test coverage
- [ ] Documentation complete
- [ ] Performance benchmarks within tolerance

---

## Testing Goals

### Coverage

- [ ] >85% unit test coverage
- [ ] Integration tests for all format combinations
- [ ] Performance tests with <5% regression tolerance
- [ ] CLI tests for all command variations

### Specific Test Suites

- [ ] FormatIO unit tests
- [ ] IFormatHandler tests for all handlers
- [ ] FormatRegistry tests
- [ ] FormatConverter tests
- [ ] CLI integration tests
- [ ] Performance benchmarks (BenchmarkDotNet)
- [ ] Memory profiling (no leaks)

---

## Documentation

- [ ] Update README.md with v3.0 examples
- [ ] Create migration guide (docs/MIGRATION.md)
- [ ] Update API documentation
- [ ] Create blog post for v3.0 announcement
- [ ] Update command-line help text
- [ ] Create example projects showcasing new API

---

## Release Checklist (Consolidated)

### v2.2.0 - Internal Foundation (Phases 1 + 2) ✅

- [x] Phase 1 complete: FormatIOBase ✅
- [x] Phase 2 complete: IFormatHandler, IPacketFormatHandler, FormatRegistry, ParseOptions ✅
  - [x] Interfaces and registry complete ✅
  - [x] All 5 format handlers complete (T42, VBI, ANC, TS, MXF) ✅
- [x] Tests passing - 66/66 tests ✅
- [x] Internal documentation updated ✅
- [ ] Performance baseline established
- [x] No breaking changes to public API ✅

### v2.3.0 - STL Export 🚧

- [x] STLExporter class with intelligent subtitle merging ✅
- [x] Content-based tracking (handles row-shifting) ✅
- [x] Text growth detection (word-by-word buildup) ✅
- [x] Delayed clear mechanism (30 frames buffer) ✅
- [x] `--stl-merge` CLI option for intelligent merging (default is raw output) ✅
- [x] 30 unit tests in STLExporterTests.cs ✅
- [x] Functions.cs integration ✅
- [x] CLI documentation updated ✅
- [x] CHANGELOG.md updated ✅
- [ ] Tag and release

### v2.4.0 - New API + Deprecation (Phase 3)

- [x] Phase 3 complete: FormatConverter ✅
- [x] FormatIO public API implemented and tested (84 tests) ✅
- [x] FormatRegistry auto-registration implemented ✅
- [x] Filter functions migrated to FormatIO ✅
- [ ] FFmpeg.AutoGen integration complete (deferred)
- [x] Deprecation warnings in place ✅
- [x] Old API works alongside new API ✅
- [x] Migration guide published (CHANGELOG.md) ✅
- [ ] Side-by-side examples documented
- [x] Tests passing (317/317) ✅
- [ ] Deprecation blog post published

### v3.0.0 - Breaking Changes (Phase 4)

- [ ] Phase 4 complete: Unified CLI
- [ ] Breaking changes documented
- [ ] Migration guide complete
- [ ] Old commands removed
- [ ] Deprecated code removed
- [ ] Performance validated (<5% regression)
- [ ] Community announcement ready
- [ ] NuGet package published

---

**Note:** v2.5.0 skipped to go directly from v2.4.0 to v3.0.0 after sufficient migration period.

---

## Future Improvements: STLExporter

The new intelligent STL merging (`lib/Exporters/STLExporter.cs`) dramatically reduces subtitle count when converting from frame-based formats (MXF, ANC, TS) to STL. There is room for further enhancement:

### Potential Improvements

- [ ] **Configurable clear delay** - Expose `ClearDelayFrames` (currently 30 frames) as CLI option for tuning
- [ ] **T42 control code interpretation** - Parse teletext control bytes (clear line, double height, etc.) for more accurate end-time detection
- [ ] **Multi-row subtitle grouping** - Detect and merge related content across multiple rows (e.g., two-line captions)
- [ ] **Punctuation-aware merging** - Use sentence boundaries (periods, question marks) to improve subtitle segmentation
- [ ] **Speaker identification** - Detect speaker changes based on content patterns or row positioning
- [ ] **Maximum duration limits** - Split very long subtitles that exceed typical reading time thresholds
- [ ] **Overlap detection** - Handle cases where new content appears before old content is cleared

### Configuration Options to Consider

- `--stl-clear-delay <frames>` - Number of frames to wait before emitting cleared content
- `--stl-max-duration <seconds>` - Maximum subtitle duration before forced split
- `--stl-merge-rows` - Enable cross-row subtitle merging

---

## Notes

- Track issues with `v3.0-design` label on GitHub
- Document decisions in NEXT.md
- Keep backward compatibility until v3.0.0
- Maintain >85% test coverage throughout
