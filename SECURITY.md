# Security Policy

## Reporting a vulnerability
If you discover a security vulnerability in LanCopy, please report it
**privately**. Do not open a public issue.

- Use GitHub''s [private vulnerability reporting](https://github.com/carbarher/LanCopy/security/advisories/new), or
- Contact the maintainer directly through the repository profile.

Please include a description, reproduction steps, and the potential impact.
You can expect an initial response within a reasonable time frame.

## Scope
LanCopy is designed for use on trusted local networks. Its security model:

- **Path confinement**: the server confines all file operations to a shared
  folder by default and blocks path traversal and symlink/junction escapes.
- **TLS + TOFU**: traffic can be encrypted with Trust-On-First-Use certificate
  pinning. A changed fingerprint is treated as a possible MITM and rejected.
- **PIN authentication**: optional, with constant-time comparison and per-IP
  rate limiting with backoff.
- **Anti-DoS**: protocol line-length cap, concurrent and per-IP connection
  limits, and an anti zip-bomb cap during decompression.

## Supported versions
The latest released version receives security fixes.
