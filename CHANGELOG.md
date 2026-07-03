# Changelog

All notable changes to LanCopy are documented in this file.

## [1.5.5] — 2026-07-03

### Features
- Universal Clipboard: automatic clipboard sync between peers
- Push Links: auto-open received URLs on the remote machine
- Quick-access bookmarks (Favorites) in local/remote file browsers
- Remote Shutdown/Restart (LanControl) with confirmation dialog and PIN
- Recursive remote search with 500ms debounce and 250-entry cap
- Trash Restore: restore files and directories from the Audit window

### Fixed
- 47 critical bug fixes: race conditions (TOCTOU), SemaphoreSlim deadlocks, TLS SafeHandle leaks, injection vulnerabilities, and DoS vectors

### Performance
- 20 optimizations (M1-O20): zero-alloc hot paths, FileInfo-based single-syscall, pre-serialized UDP payloads, O(1) active IP tracking, direct stream serialization, ArrayPool for download buffers, lazy-cached FileEntry display properties, single-pass FileSorter

### Infrastructure
- Added `.editorconfig`, `.gitattributes`, expanded `.gitignore`
- Added Dependabot and PR template
- Tests now run cross-platform (removed win-x64 RuntimeIdentifier)

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
- **Direct LAN transfers** — peer-to-peer file transfer with TLS encryption (TOFU certificate pinning)
- **Auto-discovery** — UDP broadcast finds peers on the same network automatically
- **Easy pairing** — voice-friendly codes, QR codes, or `lancopy://` protocol links
- **Integrity verification** — streaming SHA-256 hash on every transfer
- **Resumable downloads** — interrupted transfers continue from the last byte
- **Optional PIN** — constant-time comparison with per-IP rate limiting and exponential backoff
- **Folder sync** — one-way or two-way directory synchronization
- **Watch mode** — automatically transfer files when they appear in a folder
- **Clipboard text send** — quickly share text snippets over the network
- **Bandwidth limiting** — token-bucket rate limiter (configurable Mbps)
- **20 languages** — including RTL support (Arabic)

### Security
- **Path confinement** — restricted to shared folder; blocks path traversal, symlinks, junctions
- **Anti-DoS** — line-length cap (1 MB), connection limits, anti-zipbomb decompression cap
- **Command rate-limiting** — per-IP+command tracking prevents brute force
- **Health endpoint** — monitor peer connection state, cache, rate-limit metrics
- **Robust queue persistence** — atomic saves, corruption recovery, retry tracking

### Platforms
- Windows x64, ARM64 (Surface Pro X, Snapdragon PCs)
- Linux x64, ARM64 (Raspberry Pi, etc.)
- macOS Apple Silicon (M1/M2/M3), Intel

### Testing
- 96 unit tests covering path confinement, real transfers, hash integrity, queue persistence
- CI/CD pipeline builds and tests on Windows, Linux, macOS
- Automated release builds for all platforms on version tags

### Known Limitations
- Designed for trusted local networks (no internet-scale security)
- PIN is optional and recommended only for shared networks
- Rate-limit window resets on app restart (no persistent config yet)
