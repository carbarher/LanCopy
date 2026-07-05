# CLI Guide

The CLI is useful for scripts, scheduled jobs and headless transfers. For normal desktop use, run the LanCopy app instead.

Examples below assume you run commands from the repository root while developing:

```powershell
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- <command>
```

If you publish or package the CLI, replace that prefix with your CLI executable.

## Help

```powershell
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- --help
```

## Discover nearby computers

```powershell
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- peers --wait 5
```

JSON output:

```powershell
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- peers --wait 5 --json
```

Options:

| Option | Meaning |
|--------|---------|
| `--wait <seconds>` | Discovery time, 1 to 30 seconds. Default: 3. |
| `--json` | Print machine-readable JSON. |

## Send one file

```powershell
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- send C:\tmp\file.zip --to 192.168.1.50:8742
```

With a destination path and PIN:

```powershell
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- send C:\tmp\file.zip --to 192.168.1.50:8742 --remote uploads/file.zip --pin 1234
```

Options:

| Option | Meaning |
|--------|---------|
| `--to <ip[:port]>` | Required remote computer. Port defaults to `8742`. |
| `--remote <path>` | Remote destination path. Defaults to the source file name. |
| `--pin <pin>` | Optional PIN if the remote app requires one. |
| `--json` | Print final status as JSON. |
| `--no-tls` | Advanced compatibility option. Normal use should not need it. |
| `--no-compress` | Disable compression. |
| `--allow-plaintext-fallback` | Advanced compatibility fallback for older/explicitly compatible peers. |

Exit codes:

| Code | Meaning |
|------|---------|
| `0` | Success. |
| `1` | Transfer failed or was cancelled. |
| `2` | Invalid arguments. |
| `3` | Source path not found. |

## Sync a folder to another computer

```powershell
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- sync C:\data --to 192.168.1.50:8742 --remote-root backup
```

Options:

| Option | Meaning |
|--------|---------|
| `--to <ip[:port]>` | Required remote computer. Port defaults to `8742`. |
| `--remote-root <path>` | Remote folder prefix. |
| `--pin <pin>` | Optional PIN. |
| `--json` | Print final status as JSON. |
| `--no-tls` | Advanced compatibility option. |
| `--no-compress` | Disable compression. |
| `--allow-plaintext-fallback` | Advanced compatibility fallback. |

Symlinks and junctions are skipped by default, matching the desktop app safety policy.

## Start the local API

```powershell
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- api --port 3489
```

The API listens only on `127.0.0.1`. It prints the token at startup and stores a generated token for reuse.

Options:

| Option | Meaning |
|--------|---------|
| `--port <port>` | API port. Default: `3489`. |
| `--token <token>` | Use a specific API token. |
| `--reset-token` | Generate and persist a new token. |

## Manage API transfers from CLI

Set the token once in your shell:

```powershell
$env:LANCOPY_API_TOKEN = "<token>"
```

Check status:

```powershell
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- transfer status <id>
```

Cancel:

```powershell
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- transfer cancel <id>
```

Retry:

```powershell
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- transfer retry <id>
```

Options:

| Option | Meaning |
|--------|---------|
| `--api-url <url>` | API base URL. Default: `http://127.0.0.1:3489`. |
| `--token <token>` | API token. If omitted, `LANCOPY_API_TOKEN` is used. |
| `--json` | Print raw JSON. |
