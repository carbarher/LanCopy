# Release & Packaging

## CI flow
- Push/PR: build + tests on Windows/Linux/macOS.
- Tag `vX.Y.Z`: publish artifacts and create GitHub release.

## Published artifacts
- Windows: portable EXE (x64/arm64) + optional installer.
- Linux: tar.gz + deb (+ AppImage on x64).
- macOS: zip + dmg (arm64/x64).

## Version automation
- Auto Version Tag workflow bumps csproj version.
- CI workflow builds and publishes release assets.

## Validation checklist
1. CI green on master.
2. Tag workflow completed.
3. Release exists and contains expected assets.
4. Download links in README resolve correctly.
