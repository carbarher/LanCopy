Write-Host "Limpiando proyecto..." -ForegroundColor Cyan
dotnet clean

Write-Host "`nCompilando..." -ForegroundColor Cyan
$output = dotnet build -c Release 2>&1 | Out-String
Write-Host $output

Write-Host "`nVerificando ejecutable..." -ForegroundColor Cyan
if (Test-Path "bin\Release\net8.0-windows\SlskDown.exe") {
    Write-Host "[OK] Ejecutable generado exitosamente" -ForegroundColor Green
    Get-Item "bin\Release\net8.0-windows\SlskDown.exe" | Select-Object Name, Length, LastWriteTime
} else {
    Write-Host "[ERROR] No se genero el ejecutable" -ForegroundColor Red
}

Write-Host "`nPresiona cualquier tecla para continuar..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
