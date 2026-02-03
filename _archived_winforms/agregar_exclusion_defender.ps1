# Script para agregar exclusión de SlskDown en Windows Defender
# DEBE ejecutarse como Administrador

$exclusionPath = "C:\p2p\SlskDown"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Agregar Exclusión en Windows Defender" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Verificar si se está ejecutando como administrador
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "ERROR: Este script debe ejecutarse como Administrador" -ForegroundColor Red
    Write-Host ""
    Write-Host "Haz clic derecho en PowerShell y selecciona 'Ejecutar como administrador'" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Presiona cualquier tecla para salir..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

Write-Host "Verificando si la carpeta existe..." -ForegroundColor Yellow
if (-not (Test-Path $exclusionPath)) {
    Write-Host "ERROR: No se encuentra la carpeta:" -ForegroundColor Red
    Write-Host "  $exclusionPath" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Presiona cualquier tecla para salir..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

Write-Host "  OK - Carpeta encontrada: $exclusionPath" -ForegroundColor Green
Write-Host ""

Write-Host "Agregando exclusión en Windows Defender..." -ForegroundColor Yellow
try {
    # Agregar exclusión de ruta
    Add-MpPreference -ExclusionPath $exclusionPath
    Write-Host "  OK - Exclusión agregada exitosamente" -ForegroundColor Green
} catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Presiona cualquier tecla para salir..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

Write-Host ""
Write-Host "Verificando exclusiones actuales..." -ForegroundColor Yellow
try {
    $exclusions = Get-MpPreference | Select-Object -ExpandProperty ExclusionPath
    if ($exclusions -contains $exclusionPath) {
        Write-Host "  OK - Exclusión verificada:" -ForegroundColor Green
        Write-Host "    $exclusionPath" -ForegroundColor White
    } else {
        Write-Host "  ADVERTENCIA: No se pudo verificar la exclusión" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ADVERTENCIA: No se pudo verificar: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Configuración completada!" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "SlskDown ahora está excluido de Windows Defender." -ForegroundColor White
Write-Host ""
Write-Host "SIGUIENTE PASO:" -ForegroundColor Yellow
Write-Host "1. Ejecuta: .\agregar_firewall.ps1" -ForegroundColor White
Write-Host "2. Compila SlskDown" -ForegroundColor White
Write-Host "3. Ejecuta SlskDown e intenta conectar" -ForegroundColor White
Write-Host ""
Write-Host "Presiona cualquier tecla para salir..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
