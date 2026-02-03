# Agregar bloque finally correctamente después de la línea 20303
$ErrorActionPreference = "Stop"

$filePath = "MainForm.cs"
Write-Host "Leyendo archivo..." -ForegroundColor Yellow

$lines = [System.IO.File]::ReadAllLines($filePath, [System.Text.Encoding]::UTF8)
Write-Host "Lineas totales: $($lines.Count)" -ForegroundColor Cyan
Write-Host ""

# Crear backup
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupPath = "MainForm.cs.backup_$timestamp"
[System.IO.File]::Copy($filePath, $backupPath, $true)
Write-Host "Backup creado: $backupPath" -ForegroundColor Green
Write-Host ""

# Mostrar contexto actual
Write-Host "Contexto actual (20300-20310):" -ForegroundColor Cyan
for ($i = 20299; $i -le 20309 -and $i -lt $lines.Count; $i++) {
    Write-Host "  $($i+1): $($lines[$i])"
}
Write-Host ""

# Insertar bloque finally después de la línea 20303 (índice 20302)
Write-Host "Insertando bloque finally despues de linea 20303..." -ForegroundColor Yellow

$newLines = @()
for ($i = 0; $i -lt $lines.Count; $i++) {
    $newLines += $lines[$i]
    
    # Después de la línea 20303 (índice 20302), insertar el bloque finally
    if ($i -eq 20302) {
        # Obtener la indentación de la línea 20303
        $indentMatch = $lines[$i] -match '^(\s+)'
        $baseIndent = if ($matches) { $matches[1] } else { "" }
        
        Write-Host "  Indentacion base detectada: $($baseIndent.Length) espacios" -ForegroundColor Yellow
        
        # Agregar bloque finally con la misma indentación
        $newLines += "$baseIndent" + "finally"
        $newLines += "$baseIndent" + "{"
        $newLines += "$baseIndent" + "    // Asegurar que el semaphore siempre se libere"
        $newLines += "$baseIndent" + "    semaphore.Release();"
        $newLines += "$baseIndent" + "}"
    }
}

Write-Host "Nuevas lineas totales: $($newLines.Count)" -ForegroundColor Cyan
Write-Host ""

# Guardar archivo
Write-Host "Guardando archivo..." -ForegroundColor Yellow
[System.IO.File]::WriteAllLines($filePath, $newLines, [System.Text.Encoding]::UTF8)
Write-Host "Archivo guardado!" -ForegroundColor Green
Write-Host ""

# Verificar
Write-Host "Verificando contexto (lineas 20300-20315):" -ForegroundColor Cyan
$verifyLines = [System.IO.File]::ReadAllLines($filePath, [System.Text.Encoding]::UTF8)
for ($i = 20299; $i -le 20314 -and $i -lt $verifyLines.Count; $i++) {
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
