# Quick Start (5 min)

## Windows
1. Download portable EXE from Releases.
2. Launch LanCopy on both devices.
3. Confirm both devices are on same LAN.
4. Pick peer from discovery list.
5. Send a test file.

## Linux
1. Download tar.gz, deb, or AppImage.
2. Start LanCopy on both devices.
3. Verify LAN reachability and firewall.
4. Select peer and transfer a sample file.

## macOS
1. Download zip or dmg build.
2. Launch LanCopy on both devices.
3. Allow local network access if prompted.
4. Select peer and send a sample file.

## API quick start
```powershell
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- api --port 3489
curl http://127.0.0.1:3489/api/v1/health
```
