# Troubleshooting

## Common startup/update issues

### Windows update loops/failures (example: KB5066791)
1. Identify failing KB in update history.
2. Hide the KB so it is not retried.
3. Reset Windows Update components.
4. Run `DISM /Online /Cleanup-Image /RestoreHealth` and `sfc /scannow`.

### App starts but no peers found
- Ensure both devices are on same LAN/subnet.
- Check firewall allows TCP 8742 and UDP 8743.
- Disable VPN/proxy for local network tests.

### Transfer fails mid-file
- Try without compression for already-compressed files.
- Verify free disk space on receiver.
- Check antivirus quarantine/history.

## Quick diagnostics
- `dotnet test tests/LanCopy.Tests/LanCopy.Tests.csproj`
- `dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- peers --wait 5 --json`
- `curl http://127.0.0.1:3489/api/v1/health`
