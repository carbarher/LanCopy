# SOLUCION DEFINITIVA - Eliminar el bloque finally problemático
$ErrorActionPreference = "Stop"

$filePath = "MainForm.cs"
Write-Host "Leyendo archivo..." -ForegroundColor Yellow

$content = [System.IO.File]::ReadAllText($filePath, [System.Text.Encoding]::UTF8)
$lines = $content -split "`r?`n"

Write-Host "Lineas totales: $($lines.Count)" -ForegroundColor Cyan
Write-Host ""

# Crear backup con timestamp
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupPath = "MainForm.cs.backup_$timestamp"
[System.IO.File]::Copy($filePath, $backupPath, $true)
Write-Host "Backup creado: $backupPath" -ForegroundColor Green
Write-Host ""

# Eliminar líneas 20304-20308 (índices 20303-20307)
Write-Host "Eliminando lineas 20304-20308 (bloque finally)..." -ForegroundColor Yellow
$newLines = @()
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($i -ge 20303 -and $i -le 20307) {
        Write-Host "  Eliminando linea $($i+1): $($lines[$i])" -ForegroundColor Red
        continue
    }
    $newLines += $lines[$i]
}

Write-Host ""
Write-Host "Nuevas lineas totales: $($newLines.Count)" -ForegroundColor Cyan
Write-Host ""

# Guardar archivo
Write-Host "Guardando archivo..." -ForegroundColor Yellow
$newContent = $newLines -join "`r`n"
[System.IO.File]::WriteAllText($filePath, $newContent, [System.Text.Encoding]::UTF8)
Write-Host "Archivo guardado!" -ForegroundColor Green
Write-Host ""

# Verificar
Write-Host "Verificando contexto (lineas 20300-20310):" -ForegroundColor Cyan
$verifyLines = [System.IO.File]::ReadAllLines($filePath, [System.Text.Encoding]::UTF8)
for ($i = 20299; $i -le 20309 -and $i -lt $verifyLines.Count; $i++) {
    Write-Host "  $($i+1): $($verifyLines[$i])"
}
Write-Host ""

# Compilar
Write-Host "========================================" -ForegroundColor Magenta
Write-Host "COMPILANDO PROYECTO..." -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""

$buildOutput = & dotnet build SlskDown.csproj --no-incremental 2>&1
$buildOutput | Write-Host

Write-Host ""
Write-Host "========================================" -ForegroundColor Magenta
if ($LASTEXITCODE -eq 0) {
    Write-Host "COMPILACION EXITOSA!" -ForegroundColor Green
} else {
    Write-Host "COMPILACION FALLIDA - Codigo: $LASTEXITCODE" -ForegroundColor Red
}
Write-Host "========================================" -ForegroundColor Magenta
