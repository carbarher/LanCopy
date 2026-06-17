# Contributing to LanCopy

Thanks for your interest in improving LanCopy! Contributions are welcome.

## Getting started
1. Fork and clone the repository.
2. Install the [.NET SDK 9.0+](https://dotnet.microsoft.com/download).
3. Build and run the tests:
   ```powershell
   dotnet build LanCopy.csproj -c Release
   dotnet test tests/LanCopy.Tests/LanCopy.Tests.csproj
   ```

## Pull requests
- Keep changes focused; one logical change per PR.
- Make sure `dotnet build` and `dotnet test` both pass before submitting.
- Match the existing code style (nullable enabled, async/await, no new external
  dependencies unless necessary).
- Add or update tests for behavior changes, especially anything touching the
  network protocol, path confinement, or file integrity.
- Describe what changed and why in the PR description.

## Reporting bugs
Open an issue with steps to reproduce, expected vs. actual behavior, your OS and
.NET version, and relevant log output (`%LocalAppData%\LanCopy\logs`).

## Security issues
Please do **not** open public issues for security vulnerabilities.
See [SECURITY.md](SECURITY.md).

## License
By contributing, you agree that your contributions are licensed under the
project''s [MIT License](LICENSE).
