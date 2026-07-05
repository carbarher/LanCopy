# Protocol

This document summarizes the current LANCopy protocol contract.

## Session flow

1. Connect over TCP.
2. Negotiate TLS if enabled.
3. Optionally authenticate with PIN.
4. Send a JSON command header.
5. Receive a JSON response or a binary payload.

## Command policy

Commands are authorized before handlers run.

- Unknown peers: minimal status only.
- Paired peers: limited access.
- Trusted peers: advanced permissions only when explicitly enabled.
- OwnerDevice: local device with full trust.

## High-risk commands

The following require extra caution:

- delete
- power
- sync / delta
- clipboard automation

They are rejected on plaintext connections and require explicit permission.

## Compatibility

Protocol changes must preserve safe defaults and maintain test coverage for
handshake, auth, and transfer behavior.
