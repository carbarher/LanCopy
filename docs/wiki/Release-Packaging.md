# Release & Packaging

## CI flow
- Pull request: LanCopy CI builds and tests on Windows/Linux/macOS.
- Tag `lancopy-vX.Y.Z`: publish LanCopy release artifacts.

## Published artifacts
- Windows: `LanCopy-win-x64.exe`, `LanCopy-win-arm64.exe`.
- Linux: `LanCopy-linux-x64.tar.gz`, `LanCopy-linux-arm64.tar.gz`.
- macOS: `LanCopy-osx-x64.zip`, `LanCopy-osx-arm64.zip`.

## Update manifests
- Every release asset gets `<asset>.sha256`.
- Without a signing secret, the sidecar is a legacy plain SHA-256 hash.
- With `LANCOPY_RELEASE_SIGNING_PRIVATE_KEY_PEM`, the sidecar is JSON with `sha256` and `signature`.
- Signatures cover `assetName + newline + sha256 + newline` with ECDSA P-256/SHA-256.

## Validation checklist

Use the full [Release Checklist](Release-Checklist) before promoting a release.

Minimum gate:

1. LanCopy CI green on Windows/Linux/macOS.
2. Tag workflow completed.
3. Release exists and contains expected assets.
4. Every artifact has a `.sha256` sidecar.
5. Signed-update mode uses the public key pinned in the app.
6. Download links in README resolve correctly.
7. Windows installer passes [Windows Installer Validation](Windows-Installer-Validation).
8. Static OpenAPI copy `docs/api/openapi.json` is regenerated if API endpoints changed.
