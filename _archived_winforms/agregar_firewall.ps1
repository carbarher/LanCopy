# Script para agregar SlskDown al Firewall de Windows
# Ejecutar como Administrador

$appPath = "C:\p2p\SlskDown\bin\Release\net8.0-windows\SlskDown.exe"
$ruleName = "SlskDown - Soulseek Client"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Agregando SlskDown al Firewall de Windows" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Verificar si el archivo existe
if (-not (Test-Path $appPath)) {
    Write-Host "ERROR: No se encuentra SlskDown.exe en:" -ForegroundColor Red
    Write-Host "  $appPath" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Presiona cualquier tecla para salir..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

Write-Host "Archivo encontrado:" -ForegroundColor Green
Write-Host "  $appPath" -ForegroundColor White
Write-Host ""

# Eliminar reglas existentes (si existen)
Write-Host "Eliminando reglas antiguas (si existen)..." -ForegroundColor Yellow
try {
    Remove-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
    Write-Host "  OK - Reglas antiguas eliminadas" -ForegroundColor Green
} catch {
    Write-Host "  OK - No habia reglas antiguas" -ForegroundColor Gray
}
Write-Host ""

# Crear regla de entrada (Inbound)
Write-Host "Creando regla de ENTRADA (Inbound)..." -ForegroundColor Yellow
try {
    New-NetFirewallRule -DisplayName "$ruleName (Entrada)" `
                        -Direction Inbound `
                        -Program $appPath `
                        -Action Allow `
                        -Profile Any `
                        -Enabled True `
                        -Description "Permite conexiones entrantes para SlskDown (Soulseek)"
    Write-Host "  OK - Regla de entrada creada" -ForegroundColor Green
} catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Crear regla de salida (Outbound)
Write-Host "Creando regla de SALIDA (Outbound)..." -ForegroundColor Yellow
try {
    New-NetFirewallRule -DisplayName "$ruleName (Salida)" `
                        -Direction Outbound `
                        -Program $appPath `
                        -Action Allow `
                        -Profile Any `
                        -Enabled True `
                        -Description "Permite conexiones salientes para SlskDown (Soulseek)"
    Write-Host "  OK - Regla de salida creada" -ForegroundColor Green
} catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Configuracion completada!" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "SlskDown ahora tiene permiso en el Firewall de Windows." -ForegroundColor White
Write-Host ""
Write-Host "Presiona cualquier tecla para salir..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
