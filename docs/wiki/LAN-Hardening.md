# LAN Hardening

## Network and firewall baseline
- Open TCP 8742 and UDP 8743 only on trusted LAN profiles.
- Block inbound access from public/untrusted network profiles.
- Keep endpoint firewall enabled on both sender and receiver.

## Segmentation and trust
- Prefer dedicated trusted VLAN for transfer-heavy devices.
- Avoid cross-segment routing unless required and controlled.
- Use optional PIN when LAN trust is partial.

## VPN/proxy considerations
- Disable full-tunnel VPN when peer discovery is needed.
- If VPN is required, use explicit IP/port instead of discovery.
- Avoid proxy interception for LAN direct transfer traffic.

## Operational guidance
- Keep TLS enabled by default.
- Restrict share root where possible.
- Use read-only mode on receiver devices when practical.
