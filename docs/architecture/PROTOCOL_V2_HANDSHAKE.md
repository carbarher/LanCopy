# Protocol v2 Capability Handshake

## Purpose

Negotiate capabilities before command execution so the client can adapt safely.

## Proposed fields

- protocol version
- trust level
- capability flags
- safe-mode indicators

## Rules

- Unknown capabilities must fail closed.
- Old peers must continue working with the current safe fallback behavior.
- Version gates should be explicit and test-covered.

