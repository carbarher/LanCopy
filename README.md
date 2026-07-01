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

### Supported platforms

| Platform            | RID          | Download                                                                 | Notes                          |
|---------------------|--------------|--------------------------------------------------------------------------|--------------------------------|
| Windows x64         | win-x64      | [ZIP](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-win-x64.zip) | .exe + installer               |
| Windows ARM64       | win-arm64    | [ZIP](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-win-arm64.zip) | Surface Pro X, Snapdragon PCs  |
| Linux x64           | linux-x64    | [tar.gz](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-x64.tar.gz) | .tar.gz with .desktop entry    |
| Linux ARM64         | linux-arm64  | [tar.gz](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-arm64.tar.gz) | Raspberry Pi 4/5, etc.         |
| macOS Apple Silicon | osx-arm64    | [ZIP](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-osx-arm64.zip) | M1/M2/M3 Macs                  |
| macOS Intel         | osx-x64      | [ZIP](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-osx-x64.zip) | Intel Macs                     |

Windows installer (`.exe`): [Releases page](https://github.com/carbarher/LanCopy/releases/latest)

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

### Plataformas soportadas

| Plataforma           | RID          | Descarga                                                                 | Notas                          |
|----------------------|--------------|--------------------------------------------------------------------------|--------------------------------|
| Windows x64          | win-x64      | [ZIP](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-win-x64.zip) | .exe + instalador              |
| Windows ARM64        | win-arm64    | [ZIP](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-win-arm64.zip) | Surface Pro X, PCs Snapdragon  |
| Linux x64            | linux-x64    | [tar.gz](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-x64.tar.gz) | .tar.gz con entrada .desktop   |
| Linux ARM64          | linux-arm64  | [tar.gz](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-linux-arm64.tar.gz) | Raspberry Pi 4/5, etc.         |
| macOS Apple Silicon  | osx-arm64    | [ZIP](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-osx-arm64.zip) | Macs M1/M2/M3                  |
| macOS Intel          | osx-x64      | [ZIP](https://github.com/carbarher/LanCopy/releases/latest/download/LanCopy-osx-x64.zip) | Macs Intel                     |

Instalador de Windows (`.exe`): [página de releases](https://github.com/carbarher/LanCopy/releases/latest)

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

