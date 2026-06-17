# Privacy

LanCopy works without accounts and without cloud by default.

## Optional telemetry (opt-in)

LanCopy can send **anonymous** technical events only if the user explicitly accepts.

Collected events:
- `app_started` (app version, OS, advanced-mode flag)
- `transfer_completed` (direction, file count, total bytes)

Never collected:
- File names
- File paths
- IP addresses
- Clipboard content
- PIN values
- Personal identifiers

## How telemetry is configured

- Disabled by default until user consent.
- Can be toggled later in **Advanced** settings.
- Endpoint is read from `settings.json` (`telemetryEndpoint`) or env var `LANCOPY_TELEMETRY_ENDPOINT`.

## Data retention and control

Telemetry is best-effort and non-blocking. If endpoint is not configured, nothing is sent.
