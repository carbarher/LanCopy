# Windows Installer Validation

This checklist validates install/uninstall behavior without guessing from the build alone.

## Static Validation

The installer script `installer/LanCopy.iss` should include:

- application installed under `{autopf}\LanCopy`
- Start Menu shortcut
- optional desktop shortcut
- optional Windows Firewall rules for private networks:
  - `LanCopy TCP 8742`
  - `LanCopy UDP Discovery 8743`
- uninstall cleanup for those firewall rules
- post-install launch option

## Manual Install Test

Use a clean Windows VM or test PC.

1. Download the latest `LanCopy-Setup-*.exe` from GitHub Releases.
2. Run the installer as a normal user and accept elevation.
3. Install with desktop icon disabled.
4. Confirm Start Menu shortcut exists.
5. Launch LanCopy from Start Menu.
6. Close LanCopy and confirm no background `LanCopy.exe` remains.
7. Reinstall with desktop icon enabled.
8. Confirm desktop shortcut exists and launches the app.
9. Install with firewall task enabled.
10. Confirm private firewall rules exist:
    - `LanCopy TCP 8742`
    - `LanCopy UDP Discovery 8743`
11. Connect to a second PC and perform a small transfer.
12. Open chat and send a test message.

## Manual Uninstall Test

1. Uninstall LanCopy from Windows Settings or Control Panel.
2. Confirm installation folder is removed.
3. Confirm Start Menu shortcut is removed.
4. Confirm desktop shortcut is removed when it was created by installer.
5. Confirm firewall rules are removed.
6. Confirm no `LanCopy.exe` process remains.
7. Confirm user data is not unexpectedly deleted from `%LOCALAPPDATA%\LanCopy` unless a future explicit cleanup option is added.

## Release Gate

A release should not be promoted as installer-ready until the install and uninstall checks pass on at least one clean Windows x64 machine.