# LanCopy

> Encrypted, cross-platform LAN file transfer — fast, private, no cloud.
> Transferencia de archivos por LAN, cifrada y multiplataforma — rápida, privada, sin nube.

[![CI](https://github.com/carbarher/LanCopy/actions/workflows/ci.yml/badge.svg)](https://github.com/carbarher/LanCopy/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4.svg)](https://dotnet.microsoft.com/)

LanCopy moves files directly between devices on your local network over an
encrypted channel. No accounts, no servers, no internet round-trips. Built with
C# / .NET 9 and Avalonia UI. Available in 20 languages.

---

## English

### Features
- **Direct LAN transfers** — peer-to-peer, no cloud or third-party servers.
- **Encrypted** — TLS with Trust-On-First-Use (TOFU) certificate pinning.
- **Auto-discovery** — peers on the same network appear automatically (UDP).
- **Easy pairing** — short voice-friendly codes, QR codes, or `lancopy://` links.
- **Integrity** — every transfer is verified with a streaming SHA-256 hash.
- **Resumable downloads** — interrupted transfers continue from where they stopped.
- **Optional PIN** — constant-time check with per-IP rate limiting and backoff.
- **Safe by default** — transfers are confined to a shared folder; path traversal,
  symlink/junction escapes, zip-bombs and oversized headers are blocked.
- **Folder sync, watch mode, clipboard text send, bandwidth limit.**
- **20 languages**, including RTL (Arabic).
- **Optional anonymous telemetry (opt-in)** for adoption metrics only.

### Requirements
- .NET SDK 9.0 or later
- (Optional, for the Windows installer) [Inno Setup 6](https://jrsoftware.org/isdl.php)

### Build & run
```powershell
dotnet build LanCopy.csproj -c Release
dotnet run --project LanCopy.csproj
```

### Tests
```powershell
dotnet test tests/LanCopy.Tests/LanCopy.Tests.csproj
```
They cover path confinement (ShareRoot), real server↔client transfers,
upload/download validation and hash integrity.

### CLI & local API (preview)
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

### Supported platforms

| Platform            | RID          | Download                                                                 | Notes                          |
|---------------------|--------------|--------------------------------------------------------------------------|--------------------------------|
| Windows x64         | win-x64      | [Portable EXE](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-win-x64.exe) | single-file app, no installation required |
| Windows ARM64       | win-arm64    | [Portable EXE](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-win-arm64.exe) | Surface Pro X, Snapdragon PCs (no installation required) |
| Linux x64           | linux-x64    | [tar.gz](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-x64.tar.gz) · [DEB](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-x64.deb) · [AppImage](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-x64.AppImage) | portable + installable options |
| Linux ARM64         | linux-arm64  | [tar.gz](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-arm64.tar.gz) · [DEB](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-arm64.deb) | Raspberry Pi 4/5, etc.         |
| macOS Apple Silicon | osx-arm64    | [ZIP](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-osx-arm64.zip) · [DMG](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-osx-arm64.dmg) | M1/M2/M3 Macs                  |
| macOS Intel         | osx-x64      | [ZIP](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-osx-x64.zip) · [DMG](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-osx-x64.dmg) | Intel Macs                     |

Windows installer (`.exe`, optional): [Releases page](https://github.com/carbarher/LanCopy/releases/latest)

### Publish (single self-contained executable)
```powershell
# All platforms at once:
.\scripts\build-all.ps1

# Single platform:
dotnet publish LanCopy.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish/linux-x64
```
Result: a single binary with no runtime dependencies.
CI builds all 6 targets automatically on every `vX.Y.Z` tag.
See [PUBLISHING.md](PUBLISHING.md) for step-by-step release instructions.

### Security
- **Path confinement**: by default the server only serves the shared folder
  (`%UserProfile%\LanCopy\Shared`); blocks path traversal and reparse points.
- **PIN**: constant-time comparison + per-IP rate limit with backoff.
- **TLS**: TOFU with peer certificate fingerprint pinning.
- **Anti-DoS**: line-length cap (1 MB), concurrent/per-IP connection limits,
  and an anti zip-bomb cap on decompression.

See [SECURITY.md](SECURITY.md) to report vulnerabilities. Privacy details: [PRIVACY.md](PRIVACY.md).

### Default port
TCP **8742** (file transfer), UDP **8743** (discovery).

### License
[MIT](LICENSE) © 2026 carbar. Free to use, modify and distribute.

---

## Español

### Características
- **Transferencias directas por LAN** — entre equipos, sin nube ni terceros.
- **Cifrado** — TLS con fijación de certificado TOFU (confianza al primer uso).
- **Autodescubrimiento** — los equipos de la red aparecen solos (UDP).
- **Emparejamiento fácil** — códigos cortos fáciles de dictar, QR o enlaces `lancopy://`.
- **Integridad** — cada transferencia se verifica con SHA-256 en streaming.
- **Descargas reanudables** — continúan donde se cortaron.
- **PIN opcional** — comparación en tiempo constante con rate-limit por IP.
- **Seguro por defecto** — confina a una carpeta compartida; bloquea path traversal,
  enlaces/junctions, zip-bombs y cabeceras gigantes.
- **Sincronización de carpetas, modo vigilancia, envío de texto, límite de ancho de banda.**
- **20 idiomas**, incluido RTL (árabe).

### Requisitos de desarrollo
- .NET SDK 9.0 o superior
- (Opcional, para el instalador) [Inno Setup 6](https://jrsoftware.org/isdl.php)

### Compilar y ejecutar
```powershell
dotnet build LanCopy.csproj -c Release
dotnet run --project LanCopy.csproj
```

### Tests
```powershell
dotnet test tests/LanCopy.Tests/LanCopy.Tests.csproj
```

### CLI y API local (preview)
```powershell
# Descubrimiento de peers por CLI
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- peers --wait 5 --json

# Envío por CLI
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- send C:\tmp\file.zip --to 192.168.1.50:8742 --pin 1234

# Sincronización por CLI
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- sync C:\tmp\folder --to 192.168.1.50:8742 --remote-root backup

# Arrancar API local en localhost (el token se persiste en %LOCALAPPDATA%\LanCopy\cli-api.json)
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- api --port 3489

# Consultar peers (usa el token mostrado al arrancar)
curl -H "X-LanCopy-Token: <token>" http://127.0.0.1:3489/api/v1/peers

# Lanzar envío por API
curl -X POST http://127.0.0.1:3489/api/v1/transfers/send ^
  -H "Content-Type: application/json" ^
  -H "X-LanCopy-Token: <token>" ^
  -d "{\"localPath\":\"C:\\\\tmp\\\\file.zip\",\"to\":\"192.168.1.50:8742\",\"pin\":\"1234\"}"

# Lanzar sincronización por API
curl -X POST http://127.0.0.1:3489/api/v1/sync ^
  -H "Content-Type: application/json" ^
  -H "X-LanCopy-Token: <token>" ^
  -d "{\"localDir\":\"C:\\\\data\",\"to\":\"192.168.1.50:8742\",\"remoteRoot\":\"backup\"}"

# Cancelar transferencia
curl -X POST -H "X-LanCopy-Token: <token>" http://127.0.0.1:3489/api/v1/transfers/<id>/cancel

# Reintentar transferencia fallida/cancelada
curl -X POST -H "X-LanCopy-Token: <token>" http://127.0.0.1:3489/api/v1/transfers/<id>/retry

# Las mismas operaciones desde CLI helper
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- transfer cancel <id> --token <token>
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- transfer retry <id> --token <token>

# Documento OpenAPI para integraciones
curl http://127.0.0.1:3489/api/v1/openapi.json

# Ver eventos en tiempo real (SSE)
curl -N -H "X-LanCopy-Token: <token>" http://127.0.0.1:3489/api/v1/events
```

### Plataformas soportadas

| Plataforma           | RID          | Descarga                                                                 | Notas                          |
|----------------------|--------------|--------------------------------------------------------------------------|--------------------------------|
| Windows x64          | win-x64      | [EXE portable](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-win-x64.exe) | aplicación de archivo único, sin instalación |
| Windows ARM64        | win-arm64    | [EXE portable](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-win-arm64.exe) | Surface Pro X, PCs Snapdragon (sin instalación) |
| Linux x64            | linux-x64    | [tar.gz](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-x64.tar.gz) · [DEB](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-x64.deb) · [AppImage](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-x64.AppImage) | opciones portable e instalable |
| Linux ARM64          | linux-arm64  | [tar.gz](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-arm64.tar.gz) · [DEB](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-arm64.deb) | Raspberry Pi 4/5, etc.         |
| macOS Apple Silicon  | osx-arm64    | [ZIP](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-osx-arm64.zip) · [DMG](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-osx-arm64.dmg) | Macs M1/M2/M3                  |
| macOS Intel          | osx-x64      | [ZIP](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-osx-x64.zip) · [DMG](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-osx-x64.dmg) | Macs Intel                     |

Instalador de Windows (`.exe`, opcional): [página de releases](https://github.com/carbarher/LanCopy/releases/latest)

### Publicar (ejecutable único autocontenido)
```powershell
# Todas las plataformas de una vez:
.\scripts\build-all.ps1

# Solo una plataforma:
dotnet publish LanCopy.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish/linux-x64
```
Resultado: un único binario sin dependencias de runtime.
El CI construye los 6 destinos automáticamente con cada tag `vX.Y.Z`.
Consulta [PUBLISHING.md](PUBLISHING.md) para instrucciones paso a paso.

### Crear el instalador (Windows)
```powershell
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" /DMyAppVersion=1.0.0 installer\LanCopy.iss
```
Genera `installer/Output/LanCopy-Setup-1.0.0.exe`, que instala la app y,
opcionalmente, crea la regla de firewall para el puerto TCP 8742.

### CI/CD
`.github/workflows/ci.yml`:
- En cada push/PR: compila y ejecuta los tests en Windows, Linux y macOS.
- En cada tag `vX.Y.Z`: publica los 6 RIDs, construye el instalador y crea
  un GitHub Release con los artefactos.

```powershell
git tag v1.0.0
git push origin v1.0.0
```

### Licencia
[MIT](LICENSE) © 2026 carbar. Libre para usar, modificar y distribuir.





