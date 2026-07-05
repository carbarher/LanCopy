# Sync Job Model

Sync should be represented as an explicit job contract:

- source and destination paths
- mode (upload, download, mirror)
- trust requirements
- retry and resume policy
- expected capability checks

The UI and engine should refer to the same job shape so sync behavior is
predictable and testable.

