# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Git LFS support for large file handling in repository

### Fixed

- Broken async MXF restriping methods functionality
- Complete async parsing implementation with memory benchmarks and unified CLI commands

## [1.4.0] - 2025-09-03

### Added

- Async parsing methods with ArrayPool buffer management for 30-45% memory reduction
- Enhanced CLI with Ctrl+C cancellation support and progress reporting
- ParseAsync() methods for all formats (BIN, VBI, T42, MXF)
- AsyncHelpers utility class for improved async operations
- Performance improvements across all parsing operations

### Changed

- README refactor with updated documentation and examples

### Performance

- Implemented ArrayPool buffer management for significant memory reduction
- Optimized async parsing methods for better performance

## [1.3.0] - 2025-08-17

### Added

- Enhanced T42.GetText parsing to handle header rows correctly
- Improved SMPTE XML documentation with reduced warnings

### Fixed

- T42 format parsing edge cases with header row handling

### Documentation

- Reduced SMPTE XML documentation warnings
- Enhanced T42 parsing documentation

## [1.2.0] - 2025-08-16

### Changed

- Refactored CLI commands for better consistency
- Standardized default magazine behavior across all commands
- Improved command-line interface usability

## [1.1.0] - 2025-08-13

### Added

- Comprehensive XML documentation across the entire codebase
- Filter examples in README with new sample files
- Enhanced sample test files and updated .gitignore

### Changed

- Input/output parameters now use arguments instead of options
- Updated README with comprehensive usage examples
- Removed Seek functionality from VBI/T42 classes

### Fixed

- Piping to stdout with convert command functionality
- Command-line argument handling improvements

### Documentation

- Complete XML documentation coverage
- Updated README.md with detailed examples
- Added filter command examples

## [1.0.0] - 2025-08-03

### Added

- **Initial major release** of libopx library
- Unified CLI application (`opx`) consolidating multiple tools:
  - `filter` - Filter teletext data by magazine and rows
  - `extract` - Extract/demux streams from MXF files  
  - `restripe` - Restripe MXF files with new start timecode
  - `convert` - Convert between different teletext data formats
- Complete format support:
  - **MXF (Material Exchange Format)** parsing and extraction
  - **BIN** file processing with streaming support
  - **VBI (Vertical Blanking Interval)** parsing with T42 conversion
  - **T42 (Teletext packet stream)** processing with filtering
- **Format conversion system** with automatic detection and conversion between:
  - BIN ↔ VBI ↔ T42 ↔ MXF formats
  - Automatic VBI-to-T42 conversion during parsing
- **SMPTE timecode support**:
  - Comprehensive timecode calculations with various frame rates
  - Drop-frame mode handling
  - Timecode byte generation for SMPTE integration
  - Sequential timecode validation
- **Teletext processing**:
  - Magazine and row filtering capabilities
  - Unicode character mapping via TeletextCharset
  - Caption row support (rows 1-24) and standard rows (0-24)
- **MXF processing features**:
  - Key-based filtering using KeyType enum (Data, Video, System, etc.)
  - Extract mode for stream extraction to separate files
  - Demux mode for extracting all found keys as individual files
  - KLV mode for including key/length bytes in output
  - Progress tracking and in-place modification for restriping
- **Streaming architecture**:
  - Memory-efficient IEnumerable processing with yield return
  - Stream-based parsing for large files
  - Standard input/output support for Unix-style piping
- **SMPTE metadata system**:
  - Complete SMPTE essence element definitions
  - XML-based metadata loading
  - Comprehensive essence element, group, type, and label support
- **CLI features**:
  - Modern command-line interface using System.CommandLine
  - Verbose output options across all commands
  - Flexible input/output handling with format auto-detection
  - Progress reporting for long-running operations

### Added - Testing & Development

- Comprehensive xUnit test suite with coverlet code coverage
- Sample test files with Git LFS integration
- Performance benchmarking and validation tests
- Memory usage optimization testing
- Timecode validation across various scenarios

### Added - CI/CD & Automation

- GitHub Actions workflows for:
  - Automated testing and code coverage
  - Claude Code Review integration
  - Pull request assistance automation
- PowerShell and shell scripts for test file creation
- FFmpeg integration for test file generation with various frame rates

### Added - Documentation

- Complete project documentation in CLAUDE.md
- Detailed usage examples and command reference
- Architecture documentation and development guidelines
- Format conversion examples and best practices

### Technical Details

- **Target Framework**: .NET 9 with modern C# features
- **Architecture**: Multi-project solution with library and CLI separation
- **Performance**: Optimized with stream-based processing and memory management
- **Publishing**: Single-file deployment with ReadyToRun optimization
- **Dependencies**: Minimal external dependencies with System.CommandLine for CLI

[unreleased]: https://github.com/nathanpbutler/libopx/compare/v1.4.0...HEAD
[1.4.0]: https://github.com/nathanpbutler/libopx/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/nathanpbutler/libopx/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/nathanpbutler/libopx/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/nathanpbutler/libopx/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/nathanpbutler/libopx/releases/tag/v1.0.0
