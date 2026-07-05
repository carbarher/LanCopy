# Known Limitations

LanCopy is designed for nearby computers on a trusted local network. It is not a cloud sync service and it is not meant to expose files over the public internet.

## Network

- Both computers must be on the same local network.
- Automatic discovery can fail on public Wi-Fi, guest Wi-Fi, client-isolated networks or strict corporate networks.
- Cable works when both computers are on a compatible Ethernet network, usually through a router or switch.
- Some firewalls block the first connection until LanCopy is allowed on private networks.
- Default ports are TCP `8742` for transfer/chat and UDP `8743` for discovery.
- Manual IP connection is available in Advanced when discovery is blocked.

## Security and Permissions

- Protected mode blocks or confirms risky actions, but it does not replace normal backups.
- Sensitive system folders are protected; LanCopy is not intended for managing operating system directories.
- Remote delete, rename and power actions should only be used with trusted computers.
- Device trust is local to each computer. If a device identity changes, re-pair only when you trust that device.

## Transfers

- Very large transfers depend on disk speed, network stability and available space.
- Resuming helps after interruptions, but changing or moving files during a transfer can require retrying.
- Folder sync is a transfer helper, not a full versioned backup system.

## CLI and API

- The CLI/API are automation surfaces. The desktop app is the recommended flow for normal users.
- The local API binds to `127.0.0.1` and requires `X-LanCopy-Token` for protected endpoints.
- Do not expose the local API to the LAN or internet without adding your own network controls.

## Platforms

- Windows has the most complete installer flow.
- Linux and macOS packages are provided as portable/installable artifacts, but desktop integration can vary by distribution/version.
- Code signing and OS trust prompts depend on release signing/notarization configuration.