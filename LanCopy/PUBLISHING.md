# Guia de publicacion de LanCopy

## 1. Extraer a repositorio publico limpio

```powershell
cd C:\p2p\LanCopy
.\scripts\extract-repo.ps1 -Dest C:\lancopy-public
cd C:\lancopy-public
git init
git add .
git commit -m "Initial release v1.0.0"
gh repo create carbarher/LanCopy --public --source=. --remote=origin --push
```

## 2. Crear la primera release con tag

```powershell
cd C:\lancopy-public
git tag v1.0.0
git push origin v1.0.0
```

El tag activa el pipeline CI que construye y publica automaticamente los binarios
para las 6 plataformas (ver `.github/workflows/ci.yml`).

## 3. Plataformas y artefactos generados

| Plataforma         | RID          | Artefacto                          |
|--------------------|--------------|------------------------------------|
| Windows x64        | win-x64      | `LanCopy-win-x64.zip` + instalador |
| Windows ARM64      | win-arm64    | `LanCopy-win-arm64.zip`            |
| Linux x64          | linux-x64    | `LanCopy-linux-x64.tar.gz`        |
| Linux ARM64        | linux-arm64  | `LanCopy-linux-arm64.tar.gz`      |
| macOS Apple Silicon| osx-arm64    | `LanCopy-osx-arm64.zip` (.app)    |
| macOS Intel        | osx-x64      | `LanCopy-osx-x64.zip` (.app)      |

## 4. Build local multiplataforma

```powershell
cd C:\p2p\LanCopy
.\scripts\build-all.ps1                  # todas las plataformas
.\scripts\build-all.ps1 -Rid linux-x64  # solo una
.\scripts\build-all.ps1 -Version 1.1.0  # con version especifica
```

## 5. Firma de codigo en Windows (opcional)

El CI tiene el bloque de firma de codigo preparado pero comentado.
Para habilitarlo define los secretos del repositorio en GitHub:
- `WINDOWS_CERT_BASE64` → el .pfx codificado en base64
- `WINDOWS_CERT_PASSWORD` → la contrasena del .pfx

Y descomenta el paso "Sign executable" en `.github/workflows/ci.yml`.

## 6. Firma de codigo en macOS (notarizacion, opcional)

El .app generado no esta firmado ni notarizado. Para distribuirlo sin que macOS
muestre advertencias de seguridad necesitaras una cuenta de Apple Developer y
seguir el proceso de codesign + notarytool. Ver la documentacion de Apple.

## 7. Nota sobre el icono en macOS

El script `packaging/macos/make-app.sh` genera automaticamente el icono .icns
a partir de `Assets/app.png` si `sips` e `iconutil` estan disponibles (macOS).
En el runner de GitHub Actions (macos-latest) estan disponibles por defecto.