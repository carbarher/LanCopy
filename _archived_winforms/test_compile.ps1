$output = dotnet build SlskDown.csproj -c Release --no-incremental 2>&1
$output | Out-String | Write-Host
if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✅ COMPILACIÓN EXITOSA" -ForegroundColor Green
    if (Test-Path "bin\Release\net8.0-windows\SlskDown.exe") {
        Write-Host "Ejecutable generado correctamente" -ForegroundColor Green
    } else {
        Write-Host "⚠️ Ejecutable NO encontrado" -ForegroundColor Yellow
    }
} else {
    Write-Host "`n❌ ERROR DE COMPILACIÓN" -ForegroundColor Red
}
