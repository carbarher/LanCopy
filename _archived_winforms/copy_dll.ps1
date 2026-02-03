# Script PowerShell para copiar slsk_native.dll

$source = "c:\p2p\SlskDown\slsk_native\target\release\slsk_native.dll"
$dest = "c:\p2p\SlskDown\bin\Release\net8.0-windows\slsk_native.dll"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Copiando DLL de Rust" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (Test-Path $source) {
    Write-Host "✓ Origen encontrado: $source" -ForegroundColor Green
    $sourceSize = (Get-Item $source).Length / 1MB
    Write-Host "  Tamaño: $([math]::Round($sourceSize, 2)) MB" -ForegroundColor Gray
    Write-Host ""
    
    Copy-Item -Path $source -Destination $dest -Force
    
    if (Test-Path $dest) {
        Write-Host "✓ DLL copiada exitosamente!" -ForegroundColor Green
        Write-Host "  Destino: $dest" -ForegroundColor Gray
        $destSize = (Get-Item $dest).Length / 1MB
        Write-Host "  Tamaño: $([math]::Round($destSize, 2)) MB" -ForegroundColor Gray
    } else {
        Write-Host "✗ Error: No se pudo copiar la DLL" -ForegroundColor Red
    }
} else {
    Write-Host "✗ Error: No se encontró slsk_native.dll" -ForegroundColor Red
    Write-Host "  Ruta buscada: $source" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Ejecuta primero:" -ForegroundColor Yellow
    Write-Host "  cd c:\p2p\SlskDown\slsk_native" -ForegroundColor White
    Write-Host "  cargo build --release" -ForegroundColor White
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
