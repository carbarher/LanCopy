Write-Host "=== INICIANDO COMPILACION ===" -ForegroundColor Green
Write-Host "Hora actual: $(Get-Date)" -ForegroundColor Yellow

# Limpiar
Write-Host "`nLimpiando proyecto..." -ForegroundColor Cyan
dotnet clean -c Release -v minimal

# Compilar
Write-Host "`nCompilando..." -ForegroundColor Cyan
dotnet build -c Release -v minimal

# Verificar resultado
Write-Host "`n=== RESULTADO ===" -ForegroundColor Green
$exePath = "bin\Release\net8.0-windows\SlskDown_NEW.exe"
if (Test-Path $exePath) {
    $file = Get-Item $exePath
    Write-Host "Ejecutable encontrado:" -ForegroundColor Green
    Write-Host "  Ruta: $($file.FullName)" -ForegroundColor White
    Write-Host "  Fecha modificacion: $($file.LastWriteTime)" -ForegroundColor White
    Write-Host "  Tamano: $($file.Length) bytes" -ForegroundColor White
} else {
    Write-Host "ERROR: No se encontro el ejecutable en $exePath" -ForegroundColor Red
}

Write-Host "`nHora final: $(Get-Date)" -ForegroundColor Yellow
