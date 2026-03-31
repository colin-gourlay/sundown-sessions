# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [contentops/v0.1.0] - 2026-03-21

### Added

- ContentOps automation platform under `automation/dotnet/`
- Self-contained Linux binary for album intake and preparation workflows
- Production Dockerfile for containerised deployment
- Spotify and Lidarr integration libraries
- Clean Architecture solution with Domain, Application, Infrastructure, Cli, and Contracts projects
- Result pattern via `ErrorOr<T>` for explicit failure handling
- Mediator-based command/handler structure with source-generated dispatch
- Correlation ID support per CLI invocation for traceability
- SQLite persistence with `Testcontainers` and `Respawn` for integration tests
- Dev container configuration for repeatable development environments
- Release workflow publishing self-contained binary, Dockerfile, usage instructions, and GHCR image

## [v1.0.0-alpha] - 2024-06-14

### Added

- Show-specific shortcodes and refactored Hugo templates

[contentops/v0.1.0]: https://github.com/colin-gourlay/sundown-sessions/releases/tag/contentops%2Fv0.1.0
[v1.0.0-alpha]: https://github.com/colin-gourlay/sundown-sessions/releases/tag/v1.0.0-alpha