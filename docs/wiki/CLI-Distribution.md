# CLI Distribution Decision

Decision: keep the CLI as a developer/automation tool for now, not as the primary user-facing download.

## Current Position

- The desktop app is the main product for normal users.
- The CLI is supported for scripts, local automation, peer discovery, file send, folder sync and local API control.
- The local API is preview/stable enough for internal automation and integrations, but should remain clearly labeled as local-only.

## Why Not a Separate Main Download Yet

- Most users need the desktop app, not command-line tooling.
- A separate CLI package increases release, support and documentation surface.
- The CLI depends on the same local-network assumptions and should not be mistaken for a cloud transfer client.

## Packaging Rule

For now:

- Keep CLI source and tests in the repository.
- Document CLI usage in `docs/wiki/CLI-Guide.md`.
- Keep API scripts and Postman collection under `scripts/api/`.
- Do not advertise the CLI as the default download.

Future trigger for a separate CLI artifact:

- repeated user requests for headless use
- CI/scheduled transfer scenarios
- package-manager demand
- stable API contract across several releases

When that happens, publish a clearly named artifact such as `LanCopy.Cli-win-x64.exe` and document it separately from the desktop app.