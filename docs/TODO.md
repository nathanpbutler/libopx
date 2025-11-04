# libopx v3.0 Implementation TODO

**Status:** Phase 1 COMPLETE ✅ | Ready for v2.2.0 Release
**Last Updated:** 2025-11-04

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

## Phase 2: Define Abstractions (v2.3.0)

**Goal:** Introduce interfaces while maintaining backward compatibility

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

## Phase 3: Centralize Conversions (v2.4.0)

**Goal:** Move all format conversion logic to FormatConverter + add MXF video VBI extraction

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

**Success Criteria:**

- [ ] All conversion logic in one place (FormatConverter)
- [ ] Old methods still work but show warnings
- [ ] All handlers use FormatConverter
- [ ] Documentation updated with migration examples
- [ ] All tests pass with deprecation warnings
- [ ] FFmpeg.AutoGen integration working for video VBI extraction

---

## Phase 4: New Unified API (v3.0.0) ⚠️ BREAKING

**Goal:** Launch new public API, remove deprecated code

### Core API

- [ ] Implement complete `FormatIO` public API
- [ ] Implement all format handlers (VBI, T42, TS, MXF, ANC)
- [ ] Create `lib/FormatIO.cs` (complete implementation)
- [ ] Remove deprecated methods from format classes

### CLI Changes

- [ ] Create new unified `convert` command in CLI
- [ ] Remove old `filter` command
- [ ] Remove old `extract` command
- [ ] Remove old separate `convert` command (replace with unified version)
- [ ] Test all command variations

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

## Release Checklist

### v2.2.0

- [ ] All Phase 1 tasks complete
- [ ] Tests passing
- [ ] Internal documentation updated
- [ ] Performance baseline established

### v2.3.0

- [ ] All Phase 2 tasks complete
- [ ] New API available alongside old
- [ ] Tests passing
- [ ] Documentation updated

### v2.4.0

- [ ] All Phase 3 tasks complete
- [ ] FFmpeg.AutoGen integration complete
- [ ] Deprecation warnings in place
- [ ] Migration guide published
- [ ] Tests passing

### v3.0.0

- [ ] All Phase 4 tasks complete
- [ ] Breaking changes documented
- [ ] Migration guide complete
- [ ] Performance validated
- [ ] Community announcement ready
- [ ] NuGet package published

---

## Notes

- Track issues with `v3.0-design` label on GitHub
- Document decisions in NEXT.md
- Keep backward compatibility until v3.0.0
- Maintain >85% test coverage throughout
