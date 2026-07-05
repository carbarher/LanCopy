# Core Scope

LanCopy Core is the safe default product surface:

- LAN file transfer
- encrypted local connection by default
- shared-folder confinement
- resumable upload/download
- integrity verification
- safe per-peer trust and permissions

## Out of Core

These stay in Advanced mode or require explicit opt-in:

- remote power
- remote delete
- chat between connected computers
- full-disk browsing
- direct protocol/admin APIs

## Design rule

If a feature can surprise a new user, break data safety, or widen access
outside the shared folder, it is not Core.
