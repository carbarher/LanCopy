# Security Model

## Core protections
- TLS with TOFU certificate pinning.
- Optional PIN authentication.
- Path confinement to shared root.
- Anti-DoS limits (header length, rate limits, connection caps).
- Integrity verification (SHA-256).

## Design goals
- Local-first transfer model.
- No cloud dependency.
- Explicit tradeoffs for compatibility (TLS fallback is signaled).

## Operational recommendations
- Keep TLS enabled by default.
- Use PIN in shared/untrusted LANs.
- Restrict share root and keep read-only mode where possible.
- Monitor release notes for security fixes.
