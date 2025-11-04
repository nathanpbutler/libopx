# libopx v3.0 Implementation TODO

**Status:** Phase 1 COMPLETE ✅ | Ready for v2.2.0 Release
**Last Updated:** 2025-11-04
**Release Strategy:** Consolidated to 3 releases (v2.2.0, v2.4.0, v3.0.0)

---

## Consolidated Release Strategy

To avoid flooding NuGet with rapid incremental releases:

- **v2.2.0** - Phases 1 + 2: Internal foundation (FormatIOBase + IFormatHandler abstractions)
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

## Phase 2: Define Abstractions (v2.2.0)

**Goal:** Introduce interfaces while maintaining backward compatibility

**Note:** Combined with Phase 1 into v2.2.0 to deliver a complete internal foundation in one release.

- [ ] Define `IFormatHandler` interface
- [ ] Create `FormatRegistry` class
- [ ] Create `ParseOptions` class
- [ ] Implement `T42Handler` as proof of concept
- [ ] Create adapter layer so `T42` class delegates to `T42Handler`
- [ ] Implement remaining format handlers (VBIHandler, TSHandler, MXFHandler, ANCHandler)
- [ ] Add tests for new infrastructure
- [ ] Create `lib/Core/IFormatHandler.cs`
- [ ] Create `lib/Core/FormatRegistry.cs`
- [ ] Create `lib/Core/ParseOptions.cs`
- [ ] Create `lib/Handlers/T42Handler.cs`
- [ ] Update `T42.cs` to use T42Handler internally
- [ ] Tests for new components

**Success Criteria:**

- [ ] IFormatHandler interface fully tested
- [ ] FormatRegistry can register/retrieve handlers
- [ ] T42Handler implemented and tested
- [ ] T42 class delegates to T42Handler
- [ ] All existing tests still pass
- [ ] No breaking changes to public API

---

## Phase 3: Centralize Conversions & New API (v2.4.0)

**Goal:** Move all format conversion logic to FormatConverter + introduce new FormatIO public API + add MXF video VBI extraction

**Note:** v2.3.0 skipped - combining conversion centralization with new API introduction to avoid intermediate releases.

### Core Conversion Tasks

- [ ] Create `FormatConverter` static class
- [ ] Move `VBI.ToT42()` → `FormatConverter.VBIToT42()`
- [ ] Move `T42.ToVBI()` → `FormatConverter.T42ToVBI()`
- [ ] Move `Line.ToRCWT()` → `FormatConverter.T42ToRCWT()`
- [ ] Move `Line.ToSTL()` → `FormatConverter.T42ToSTL()`
- [ ] Update all format handlers to use FormatConverter
- [ ] Add `[Obsolete]` attributes to old methods
- [ ] Update documentation with migration examples

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

### FormatIO Public API

- [ ] Implement complete `FormatIO` class with fluent API
- [ ] Implement `Open()`, `OpenStdin()`, `Open(Stream)` methods
- [ ] Implement `Parse()` and `ParseAsync()` methods
- [ ] Implement `ConvertTo()` fluent method
- [ ] Implement `Filter()` fluent method
- [ ] Implement `SaveTo()` and `SaveToAsync()` methods
- [ ] Implement `WithOptions()` for configuration
- [ ] Add format auto-detection
- [ ] Make old API available alongside new API (both work simultaneously)
- [ ] Add deprecation warnings to old API methods
- [ ] Create `lib/FormatIO.cs`
- [ ] Add unit tests for FormatIO class
- [ ] Add integration tests for all workflows
- [ ] Create side-by-side API comparison examples

**Success Criteria:**

- [ ] All conversion logic in one place (FormatConverter)
- [ ] New FormatIO API fully functional alongside old API
- [ ] Old methods still work but show warnings
- [ ] All handlers use FormatConverter
- [ ] Documentation updated with migration examples
- [ ] All tests pass with deprecation warnings
- [ ] FFmpeg.AutoGen integration working for video VBI extraction
- [ ] Users have clear migration path from old to new API

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

### v2.2.0 - Internal Foundation (Phases 1 + 2)

- [x] Phase 1 complete: FormatIOBase
- [ ] Phase 2 complete: IFormatHandler, FormatRegistry, ParseOptions
- [ ] All format handlers implemented
- [ ] Tests passing
- [ ] Internal documentation updated
- [ ] Performance baseline established
- [ ] No breaking changes to public API

### v2.4.0 - New API + Deprecation (Phase 3)

- [ ] Phase 3 complete: FormatConverter
- [ ] FormatIO public API implemented and tested
- [ ] FFmpeg.AutoGen integration complete
- [ ] Deprecation warnings in place
- [ ] Old API works alongside new API
- [ ] Migration guide published
- [ ] Side-by-side examples documented
- [ ] Tests passing
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

**Note:** v2.3.0 and v2.5.0 skipped to avoid flooding NuGet with intermediate releases.

---

## Notes

- Track issues with `v3.0-design` label on GitHub
- Document decisions in NEXT.md
- Keep backward compatibility until v3.0.0
- Maintain >85% test coverage throughout
