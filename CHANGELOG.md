# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Showrunner MCP server (`showrunner/`) providing a local stdio MCP adapter over the deterministic Showrunner application layer, with initial tools: `show_get`, `show_get_by_date`, `recording_search`, `recording_history`, `show_prepare`, `recording_resolve`, and `repeat_exception_create`.
- CI workflow (`.github/workflows/showrunner-build-and-test.yml`) that builds and tests the Showrunner solution on every change to `showrunner/**`.

### Removed

- Retired the standalone .NET automation solution and its supporting CI, release, scanning, and enrichment workflows.

## [v1.0.0-alpha] - 2024-06-14

### Added

- Show-specific shortcodes and refactored Hugo templates

[v1.0.0-alpha]: https://github.com/colin-gourlay/sundown-sessions/releases/tag/v1.0.0-alpha
