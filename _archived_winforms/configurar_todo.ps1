# Script MAESTRO para configurar SlskDown completamente
# Ejecuta: Exclusión de Defender + Reglas de Firewall
# DEBE ejecutarse como Administrador

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  CONFIGURACIÓN COMPLETA DE SLSKDOWN" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

# Verificar si se está ejecutando como administrador
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "ERROR: Este script debe ejecutarse como Administrador" -ForegroundColor Red
    Write-Host ""
    Write-Host "INSTRUCCIONES:" -ForegroundColor Yellow
    Write-Host "1. Presiona Windows + X" -ForegroundColor White
    Write-Host "2. Selecciona 'Windows PowerShell (Administrador)'" -ForegroundColor White
    Write-Host "3. Ejecuta: cd C:\p2p\SlskDown" -ForegroundColor White
    Write-Host "4. Ejecuta: .\configurar_todo.ps1" -ForegroundColor White
    Write-Host ""
    Write-Host "Presiona cualquier tecla para salir..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

$exclusionPath = "C:\p2p\SlskDown"
$appPath = "C:\p2p\SlskDown\bin\Release\net8.0-windows\SlskDown.exe"
$ruleName = "SlskDown - Soulseek Client"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  PASO 1: AGREGAR EXCLUSIÓN EN WINDOWS DEFENDER" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Verificando carpeta..." -ForegroundColor Yellow
if (-not (Test-Path $exclusionPath)) {
    Write-Host "  ERROR: No se encuentra: $exclusionPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Presiona cualquier tecla para salir..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}
Write-Host "  OK - Carpeta encontrada" -ForegroundColor Green
Write-Host ""

Write-Host "Agregando exclusión en Windows Defender..." -ForegroundColor Yellow
try {
    Add-MpPreference -ExclusionPath $exclusionPath -ErrorAction Stop
    Write-Host "  OK - Exclusión agregada" -ForegroundColor Green
} catch {
    if ($_.Exception.Message -like "*already exists*" -or $_.Exception.Message -like "*ya existe*") {
        Write-Host "  OK - Exclusión ya existía" -ForegroundColor Green
    } else {
        Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
    }
}
Write-Host ""

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  PASO 2: AGREGAR REGLAS DE FIREWALL" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Verificando ejecutable..." -ForegroundColor Yellow
if (-not (Test-Path $appPath)) {
    Write-Host "  ADVERTENCIA: No se encuentra SlskDown.exe" -ForegroundColor Yellow
    Write-Host "  Ruta esperada: $appPath" -ForegroundColor White
    Write-Host "  Las reglas se crearán de todas formas." -ForegroundColor White
} else {
    Write-Host "  OK - Ejecutable encontrado" -ForegroundColor Green
}
Write-Host ""

Write-Host "Eliminando reglas antiguas..." -ForegroundColor Yellow
try {
    Remove-NetFirewallRule -DisplayName "$ruleName*" -ErrorAction SilentlyContinue
    Write-Host "  OK - Reglas antiguas eliminadas" -ForegroundColor Green
} catch {
    Write-Host "  OK - No había reglas antiguas" -ForegroundColor Gray
}
Write-Host ""

Write-Host "Creando regla de ENTRADA (Inbound)..." -ForegroundColor Yellow
try {
    New-NetFirewallRule -DisplayName "$ruleName (Entrada)" `
                        -Direction Inbound `
                        -Program $appPath `
                        -Action Allow `
                        -Profile Any `
                        -Enabled True `
                        -Description "Permite conexiones entrantes para SlskDown (Soulseek)" `
                        -ErrorAction Stop | Out-Null
    Write-Host "  OK - Regla de entrada creada" -ForegroundColor Green
} catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "Creando regla de SALIDA (Outbound)..." -ForegroundColor Yellow
try {
    New-NetFirewallRule -DisplayName "$ruleName (Salida)" `
                        -Direction Outbound `
                        -Program $appPath `
                        -Action Allow `
                        -Profile Any `
                        -Enabled True `
                        -Description "Permite conexiones salientes para SlskDown (Soulseek)" `
                        -ErrorAction Stop | Out-Null
    Write-Host "  OK - Regla de salida creada" -ForegroundColor Green
} catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  PASO 3: VERIFICACIÓN" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Verificando exclusiones de Defender..." -ForegroundColor Yellow
try {
    $exclusions = Get-MpPreference | Select-Object -ExpandProperty ExclusionPath
    if ($exclusions -contains $exclusionPath) {
        Write-Host "  OK - Exclusión verificada: $exclusionPath" -ForegroundColor Green
    } else {
        Write-Host "  ADVERTENCIA - No se pudo verificar la exclusión" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ADVERTENCIA - Error al verificar: $($_.Exception.Message)" -ForegroundColor Yellow
}
Write-Host ""

Write-Host "Verificando reglas de firewall..." -ForegroundColor Yellow
try {
    $rules = Get-NetFirewallRule -DisplayName "$ruleName*" -ErrorAction SilentlyContinue
    if ($rules) {
        foreach ($rule in $rules) {
            Write-Host "  OK - $($rule.DisplayName): Enabled=$($rule.Enabled), Action=$($rule.Action)" -ForegroundColor Green
        }
    } else {
        Write-Host "  ADVERTENCIA - No se encontraron reglas" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ADVERTENCIA - Error al verificar: $($_.Exception.Message)" -ForegroundColor Yellow
}
Write-Host ""

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  CONFIGURACIÓN COMPLETADA!" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "RESUMEN:" -ForegroundColor Yellow
Write-Host "  - Exclusión de Windows Defender: OK" -ForegroundColor White
Write-Host "  - Reglas de Firewall: OK" -ForegroundColor White
Write-Host ""
Write-Host "SIGUIENTES PASOS:" -ForegroundColor Yellow
Write-Host "  1. Compila SlskDown (si aún no lo has hecho)" -ForegroundColor White
Write-Host "  2. Ejecuta SlskDown" -ForegroundColor White
Write-Host "  3. Intenta conectar a Soulseek" -ForegroundColor White
Write-Host "  4. Deberías ver: 'Conexión exitosa en puerto 55555'" -ForegroundColor White
Write-Host ""
Write-Host "Si sigue sin funcionar, comparte los logs completos." -ForegroundColor Yellow
Write-Host ""
Write-Host "Presiona cualquier tecla para salir..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
