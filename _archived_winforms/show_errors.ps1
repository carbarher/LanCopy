$output = & dotnet build SlskDown.csproj 2>&1 | Out-String
$output | Out-File "latest_build.log" -Encoding UTF8
$errors = $output | Select-String "error CS"
Write-Host "=== ERRORES ENCONTRADOS ===" -ForegroundColor Red
$errors
$summary = $output | Select-String "Errores|Advertencia"
Write-Host "`n=== RESUMEN ===" -ForegroundColor Yellow
$summary
