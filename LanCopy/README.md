# LanCopy

Transferencia de archivos por LAN, cifrada (TLS + TOFU), multilingüe (20 idiomas) y multiplataforma.
Desarrollada en C# / .NET 9 con Avalonia UI.

## Requisitos de desarrollo

- .NET SDK 9.0 o superior
- (Opcional, para el instalador) [Inno Setup 6](https://jrsoftware.org/isdl.php)

## Compilar y ejecutar

```powershell
dotnet build LanCopy.csproj -c Release
dotnet run --project LanCopy.csproj
```

## Tests

```powershell
dotnet test tests/LanCopy.Tests/LanCopy.Tests.csproj
```

Cubren: confinamiento de rutas (ShareRoot), transferencias reales servidor↔cliente,
validación de subida/descarga e integridad por hash.

## Publicar (ejecutable único, autocontenido)

```powershell
dotnet publish LanCopy.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/win-x64
```

El resultado es `publish/win-x64/LanCopy.exe`, sin dependencias del runtime.

## Crear el instalador (Windows)

```powershell
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" /DMyAppVersion=1.0.0 installer\LanCopy.iss
```

Genera `installer/Output/LanCopy-Setup-1.0.0.exe`, que instala la app y, opcionalmente,
crea la regla de firewall para el puerto TCP 8742.

## CI/CD

`.github/workflows/ci.yml`:
- En cada push/PR: compila y ejecuta los tests.
- En cada tag `vX.Y.Z`: publica el ejecutable, construye el instalador y crea un GitHub Release con los artefactos.

```powershell
git tag v1.0.0
git push origin v1.0.0
```

## Seguridad

- **Confinamiento de rutas**: por defecto el servidor solo sirve la carpeta compartida
  (`%UserProfile%\LanCopy\Shared`); bloquea path traversal y enlaces/reparse points.
- **PIN**: comparación en tiempo constante + rate-limit por IP con backoff.
- **TLS**: TOFU (Trust On First Use) con fijación de huella del certificado del peer.
- **Anti-DoS**: límite de longitud de línea (1 MB), límite de conexiones concurrentes
  y por IP, y cap anti zip-bomb en la descompresión.