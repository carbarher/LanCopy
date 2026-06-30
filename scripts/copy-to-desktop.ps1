# Script para copiar LanCopy.exe a Desktop después de compilar

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir

Write-Host "LanCopy Desktop Installer" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host ""

$exePath = Join-Path $repoRoot "bin\$Configuration\net9.0\$Runtime\publish\LanCopy.exe"
$desktopPath = Join-Path $env:USERPROFILE "Desktop\LanCopy.exe"

if (-not (Test-Path $exePath)) {
    Write-Host "❌ Error: $exePath no encontrado" -ForegroundColor Red
    Write-Host "Ejecuta primero: dotnet publish -c $Configuration -r $Runtime" -ForegroundColor Yellow
    exit 1
}

Write-Host "Compilando..." -ForegroundColor Yellow
cd $repoRoot
dotnet publish -c $Configuration -r $Runtime --self-contained -o "bin\$Configuration\publish" 2>&1 | Select-Object -Last 2

if (Test-Path $exePath) {
    Copy-Item -Path $exePath -Destination $desktopPath -Force
    $size = (Get-Item $desktopPath).Length / 1MB
    Write-Host ""
    Write-Host "✅ LanCopy.exe ($([math]::Round($size,1)) MB) copiado a Desktop" -ForegroundColor Green
    Write-Host "   Ubicación: $desktopPath" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Próximos pasos:" -ForegroundColor Yellow
    Write-Host "  1. Cierra LanCopy si está abierto"
    Write-Host "  2. Haz doble-clic en Desktop\LanCopy.exe para ejecutar"
} else {
    Write-Host "❌ Error: No se pudo encontrar el exe después de compilar" -ForegroundColor Red
    exit 1
}
