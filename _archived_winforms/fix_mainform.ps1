# Script para eliminar el método InitializeComponents duplicado (líneas 928-1509)
$file = "MainForm.cs"
$content = Get-Content $file

# Crear backup
Copy-Item $file "$file.backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"

# Leer todas las líneas
$newContent = @()
$skipMode = $false
$lineNum = 0

foreach ($line in $content) {
    $lineNum++
    
    # Detectar inicio del bloque a eliminar (línea 928)
    if ($lineNum -eq 928) {
        $skipMode = $true
        Write-Host "Iniciando eliminación en línea $lineNum" -ForegroundColor Yellow
    }
    
    # Detectar fin del bloque a eliminar (línea 1509 - cierre del método duplicado)
    if ($lineNum -eq 1509 -and $line.Trim() -eq "}") {
        $skipMode = $false
        Write-Host "Finalizando eliminación en línea $lineNum" -ForegroundColor Yellow
        continue # Saltar esta línea también
    }
    
    # Solo agregar líneas que no están en el bloque a eliminar
    if (-not $skipMode) {
        $newContent += $line
    }
}

# Guardar archivo modificado
$newContent | Set-Content $file -Encoding UTF8

Write-Host "`n✅ Archivo modificado exitosamente" -ForegroundColor Green
Write-Host "Líneas eliminadas: 928-1509 (582 líneas)" -ForegroundColor Cyan
Write-Host "Backup guardado como: $file.backup_*" -ForegroundColor Gray
