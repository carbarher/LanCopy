# Restaurar desde backup y aplicar fix correcto
$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Magenta
Write-Host "RESTAURACION Y CORRECCION DEFINITIVA" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""

# Buscar el backup más antiguo (antes de todos los intentos de fix)
$backups = Get-ChildItem "MainForm.cs.backup*" | Sort-Object LastWriteTime
Write-Host "Backups encontrados: $($backups.Count)" -ForegroundColor Cyan

if ($backups.Count -gt 0) {
    $oldestBackup = $backups[0]
    Write-Host "Usando backup mas antiguo: $($oldestBackup.Name)" -ForegroundColor Yellow
    Write-Host "Fecha: $($oldestBackup.LastWriteTime)" -ForegroundColor Yellow
    Write-Host ""
    
    # Restaurar desde el backup
    Copy-Item $oldestBackup.FullName "MainForm.cs" -Force
    Write-Host "Archivo restaurado desde backup" -ForegroundColor Green
    Write-Host ""
}

# Leer el archivo restaurado
$lines = [System.IO.File]::ReadAllLines("MainForm.cs", [System.Text.Encoding]::UTF8)
Write-Host "Lineas totales: $($lines.Count)" -ForegroundColor Cyan
Write-Host ""

# Verificar contexto
Write-Host "Verificando contexto (20300-20310):" -ForegroundColor Cyan
for ($i = 20299; $i -le 20309 -and $i -lt $lines.Count; $i++) {
    Write-Host "  $($i+1): $($lines[$i])"
}
Write-Host ""

# Compilar para ver el estado
Write-Host "Compilando archivo restaurado..." -ForegroundColor Yellow
$buildOutput = & dotnet build SlskDown.csproj --no-incremental 2>&1
$buildOutput | Select-String "error" | Write-Host

Write-Host ""
Write-Host "Codigo de salida: $LASTEXITCODE" -ForegroundColor $(if ($LASTEXITCODE -eq 0) { "Green" } else { "Red" })
Write-Host ""
Write-Host "========================================" -ForegroundColor Magenta
