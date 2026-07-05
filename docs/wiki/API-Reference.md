# API Reference

LanCopy includes a local API for automation and integrations. It is started by the CLI and listens only on localhost.

## Start the API

```powershell
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- api --port 3489
```

The API prints a token. Send that token in every protected request:

```http
X-LanCopy-Token: <token>
```

Unauthenticated endpoints:

- `GET /api/v1/health`
- `GET /api/v1/openapi.json`

All other `/api/v1` endpoints require `X-LanCopy-Token`.

## OpenAPI

```powershell
curl http://127.0.0.1:3489/api/v1/openapi.json
```

Postman collection:

```text
scripts/api/LanCopy-Local-API.postman_collection.json
```

Example scripts:

```text
scripts/api/lancopy-api-curl.ps1
scripts/api/lancopy-api-curl.sh
```

## Health

```http
GET /api/v1/health
```

Example:

```powershell
curl http://127.0.0.1:3489/api/v1/health
```

Response includes API status, server time and version.

## Peers

```http
GET /api/v1/peers
```

Example:

```powershell
curl -H "X-LanCopy-Token: <token>" http://127.0.0.1:3489/api/v1/peers
```

Response item:

```json
{
  "name": "PC-NAME",
  "ip": "192.168.1.50",
  "port": 8742,
  "lastSeenUtc": "2026-07-05T17:00:00Z"
}
```

## Send File

```http
POST /api/v1/transfers/send
```

Request:

```json
{
  "localPath": "C:\\tmp\\file.zip",
  "to": "192.168.1.50:8742",
  "remotePath": "uploads/file.zip",
  "pin": "1234",
  "useTls": true,
  "useCompress": true
}
```

Required fields:

- `localPath`
- `to`

Optional fields:

- `remotePath`
- `pin`
- `useTls`
- `useCompress`

PowerShell curl example:

```powershell
curl -X POST http://127.0.0.1:3489/api/v1/transfers/send ^
  -H "Content-Type: application/json" ^
  -H "X-LanCopy-Token: <token>" ^
  -d "{\"localPath\":\"C:\\\\tmp\\\\file.zip\",\"to\":\"192.168.1.50:8742\"}"
```

Accepted response:

```json
{
  "id": "transfer-id",
  "statusUrl": "/api/v1/transfers/transfer-id"
}
```

## Sync Folder

```http
POST /api/v1/sync
```

Request:

```json
{
  "localDir": "C:\\data",
  "to": "192.168.1.50:8742",
  "remoteRoot": "backup",
  "pin": "1234",
  "useTls": true,
  "useCompress": true
}
```

Required fields:

- `localDir`
- `to`

Optional fields:

- `remoteRoot`
- `pin`
- `useTls`
- `useCompress`

## Transfer Status

```http
GET /api/v1/transfers/{id}
```

Example:

```powershell
curl -H "X-LanCopy-Token: <token>" http://127.0.0.1:3489/api/v1/transfers/<id>
```

Response:

```json
{
  "id": "transfer-id",
  "kind": "send",
  "state": "completed",
  "localPath": "C:\\tmp\\file.zip",
  "remotePath": "file.zip",
  "to": "192.168.1.50:8742",
  "useTls": true,
  "useCompress": true,
  "cancellationRequested": false,
  "doneBytes": 100,
  "totalBytes": 100,
  "doneFiles": 1,
  "totalFiles": 1,
  "startedUtc": "2026-07-05T17:00:00Z",
  "finishedUtc": "2026-07-05T17:00:03Z",
  "error": null
}
```

States include:

- `queued`
- `running`
- `completed`
- `failed`
- `canceled`

## Cancel Transfer

```http
POST /api/v1/transfers/{id}/cancel
```

Example:

```powershell
curl -X POST -H "X-LanCopy-Token: <token>" http://127.0.0.1:3489/api/v1/transfers/<id>/cancel
```

Typical responses:

- `202 Accepted`: cancellation requested
- `404 Not Found`: unknown transfer id
- `409 Conflict`: transfer already finished

## Retry Transfer

```http
POST /api/v1/transfers/{id}/retry
```

Example:

```powershell
curl -X POST -H "X-LanCopy-Token: <token>" http://127.0.0.1:3489/api/v1/transfers/<id>/retry
```

Typical responses:

- `202 Accepted`: retry enqueued; response includes `retryId`
- `404 Not Found`: unknown transfer id
- `409 Conflict`: transfer is not retryable

## Events

```http
GET /api/v1/events
```

Server-Sent Events stream:

```powershell
curl -N -H "X-LanCopy-Token: <token>" http://127.0.0.1:3489/api/v1/events
```

Event payload:

```json
{
  "type": "progress",
  "timestampUtc": "2026-07-05T17:00:02Z",
  "job": {
    "id": "transfer-id",
    "state": "running",
    "doneBytes": 524288,
    "totalBytes": 1048576
  }
}
```

Common event types:

- `queued`
- `started`
- `progress`
- `completed`
- `failed`
- `canceled`

## Security Notes

- The API binds to `127.0.0.1`, not the LAN.
- Keep the token private.
- Do not expose the local API through a reverse proxy unless you add your own authentication and network controls.
- The API is for automation on the same computer; the desktop app remains the recommended interface for normal users.
