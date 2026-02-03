Set-Location "c:\p2p\SlskDown"
Write-Host "=== COMPILANDO ===" -ForegroundColor Cyan
$output = dotnet build SlskDown.csproj -c Release 2>&1
Write-Host $output
Write-Host ""
Write-Host "=== VERIFICANDO ===" -ForegroundColor Cyan
if (Test-Path "bin\Release\net8.0-windows\SlskDown.exe") {
    Write-Host "✓ Ejecutable generado" -ForegroundColor Green
    Get-Item "bin\Release\net8.0-windows\SlskDown.exe" | Select-Object Name, Length, LastWriteTime
} else {
    Write-Host "✗ No se generó ejecutable" -ForegroundColor Red
}
