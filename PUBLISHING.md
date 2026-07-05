# Guia de publicacion de LanCopy

## 1. Release desde el monorepo

El workflow `.github/workflows/lancopy-release.yml` publica LanCopy cuando se crea un tag:

```powershell
git tag lancopy-v1.2.3
git push origin lancopy-v1.2.3
```

Tambien puede lanzarse manualmente desde GitHub Actions con el input `version`.

## 2. Artefactos generados

| Plataforma          | RID          | Artefacto                    |
|---------------------|--------------|------------------------------|
| Windows x64         | win-x64      | `LanCopy-win-x64.exe`        |
| Windows ARM64       | win-arm64    | `LanCopy-win-arm64.exe`      |
| Linux x64           | linux-x64    | `LanCopy-linux-x64.tar.gz`   |
| Linux ARM64         | linux-arm64  | `LanCopy-linux-arm64.tar.gz` |
| macOS Apple Silicon | osx-arm64    | `LanCopy-osx-arm64.zip`      |
| macOS Intel         | osx-x64      | `LanCopy-osx-x64.zip`        |

Cada artefacto recibe un sidecar `<asset>.sha256`.

## 3. Manifests firmados para el updater

Por defecto, los `.sha256` contienen solo el hash para compatibilidad.

Para activar manifests JSON firmados:

1. Genera una clave ECDSA P-256 offline:

```powershell
cd C:\p2p\LanCopy
.\scripts\new-release-signing-key.ps1 -OutDir C:\secure\lancopy-signing
```

2. Guarda `lancopy-release-private.pem` como secret de GitHub:

`LANCOPY_RELEASE_SIGNING_PRIVATE_KEY_PEM`

3. La clave publica actual ya esta pineada en `UpdateChecker.ReleaseManifestPublicKeyPem`. Si rotas la clave, actualiza ese valor en la app.

El payload firmado es:

```text
assetName + newline + sha256 + newline
```

Si el secret no existe, el workflow genera manifests legacy de solo hash.

## 4. Build local multiplataforma

```powershell
cd C:\p2p\LanCopy
.\scripts\build-all.ps1                  # todas las plataformas
.\scripts\build-all.ps1 -Rid linux-x64  # solo una
.\scripts\build-all.ps1 -Version 1.1.0  # con version especifica
```

## 5. Generar manifests localmente

```powershell
cd C:\p2p\LanCopy
.\scripts\sign-release-manifests.ps1 -AssetsDir .\dist
.\scripts\sign-release-manifests.ps1 -AssetsDir .\dist -PrivateKeyPemPath C:\secure\lancopy-signing\lancopy-release-private.pem
```

## 6. Firma de codigo en Windows y macOS

Los manifests firmados protegen la integridad/autoria que valida el updater. No sustituyen la firma de codigo del sistema operativo.

Para Windows, usa un certificado Authenticode. Para macOS, usa Developer ID + notarizacion.
