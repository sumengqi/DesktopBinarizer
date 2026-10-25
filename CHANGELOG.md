# Changelog

All notable changes to this project are documented in this file.

## Unreleased

### Changed

- Optimized non-dithered RGB quantization with precomputed channel lookup tables.
- Reduced pixel-grid preview work by rendering each logical row once and reusing it.
- Reduced transient-frame comparison memory traffic by copying only sampled rows.
- Made transient-frame filter disposal thread-safe during capture shutdown.
- Updated NAudio.Wasapi from 2.3.0 to 2.4.0 while retaining .NET 8 support.

### Tests

- Added exact RGB fast-path, fractional pixel-grid scaling, and disposal lifecycle coverage.

## 1.0.0 - 2026-10-22

- Initial public release.
