$ErrorActionPreference = "Stop"

Write-Host "=== Creando build publish_hotfix12 ===" -ForegroundColor Cyan

$source = "bin\Release\net8.0-windows"
$dest = "bin\publish_hotfix12"

try {
    # Crear directorio destino
    if (-not (Test-Path $dest)) {
        New-Item -ItemType Directory -Path $dest -Force | Out-Null
        Write-Host "[OK] Directorio creado: $dest" -ForegroundColor Green
    } else {
        Write-Host "[INFO] Directorio ya existe: $dest" -ForegroundColor Yellow
    }
    
    # Copiar archivos
    Write-Host "Copiando archivos de $source a $dest..." -ForegroundColor Cyan
    Copy-Item -Path "$source\*" -Destination $dest -Recurse -Force
    
    # Verificar
    if (Test-Path "$dest\SlskDown.exe") {
        $fileCount = (Get-ChildItem $dest -Recurse | Measure-Object).Count
        Write-Host "[OK] Build creado exitosamente!" -ForegroundColor Green
        Write-Host "Archivos copiados: $fileCount" -ForegroundColor Green
        Write-Host "Ubicacion: $(Resolve-Path $dest)" -ForegroundColor Green
    } else {
        Write-Host "[ERROR] SlskDown.exe no encontrado en destino" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "[ERROR] $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
