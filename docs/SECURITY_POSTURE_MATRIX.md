# Security Posture Matrix

| Area | Default |
| --- | --- |
| TLS | On |
| Plaintext fallback | Off |
| Shared-folder confinement | On |
| Safe Mode | On |
| Remote delete | Off |
| Remote power | Off |
| Auto clipboard sync | Off |
| Auto link open | Off |
| Unknown peer access | Minimal status only |
| Browse/download permissions | On |
| Write/sync/clipboard/power permissions | Off |

## Recommended profile

- keep Safe Mode enabled
- keep TLS enabled
- only grant browse/download by default
- use trusted-device presets for anything else

## Dangerous features

- remote delete
- remote power
- full-disk browsing
- advanced clipboard automation
- plaintext fallback
