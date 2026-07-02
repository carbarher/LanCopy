# Compatibility Matrix

| Platform | Artifact | Install required | Notes |
|---|---|---:|---|
| Windows x64 | `LanCopy-win-x64.exe` | No | Portable single-file EXE |
| Windows ARM64 | `LanCopy-win-arm64.exe` | No | Portable single-file EXE |
| Windows x64 | `LanCopy-Setup-*.exe` | Yes (optional) | Installer flow and shortcuts |
| Linux x64 | `.tar.gz` | No | Portable archive |
| Linux x64 | `.deb` | Yes | Native package install |
| Linux x64 | `.AppImage` | No | Portable executable image |
| Linux ARM64 | `.tar.gz` | No | Portable archive |
| Linux ARM64 | `.deb` | Yes | Native package install |
| macOS ARM64 | `.zip` | No | Portable app bundle |
| macOS ARM64 | `.dmg` | Optional | Guided install/mount flow |
| macOS x64 | `.zip` | No | Portable app bundle |
| macOS x64 | `.dmg` | Optional | Guided install/mount flow |

## Interop notes
- LAN transfers are cross-platform as long as peers can reach TCP 8742.
- Discovery depends on UDP broadcast visibility in the local segment.
- For segmented networks, connect by explicit IP/port.
