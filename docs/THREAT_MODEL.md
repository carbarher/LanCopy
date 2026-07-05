# Threat Model

## Assumptions

- Peers are on an untrusted LAN.
- Device identity can change.
- The network may contain passive observers and active attackers.
- Users may accidentally approve risky actions.

## Key protections

- TLS with TOFU certificate pinning.
- Optional PIN authentication.
- Shared-folder confinement by default.
- Command authorization before handler execution.
- Safe Mode defaults for high-risk features.
- Integrity checks on transfer data.

## Main risks

- unauthorized file reads or writes
- path traversal and reparse-point escapes
- replay or downgrade attempts
- identity swap / fingerprint change
- misuse of remote power or delete

## Security posture

LanCopy treats unknown peers as minimal-status only and requires explicit
trust plus explicit permissions for dangerous operations.
