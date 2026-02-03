# Setup completo para SlskDown
# Ejecutar como Administrador

$ErrorActionPreference = "Continue"

Write-Host "========================================================"
Write-Host "  CONFIGURACION COMPLETA DE SLSKDOWN"
Write-Host "========================================================"
Write-Host ""

# Verificar administrador
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "ERROR: Ejecutar como Administrador" -ForegroundColor Red
    Write-Host ""
    Write-Host "1. Windows + X"
    Write-Host "2. PowerShell (Administrador)"
    Write-Host "3. cd C:\p2p\SlskDown"
    Write-Host "4. .\setup.ps1"
    Write-Host ""
    pause
    exit 1
}

# Variables
$folder = "C:\p2p\SlskDown"
$exe = "C:\p2p\SlskDown\bin\Release\net8.0-windows\SlskDown.exe"

Write-Host "========================================================"
Write-Host "  PASO 1: WINDOWS DEFENDER"
Write-Host "========================================================"
Write-Host ""

Write-Host "Agregando exclusion..." -ForegroundColor Yellow
try {
    Add-MpPreference -ExclusionPath $folder -ErrorAction Stop
    Write-Host "  OK - Exclusion agregada" -ForegroundColor Green
} catch {
    Write-Host "  OK - Ya existia" -ForegroundColor Green
}
Write-Host ""

Write-Host "========================================================"
Write-Host "  PASO 2: FIREWALL"
Write-Host "========================================================"
Write-Host ""

Write-Host "Eliminando reglas antiguas..." -ForegroundColor Yellow
Remove-NetFirewallRule -DisplayName "SlskDown*" -ErrorAction SilentlyContinue
Write-Host "  OK" -ForegroundColor Green
Write-Host ""

Write-Host "Creando regla de ENTRADA..." -ForegroundColor Yellow
try {
    New-NetFirewallRule -DisplayName "SlskDown Entrada" -Direction Inbound -Program $exe -Action Allow -Profile Any -Enabled True -ErrorAction Stop | Out-Null
    Write-Host "  OK - Regla creada" -ForegroundColor Green
} catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "Creando regla de SALIDA..." -ForegroundColor Yellow
try {
    New-NetFirewallRule -DisplayName "SlskDown Salida" -Direction Outbound -Program $exe -Action Allow -Profile Any -Enabled True -ErrorAction Stop | Out-Null
    Write-Host "  OK - Regla creada" -ForegroundColor Green
} catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "========================================================"
Write-Host "  VERIFICACION"
Write-Host "========================================================"
Write-Host ""

Write-Host "Exclusiones de Defender:" -ForegroundColor Yellow
$exclusions = (Get-MpPreference).ExclusionPath
if ($exclusions -contains $folder) {
    Write-Host "  OK - $folder" -ForegroundColor Green
} else {
    Write-Host "  ADVERTENCIA - No verificada" -ForegroundColor Yellow
}
Write-Host ""

Write-Host "Reglas de Firewall:" -ForegroundColor Yellow
$rules = Get-NetFirewallRule -DisplayName "SlskDown*" -ErrorAction SilentlyContinue
if ($rules) {
    foreach ($rule in $rules) {
        Write-Host "  OK - $($rule.DisplayName)" -ForegroundColor Green
    }
} else {
    Write-Host "  ADVERTENCIA - No encontradas" -ForegroundColor Yellow
}
Write-Host ""

Write-Host "========================================================"
Write-Host "  COMPLETADO"
Write-Host "========================================================"
Write-Host ""
Write-Host "SIGUIENTE:"
Write-Host "  1. Compila SlskDown"
Write-Host "  2. Ejecuta SlskDown"
Write-Host "  3. Conecta a Soulseek"
Write-Host ""
pause
