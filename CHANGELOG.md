# Changelog

All notable changes to LanCopy are documented in this file.

## [1.6.7] - 2026-07-05

### Added
- Added an independent chat window for conversations between connected computers.
- Chat now supports sender labels, multiline writing, reply flow, send button, and automatic opening when a remote message arrives.
- Remote and local browsers now start from disks on first use and remember the last folders afterwards.
- Added resizable Name, Size and Date columns on both local and remote file lists.
- Added broader protection for sensitive system folders across Windows, macOS and Linux.

### Changed
- Simplified the user-facing protection model around a protected-computer setting instead of exposing transport/security jargon in the main flow.
- Moved remote shutdown and restart into Advanced.
- Replaced remote search with a clearer current-remote-path display.
- Removed local file filtering from the main local browser.
- Updated help, labels and translations for all supported languages around connection, chat, protected mode and dangerous actions.

### Fixed
- Fixed same-version connection failures caused by strict/compatibility mismatch handling.
- Fixed app shutdown leaving background work alive after closing the desktop app.
- Fixed incorrect initial remote path display.
- Fixed startup state so protected mode is shown as enabled when the computer is protected.
- Fixed access-denied and connection messages to be more useful for normal users.

### Removed
- Removed clipboard sync, clipboard text sending, copy/paste shortcut helpers and clipboard permissions from the desktop app and public documentation.

### Testing
- Test suite passes at **251/251**.

## [1.0.17] - 2026-07-02

### Fixed
- Hardened multiple `async void` UI handlers with top-level `try/catch` to prevent unobserved crashes (connection, context menus, features, browsers, queue actions, profile/sync actions).
- Fixed update checker HTTP body handling to read until EOF (previously could fail on partial TCP reads).
- Fixed progress window auto-close behavior so action buttons ("Open folder"/"Open file") remain usable after successful downloads.
- Fixed CLI transfer cancellation behavior: `Ctrl+C` now requests cooperative cancellation instead of abrupt termination.
- Fixed CLI transfer runtime edge cases around cancellation/dispose races and persistence errors.

### Security & Reliability
- Removed/converted silent catches to structured logging across services and UI paths.
- Improved remote/local operation robustness with explicit error surfacing and safer null handling in UI event flows.
- Added reparse-point filtering in CLI sync to avoid traversing symlinks/junctions by default.

### Performance
- Reduced hot-path allocations in transfer and hashing flows (pooling and zero-allocation aggregation improvements).
- Optimized repeated progress/status aggregation and hash computation code paths.

### Testing
- Expanded regression coverage for CLI/runtime behaviors and parsing helpers.
- Test suite now passes at **132/132**.

## [1.0.0] - 2026-06-17

### Features
- **Direct LAN transfers** - peer-to-peer file transfer with encrypted local connections.
- **Auto-discovery** - UDP broadcast finds peers on the same network automatically.
- **Easy pairing** - voice-friendly codes, QR codes, or `lancopy://` protocol links.
- **Integrity verification** - streaming SHA-256 hash on every transfer.
- **Resumable downloads** - interrupted transfers continue from the last byte.
- **Optional PIN** - constant-time comparison with per-IP rate limiting and exponential backoff.
- **Folder sync** - one-way or two-way directory synchronization.
- **Watch mode** - automatically transfer files when they appear in a folder.
- **Bandwidth limiting** - token-bucket rate limiter (configurable Mbps).
- **20 languages** - including RTL support (Arabic).

### Security
- **Path confinement** - restricted to shared folder; blocks path traversal, symlinks, junctions.
- **Anti-DoS** - line-length cap (1 MB), connection limits, anti-zipbomb decompression cap.
- **Command rate-limiting** - per-IP+command tracking prevents brute force.
- **Health endpoint** - monitor peer connection state, cache, rate-limit metrics.
- **Robust queue persistence** - atomic saves, corruption recovery, retry tracking.

### Platforms
- Windows x64, ARM64 (Surface Pro X, Snapdragon PCs)
- Linux x64, ARM64 (Raspberry Pi, etc.)
- macOS Apple Silicon (M1/M2/M3), Intel

### Testing
- 96 unit tests covering path confinement, real transfers, hash integrity, queue persistence.
- CI/CD pipeline builds and tests on Windows, Linux, macOS.
- Automated release builds for all platforms on version tags.

### Known Limitations
- Designed for trusted local networks, not internet exposure.
- PIN is optional and recommended on shared networks.
- Rate-limit window resets on app restart.
