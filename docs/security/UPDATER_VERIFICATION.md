# Updater Verification Policy

Current status:

- release assets are accompanied by `.sha256` manifests
- downloaded assets must match a full 64-character SHA-256 digest before apply
- failed or uncertain checksum verification fails closed
- failed apply attempts must restore the previous executable when possible

Security boundary:

- SHA-256 manifests provide integrity against accidental corruption
- SHA-256 manifests downloaded from the same release do not prove publisher identity
- automatic install uses the pinned release signing public key for signed JSON manifests

Signed manifest support:

- legacy `.sha256` files are parsed, but automatic apply fails closed when the pinned key requires a signature
- JSON manifests may include `sha256` and `signature` fields
- signatures cover `assetName + newline + sha256 + newline` using ECDSA P-256/SHA-256
- `ReleaseManifestPublicKeyPem` is configured, so missing, malformed, mismatched, or untrusted signatures fail closed

Required before enabling a fully trusted auto-update path:

- sign release manifests with an offline-protected signing key
- publish signed JSON manifests beside every release asset
- provide a download-only fallback instead of silent install when verification fails

No update path should weaken the trust model or bypass the existing safety defaults.
Release automation:

- `scripts/sign-release-manifests.ps1` generates legacy or signed `.sha256` sidecars
- `.github/workflows/lancopy-release.yml` publishes release assets and sidecars
- `LANCOPY_RELEASE_SIGNING_PRIVATE_KEY_PEM` enables signed JSON manifests in CI
- keep the private key outside the repository and rotate it if exposed
