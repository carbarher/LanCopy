Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  COMPILANDO RUST (13 funcionalidades)" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

Push-Location rust_core

Write-Host "[1/3] Limpiando compilacion anterior..." -ForegroundColor Yellow
cargo clean | Out-Null

Write-Host "[2/3] Compilando con optimizaciones maximas..." -ForegroundColor Yellow
$output = cargo build --release 2>&1
$exitCode = $LASTEXITCODE

if ($exitCode -ne 0) {
    Write-Host ""
    Write-Host "ERROR: Compilacion fallida" -ForegroundColor Red
    Write-Host ""
    Write-Host "Salida de cargo:" -ForegroundColor Yellow
    $output | ForEach-Object { Write-Host $_ }
    Pop-Location
    exit 1
}

Write-Host "  Compilacion exitosa!" -ForegroundColor Green
Write-Host ""

Write-Host "[3/3] Copiando DLL al directorio principal..." -ForegroundColor Yellow
$sourceDll = "target\release\slskdown_core.dll"
$destDll = "..\slskdown_core.dll"

if (Test-Path $sourceDll) {
    Copy-Item $sourceDll $destDll -Force
    $dllSize = (Get-Item $sourceDll).Length / 1MB
    Write-Host "  DLL copiada: $($dllSize.ToString('F2')) MB" -ForegroundColor Green
} else {
    Write-Host "  ERROR: DLL no encontrada en target\release\" -ForegroundColor Red
    Pop-Location
    exit 1
}

Pop-Location

Write-Host ""
Write-Host "=====================================" -ForegroundColor Green
Write-Host "  COMPILACION EXITOSA" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""
Write-Host "DLL ubicada en: $(Get-Location)\slskdown_core.dll" -ForegroundColor Cyan
Write-Host ""
Write-Host "Proximo paso:" -ForegroundColor Yellow
Write-Host "  1. Compilar SlskDown.csproj" -ForegroundColor White
Write-Host "  2. Probar funcionalidades Rust" -ForegroundColor White
Write-Host ""
