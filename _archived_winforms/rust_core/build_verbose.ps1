# Script para compilar Rust con salida completa
Write-Host "=== Compilando Rust DLL ===" -ForegroundColor Cyan

# Limpiar build anterior
Write-Host "`nLimpiando build anterior..." -ForegroundColor Yellow
cargo clean 2>&1 | Out-String | Write-Host

# Compilar con verbose
Write-Host "`nCompilando con --verbose..." -ForegroundColor Yellow
$output = cargo build --release --verbose 2>&1 | Out-String
Write-Host $output

# Verificar DLL generada
Write-Host "`n=== Verificando archivos generados ===" -ForegroundColor Cyan

$dllPath = "target\release\slskdown_core.dll"
$depsPath = "target\release\deps\slskdown_core.dll"

if (Test-Path $dllPath) {
    Write-Host "✓ DLL encontrada en: $dllPath" -ForegroundColor Green
    Get-Item $dllPath | Select-Object Name, Length, LastWriteTime
} elseif (Test-Path $depsPath) {
    Write-Host "✓ DLL encontrada en: $depsPath" -ForegroundColor Green
    Get-Item $depsPath | Select-Object Name, Length, LastWriteTime
} else {
    Write-Host "✗ DLL NO encontrada" -ForegroundColor Red
    Write-Host "`nArchivos en target\release:" -ForegroundColor Yellow
    Get-ChildItem "target\release" -File | Select-Object Name, Length
    Write-Host "`nArchivos en target\release\deps:" -ForegroundColor Yellow
    Get-ChildItem "target\release\deps" -Filter "slskdown*" | Select-Object Name, Length
}

Write-Host "`n=== Fin ===" -ForegroundColor Cyan
