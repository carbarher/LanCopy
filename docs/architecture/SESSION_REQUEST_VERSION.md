# Session / Request / Version Fields

Future protocol revisions should carry explicit fields for:

- session identity
- request identity
- protocol version

These fields allow capability gates, replay safety, and clearer diagnostics.
Unknown future fields must be ignored only when the safety model allows it.

