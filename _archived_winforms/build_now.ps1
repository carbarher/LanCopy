Set-Location "c:\p2p\SlskDown"
Write-Host "=== COMPILANDO SLSKDOWN ===" -ForegroundColor Cyan
dotnet build SlskDown.csproj -c Release -v normal
Write-Host ""
Write-Host "=== VERIFICANDO EJECUTABLE ===" -ForegroundColor Cyan
if (Test-Path "bin\Release\net8.0-windows\SlskDown.exe") {
    Write-Host "✓ Ejecutable generado correctamente" -ForegroundColor Green
    Write-Host "Iniciando aplicación..." -ForegroundColor Yellow
    Start-Process "bin\Release\net8.0-windows\SlskDown.exe"
} else {
    Write-Host "✗ ERROR: No se generó el ejecutable" -ForegroundColor Red
    Write-Host "Verificando carpeta bin..." -ForegroundColor Yellow
    Get-ChildItem "bin\Release\net8.0-windows" -ErrorAction SilentlyContinue
}
