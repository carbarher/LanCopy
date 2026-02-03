# Script de diagnóstico avanzado para compilación Rust
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "DIAGNÓSTICO AVANZADO RUST DLL" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Verificar versión de Rust
Write-Host "[1] Versión de Rust:" -ForegroundColor Yellow
rustc --version --verbose
Write-Host ""

# 2. Verificar toolchain
Write-Host "[2] Toolchain activo:" -ForegroundColor Yellow
rustup show
Write-Host ""

# 3. Verificar linker MSVC
Write-Host "[3] Verificar linker MSVC:" -ForegroundColor Yellow
$linker = Get-Command link.exe -ErrorAction SilentlyContinue
if ($linker) {
    Write-Host "✓ Linker encontrado: $($linker.Source)" -ForegroundColor Green
    & link.exe 2>&1 | Select-String -Pattern "Version"
} else {
    Write-Host "✗ Linker MSVC NO encontrado" -ForegroundColor Red
}
Write-Host ""

# 4. Limpiar build anterior
Write-Host "[4] Limpiando build anterior..." -ForegroundColor Yellow
cargo clean
Write-Host ""

# 5. Compilar con máximo verbose
Write-Host "[5] Compilando con verbose máximo..." -ForegroundColor Yellow
$env:RUST_BACKTRACE = "full"
$env:RUST_LOG = "cargo::core::compiler::fingerprint=info"

# Capturar TODA la salida
$output = cargo build --release -vv 2>&1 | Out-String
Write-Host $output

# Guardar en archivo
$output | Out-File -FilePath "build_complete_log.txt" -Encoding UTF8
Write-Host "Log guardado en build_complete_log.txt" -ForegroundColor Green
Write-Host ""

# 6. Verificar si se generó la DLL
Write-Host "[6] Verificando archivos generados:" -ForegroundColor Yellow
$dll = Get-ChildItem -Path "target\release" -Filter "slskdown_core.dll" -Recurse -ErrorAction SilentlyContinue
if ($dll) {
    Write-Host "✓ DLL GENERADA: $($dll.FullName)" -ForegroundColor Green
    Write-Host "  Tamaño: $($dll.Length) bytes" -ForegroundColor Green
} else {
    Write-Host "✗ DLL NO GENERADA" -ForegroundColor Red
}
Write-Host ""

# 7. Verificar archivos en deps
Write-Host "[7] Archivos slskdown_core en deps:" -ForegroundColor Yellow
Get-ChildItem -Path "target\release\deps" -Filter "slskdown_core*" | ForEach-Object {
    Write-Host "  - $($_.Name) ($($_.Length) bytes)" -ForegroundColor Cyan
}
Write-Host ""

# 8. Verificar archivo .d
Write-Host "[8] Contenido de slskdown_core.d:" -ForegroundColor Yellow
if (Test-Path "target\release\deps\slskdown_core.d") {
    Get-Content "target\release\deps\slskdown_core.d"
} else {
    Write-Host "  Archivo .d no encontrado" -ForegroundColor Red
}
Write-Host ""

# 9. Buscar mensajes de error en el log
Write-Host "[9] Buscando errores en el log:" -ForegroundColor Yellow
if (Test-Path "build_complete_log.txt") {
    $errors = Select-String -Path "build_complete_log.txt" -Pattern "error|failed|cannot|unable" -CaseSensitive:$false
    if ($errors) {
        Write-Host "Errores encontrados:" -ForegroundColor Red
        $errors | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    } else {
        Write-Host "  No se encontraron mensajes de error explícitos" -ForegroundColor Yellow
    }
}
Write-Host ""

# 10. Verificar configuración de Cargo.toml
Write-Host "[10] Configuración de Cargo.toml:" -ForegroundColor Yellow
Get-Content "Cargo.toml" | Select-String -Pattern "crate-type|name"
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "DIAGNÓSTICO COMPLETADO" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
