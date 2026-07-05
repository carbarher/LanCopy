# Security Model

## Core protections
- TLS with TOFU certificate pinning.
- Optional PIN authentication.
- Path confinement to shared root.
- Safe Mode defaults (TLS + shared-root + no remote delete + no remote power).
- Protected mode keeps risky remote actions disabled unless explicitly allowed.
- Anti-DoS limits (header length, rate limits, connection caps).
- Integrity verification (SHA-256).
- Trusted device list + forget flow for TOFU identities.
- Per-peer command permissions enforced server-side before handlers execute.
- Trusted Devices offers presets: Read only, Send/receive, Full share, Advanced trusted.
- Peer trust levels: `Unknown`, `Paired`, `Trusted`, `OwnerDevice`.
- `Unknown` peers can only request minimal status commands (`caps`, `health`, `disconnect_notice`); `Paired` means the identity is known but permissions stay limited.
- Default permissions keep `browse`/`download` on and leave write/sync/power off until explicitly enabled.
- Default trust policy blocks high-risk commands (`delete`, `sync/delta`, `power`) unless peer is `Trusted` or `OwnerDevice`.
- Remote delete and remote power both use cooldowns to slow repeated risky actions.
- Safe Mode can be temporarily disabled for a fixed window and then restores itself automatically.

| Trust level | Default posture |
|-------------|-----------------|
| Unknown | Minimal status only |
| Paired | Limited access |
| Trusted | Advanced opt-in |
| OwnerDevice | Full local trust |

| Preset | Permissions |
|--------|-------------|
| Read only | browse, download |
| Send/receive | browse, download, upload |
| Full share | browse, download, upload, modify |
| Advanced trusted | all permissions |

## Design goals
- Local-first transfer model.
- No cloud dependency.
- Protected connection mode is strict by default; compatibility fallback is explicit.
- High-risk commands are rejected unless the connection and permission state allow them.
- Full-disk browsing is temporary and reverts automatically.
- Local API is localhost-only, token-protected, and opt-in at runtime.
- Identity change on a known device fingerprint is treated as a blocked connection.

## Operational recommendations
- Keep protected connection settings enabled by default.
- Use PIN in shared/untrusted LANs.
- Restrict share root and keep read-only mode where possible.
- Monitor release notes for security fixes.
