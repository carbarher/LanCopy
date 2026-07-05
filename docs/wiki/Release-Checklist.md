# Release Checklist

Use this before promoting a LanCopy release.

## 1. Version

- Confirm `LanCopy.csproj` version matches the intended release version.
- Confirm the app title shows the same version after launching.
- Confirm public GitHub tag/release uses the same version.

## 2. CI

- Public repo CI is green on `master`.
- Release/tag workflow is green.
- Tests pass locally or in CI.
- No build artifacts are committed (`bin`, `obj`, `publish`, `.exe`, `.pdb`, `.zip`).

## 3. Desktop Smoke Test

- First launch starts protected.
- First local and remote views start at drives when no previous folder exists.
- Last folders are remembered after reconnect.
- Send one file.
- Receive one file.
- Rename asks for confirmation.
- Delete asks for confirmation and does not allow protected system folders.
- Chat opens from button.
- Incoming chat opens automatically.
- Closing the app leaves no background process.

## 4. Network Smoke Test

- Discovery works on normal Wi-Fi.
- Manual IP works from Advanced.
- Cable/router or switch setup works.
- Firewall-blocked scenario shows useful help.
- `Can't see the other PC?` mentions Wi-Fi and cable.

## 5. Installer

- Windows installer installs on a clean Windows x64 machine.
- Start Menu shortcut works.
- Optional desktop shortcut works.
- Optional firewall rules are created.
- Uninstall removes app shortcuts and firewall rules.
- Uninstall does not unexpectedly delete user data.

## 6. Public Documentation

- README loads correctly on GitHub.
- Screenshots render.
- User Guide is linked.
- CLI Guide is linked.
- API Reference is linked.
- Known Limitations are linked.
- Download links resolve.

## 7. API and CLI

- `peers --wait 5 --json` works.
- `send` works against a desktop peer.
- `sync` works on a small folder.
- `api --port 3489` starts locally.
- `/api/v1/health` responds without token.
- `/api/v1/openapi.json` responds without token.
- protected endpoints reject missing/invalid token.
- `transfer status`, `transfer cancel` and `transfer retry` work with token.

## 8. Release Notes

- Changelog has a clear entry.
- Known limitations are honest.
- Installer/API/CLI preview status is clear.
- No removed feature is still advertised.