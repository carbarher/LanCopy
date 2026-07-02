# CLI & API Cookbook

## CLI examples

```powershell
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- peers --wait 5 --json
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- send C:\tmp\file.zip --to 192.168.1.50:8742 --pin 1234
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- sync C:\tmp\folder --to 192.168.1.50:8742 --remote-root backup
dotnet run --project LanCopy.Cli/LanCopy.Cli.csproj -- api --port 3489
```

## API examples

```powershell
curl -H "X-LanCopy-Token: <token>" http://127.0.0.1:3489/api/v1/peers
curl -X POST -H "Content-Type: application/json" -H "X-LanCopy-Token: <token>" ^
  -d "{\"localPath\":\"C:\\\\tmp\\\\file.zip\",\"to\":\"192.168.1.50:8742\"}" ^
  http://127.0.0.1:3489/api/v1/transfers/send
curl -X POST -H "X-LanCopy-Token: <token>" http://127.0.0.1:3489/api/v1/transfers/<id>/cancel
curl -X POST -H "X-LanCopy-Token: <token>" http://127.0.0.1:3489/api/v1/transfers/<id>/retry
curl -N -H "X-LanCopy-Token: <token>" http://127.0.0.1:3489/api/v1/events
```

## Integration pack
- Postman: `scripts/api/LanCopy-Local-API.postman_collection.json`
- Bash: `scripts/api/lancopy-api-curl.sh`
- PowerShell: `scripts/api/lancopy-api-curl.ps1`

## Notes (FR/DE)
- FR: Importez la collection Postman, configurez `baseUrl` et `token`, puis executez les requetes.
- DE: Postman-Collection importieren, `baseUrl` und `token` setzen, dann Requests ausfuhren.
