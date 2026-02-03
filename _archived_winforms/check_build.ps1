$output = dotnet build SlskDown.csproj --configuration Release 2>&1 | Out-String
$lines = $output -split "`n"
$lastLines = $lines[-10..-1]
$lastLines | ForEach-Object { Write-Host $_ }
