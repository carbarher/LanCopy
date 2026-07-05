# Feature Deprecation Policy

LanCopy deprecates features in stages:

1. Keep the feature working while a replacement or migration path exists.
2. Mark the feature as legacy in docs and UI.
3. Emit warnings and keep a clear migration path.
4. Remove only after the supported replacement has shipped and stabilized.

## Rules

- Do not remove a feature silently.
- Do not break existing transfer flows without a migration note.
- Prefer config warnings before hard removal.

