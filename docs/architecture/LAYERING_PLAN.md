# Core / Transport / App / CLI Layering Plan

## Goal

Reduce coupling by separating:

- Core: protocol, trust, policy, shared models
- Transport: socket/TLS/session plumbing
- App: UI and orchestration
- CLI: command-line/API surface

## Incremental approach

1. Extract shared policy and models first.
2. Move protocol handlers behind transport helpers.
3. Leave UI wiring in App.
4. Keep CLI thin and explicit.

