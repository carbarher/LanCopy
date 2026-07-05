# LanCopy

> Trust-first LAN file transfer — encrypted by default, private, no cloud.

[![CI](https://github.com/carbarher/LanCopy/actions/workflows/ci.yml/badge.svg)](https://github.com/carbarher/LanCopy/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![GitHub release](https://img.shields.io/github/v/release/carbarher/LanCopy)](https://github.com/carbarher/LanCopy/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/carbarher/LanCopy/total)](https://github.com/carbarher/LanCopy/releases)

LanCopy moves files directly between devices on your local network over an
encrypted-by-default channel. No accounts, no servers, no internet round-trips.
Built with C# / .NET 9 and Avalonia UI. Available in 20 languages.

## Five narrative blocks

1. What LanCopy is.
2. Why the default posture is safe.
3. How to transfer files.
4. What advanced/trust features exist.
5. How to build, test, and get support.

## 20-second demo

1. Start LanCopy on two PCs on the same LAN.
2. Keep Safe Mode on, then share the IP or pairing link.
3. Drag files to send them; use the Advanced panel only for trusted peers.

## Product modes

- **Core**: safe file transfer, TLS, shared-folder confinement, resumable transfer.
- **Advanced**: trusted devices, sync, clipboard, audit, and other opt-in power features.
- **Labs**: protocol and architecture experiments that are not part of the daily transfer flow.

Features outside LanCopy's transfer-and-trust identity stay deferred until there is a clear product need.

## Clipboard modes

- **Send once**: send a short text snippet or clipboard content a single time.
- **Continuous sync**: keep the local clipboard mirrored with a trusted peer when enabled.

---

## Screenshot

![LanCopy Interface](Assets/screenshot.png)

---

## Features

- **Direct LAN transfers** — peer-to-peer, no cloud or third-party servers.
- **Encrypted by default** — TLS with Trust-On-First-Use (TOFU) certificate pinning.
- **Auto-discovery** — peers on the same network appear automatically (UDP).
- **Easy pairing** — short voice-friendly codes, QR codes, or `lancopy://` links.
- **Integrity** — every transfer is verified with a streaming SHA-256 hash.
- **Resumable downloads** — interrupted transfers continue from where they stopped.
- **Optional PIN** — constant-time check with per-IP rate limiting and backoff.
- **Safe by default** — transfers are confined to a shared folder; path traversal,
  symlink/junction escapes, zip-bombs and oversized headers are blocked.
- **Universal Clipboard** — automatic clipboard sync between peers.
- **Push Links** — auto-open received URLs on the remote machine.
- **Quick-access bookmarks** — favorite folders in local/remote file browsers.
- **Remote Shutdown/Restart** — advanced feature, disabled by default.
- **Remote Search** — recursive search with debounce and result cap.
- **Trash Restore** — restore deleted files from the Audit window.
- **Folder sync, watch mode, clipboard text send, bandwidth limit.**
- **20 languages**, including RTL (Arabic).
- **Optional anonymous telemetry (opt-in)** for adoption metrics only.

## Requirements

- .NET SDK 9.0 or later
- Microsoft.AspNetCore.App 9.0 runtime (required by CLI/API and test host)
- (Optional, for the Windows installer) [Inno Setup 6](https://jrsoftware.org/isdl.php)

## Security defaults

- TLS required by default (no silent downgrade to plaintext).
- High-risk remote commands are rejected when the connection is plaintext.
- Shared-folder confinement enabled by default.
- Safe Mode is enabled by default and enforces secure settings.
- Full-disk browsing is temporary (10 minutes) and resets on app restart.
- Remote power disabled by default.
- Remote hard-delete fallback is disabled unless explicitly enabled.
- Auto clipboard sync and auto link opening are disabled by default.
- Local API binds to `127.0.0.1` and requires `X-LanCopy-Token`.
- Local API stays off unless you explicitly run `lancopy-cli api`.
- Plaintext fallback is compatibility mode and must be enabled explicitly.
- Trusted devices can be reviewed/forgotten from the Advanced panel ("🔐 Devices").
- Trusted Devices includes presets: Read only, Send/receive, Full share, and Advanced trusted.
- If a known device fingerprint changes, the connection is blocked until re-paired.
- Per-device permissions can be configured from Trusted Devices (browse/download/upload/modify/delete/sync/clipboard/power).
- Trust levels are available per device: Unknown, Paired, Trusted, OwnerDevice.
- By default, only `browse` and `download` are enabled; write, sync, clipboard, and power stay off until explicitly enabled.
- Safe Mode can be temporarily disabled for 10 minutes and restores itself automatically.
- High-risk commands still require explicit permission even for Trusted devices.
- Unknown peers are limited to minimal status commands only.

| Trust level   | Default posture |
|---------------|-----------------|
| Unknown       | Minimal status only |
| Paired        | Limited access  |
| Trusted       | Advanced opt-in  |
| OwnerDevice   | Full local trust |

| Preset | Permissions |
|--------|-------------|
| Read only | browse, download |
| Send/receive | browse, download, upload |
| Full share | browse, download, upload, modify |
| Advanced trusted | all permissions |

## Build & Run

```powershell
dotnet build LanCopy.csproj -c Release
dotnet run --project LanCopy.csproj
```

## Tests

```powershell
dotnet test tests/LanCopy.Tests/LanCopy.Tests.csproj
```

They cover path confinement (ShareRoot), real server↔client transfers,
upload/download validation and hash integrity.

## Supported Platforms

| Platform            | RID          | Download                                                                 | Notes                          |
|---------------------|--------------|--------------------------------------------------------------------------|--------------------------------|
| Windows x64         | win-x64      | [Portable EXE](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-win-x64.exe) | single-file app, no installation required |
| Windows ARM64       | win-arm64    | [Portable EXE](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-win-arm64.exe) | Surface Pro X, Snapdragon PCs (no installation required) |
| Linux x64           | linux-x64    | [tar.gz](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-x64.tar.gz) · [DEB](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-x64.deb) · [AppImage](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-x64.AppImage) | portable + installable options |
| Linux ARM64         | linux-arm64  | [tar.gz](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-arm64.tar.gz) · [DEB](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-arm64.deb) | Raspberry Pi 4/5, etc.         |
| macOS Apple Silicon | osx-arm64    | [ZIP](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-osx-arm64.zip) · [DMG](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-osx-arm64.dmg) | M1/M2/M3 Macs                  |
| macOS Intel         | osx-x64      | [ZIP](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-osx-x64.zip) · [DMG](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-osx-x64.dmg) | Intel Macs                     |

Windows installer (`.exe`, optional): [Releases page](https://github.com/carbarher/LanCopy/releases/latest)

## CLI & Local API (Preview)

```powershell
# CLI peer discovery
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- peers --wait 5 --json

# CLI send
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- send C:\tmp\file.zip --to 192.168.1.50:8742 --pin 1234

# CLI sync
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- sync C:\tmp\folder --to 192.168.1.50:8742 --remote-root backup

# Start local API on localhost (token persists in %LOCALAPPDATA%\LanCopy\cli-api.json)
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- api --port 3489

# Query peers (replace token shown on startup)
curl -H "X-LanCopy-Token: <token>" http://127.0.0.1:3489/api/v1/peers

# Start send transfer via API
curl -X POST http://127.0.0.1:3489/api/v1/transfers/send ^
  -H "Content-Type: application/json" ^
  -H "X-LanCopy-Token: <token>" ^
  -d "{\"localPath\":\"C:\\\\tmp\\\\file.zip\",\"to\":\"192.168.1.50:8742\",\"pin\":\"1234\"}"

# Start sync via API
curl -X POST http://127.0.0.1:3489/api/v1/sync ^
  -H "Content-Type: application/json" ^
  -H "X-LanCopy-Token: <token>" ^
  -d "{\"localDir\":\"C:\\\\data\",\"to\":\"192.168.1.50:8742\",\"remoteRoot\":\"backup\"}"

# Cancel transfer
curl -X POST -H "X-LanCopy-Token: <token>" http://127.0.0.1:3489/api/v1/transfers/<id>/cancel

# Retry a failed/canceled transfer
curl -X POST -H "X-LanCopy-Token: <token>" http://127.0.0.1:3489/api/v1/transfers/<id>/retry

# Same operations from CLI helper
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- transfer cancel <id> --token <token>
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- transfer retry <id> --token <token>

# OpenAPI document for integrations
curl http://127.0.0.1:3489/api/v1/openapi.json

# Stream live events (SSE)
curl -N -H "X-LanCopy-Token: <token>" http://127.0.0.1:3489/api/v1/events
```

### Integration Pack

- Postman collection: `scripts/api/LanCopy-Local-API.postman_collection.json`
- Curl script (Linux/macOS): `scripts/api/lancopy-api-curl.sh`
- Curl script (PowerShell/Windows): `scripts/api/lancopy-api-curl.ps1`
- Wiki pages (repo docs): `docs/wiki/`

Quick use:
```powershell
# PowerShell
$env:LANCOPY_API_TOKEN="<token>"
.\scripts\api\lancopy-api-curl.ps1

# Bash
export LANCOPY_API_TOKEN="<token>"
bash ./scripts/api/lancopy-api-curl.sh
```

### Integrator Quick Notes

- **EN**: Import Postman collection, set `baseUrl` + `token`, run requests.
- **FR**: Importez la collection Postman, configurez `baseUrl` + `token`, puis lancez.
- **DE**: Postman-Collection importieren, `baseUrl` + `token` setzen, Requests ausführen.

## Publish (Self-Contained Executable)

```powershell
# All platforms at once:
.\scripts\build-all.ps1

# Single platform:
dotnet publish LanCopy.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish/linux-x64
```

Result: a single binary with no runtime dependencies.
CI builds all 6 targets automatically on every `vX.Y.Z` tag.
See [PUBLISHING.md](PUBLISHING.md) for step-by-step release instructions.

### Build Windows Installer

```powershell
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" /DMyAppVersion=1.0.0 installer\LanCopy.iss
```

Generates `installer/Output/LanCopy-Setup-1.0.0.exe`, which installs the app and
optionally creates Windows Firewall rules for TCP 8742 and UDP discovery 8743 on private networks.

## Where to Find...

- Quick Start: `docs/wiki/Quick-Start-5-min.md`
- Troubleshooting: `docs/wiki/Troubleshooting.md`
- CLI/API cookbook: `docs/wiki/CLI-API-Cookbook.md`
- LAN hardening: `docs/wiki/LAN-Hardening.md`
- Compatibility matrix: `docs/wiki/Compatibility-Matrix.md`
- Core scope: `docs/CORE_SCOPE.md`
- Threat model: `docs/THREAT_MODEL.md`
- Protocol contract: `docs/PROTOCOL.md`
- Security posture matrix: `docs/SECURITY_POSTURE_MATRIX.md`
- Layering plan: `docs/architecture/LAYERING_PLAN.md`
- Sync job model: `docs/architecture/SYNC_JOB_MODEL.md`
- Session/request/version: `docs/architecture/SESSION_REQUEST_VERSION.md`
- Trust foundation: `docs/architecture/TRUST_FOUNDATION.md`
- Protocol v2 handshake: `docs/architecture/PROTOCOL_V2_HANDSHAKE.md`
- Feature deprecation: `docs/governance/FEATURE_DEPRECATION.md`
- Security event stream: `docs/observability/SECURITY_EVENT_STREAM.md`
- Updater verification: `docs/security/UPDATER_VERIFICATION.md`
- Release narrative: `docs/release/NARRATIVE_1_6_TO_2_0.md`
- Roadmap: `ROADMAP.md`

## Security

- **Path confinement**: by default the server only serves the shared folder
  (`%UserProfile%\LanCopy\Shared`); blocks path traversal and reparse points.
- **PIN**: constant-time comparison + per-IP rate limit with backoff.
- **TLS**: TOFU with peer certificate fingerprint pinning.
- **Anti-DoS**: line-length cap (1 MB), concurrent/per-IP connection limits,
  and an anti zip-bomb cap on decompression.

See [SECURITY.md](SECURITY.md) to report vulnerabilities. Privacy details: [PRIVACY.md](PRIVACY.md).
Additional security references: [docs/SECURITY_POSTURE_MATRIX.md](docs/SECURITY_POSTURE_MATRIX.md),
[docs/THREAT_MODEL.md](docs/THREAT_MODEL.md), [docs/PROTOCOL.md](docs/PROTOCOL.md).

## CI/CD

`.github/workflows/ci.yml`:
- On every push/PR: builds and runs tests on Windows, Linux and macOS.
- On every `vX.Y.Z` tag: publishes all 6 RIDs, builds the installer and creates
  a GitHub Release with the artifacts.

```powershell
git tag v1.0.0
git push origin v1.0.0
```

## Default Ports

TCP **8742** (file transfer), UDP **8743** (discovery).

## License

[MIT](LICENSE) © 2026 carbar. Free to use, modify and distribute.

