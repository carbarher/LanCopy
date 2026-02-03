# Script para corregir el error de sintaxis en MainForm.cs
# Lee el archivo físico desde el disco, aplica la corrección y lo guarda

$filePath = "MainForm.cs"
$backupPath = "MainForm.cs.backup_before_syntax_fix"

Write-Host "==================================="
Write-Host "CORRECTOR DE SINTAXIS MAINFORM.CS"
Write-Host "==================================="
Write-Host ""

# Leer el archivo desde el disco
Write-Host "Leyendo archivo desde disco..."
$lines = [System.IO.File]::ReadAllLines($filePath, [System.Text.Encoding]::UTF8)
Write-Host "Total de lineas: $($lines.Count)"
Write-Host ""

# Verificar las líneas problemáticas
Write-Host "Verificando lineas 20302-20305:"
Write-Host "  20302: $($lines[20301])"
Write-Host "  20303: $($lines[20302])"
Write-Host "  20304: $($lines[20303])"
if ($lines.Count -gt 20304) {
    Write-Host "  20305: $($lines[20304])"
}
Write-Host ""

# Verificar si el error existe
$needsFix = $false
if ($lines[20302] -match '^\s+}\s*$') {
    $nextLine = $lines[20303]
    if ($nextLine -match '^\s+}\)\s*;\s*$') {
        Write-Host "ERROR DETECTADO: Falta el bloque finally entre las lineas 20303 y 20304"
        $needsFix = $true
    }
}

if ($needsFix) {
    Write-Host ""
    Write-Host "Creando backup..."
    [System.IO.File]::Copy($filePath, $backupPath, $true)
    Write-Host "Backup guardado: $backupPath"
    Write-Host ""
    
    Write-Host "Aplicando correccion..."
    
    # Crear array con las líneas corregidas
    $newLines = New-Object System.Collections.ArrayList
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        [void]$newLines.Add($lines[$i])
        
        # Después de la línea 20302 (índice 20302), insertar el bloque finally
        if ($i -eq 20302) {
            [void]$newLines.Add("                                finally")
            [void]$newLines.Add("                                {")
            [void]$newLines.Add("                                    // Asegurar que el semaphore siempre se libere")
            [void]$newLines.Add("                                    semaphore.Release();")
            [void]$newLines.Add("                                }")
        }
    }
    
    # Guardar el archivo corregido
    [System.IO.File]::WriteAllLines($filePath, $newLines.ToArray(), [System.Text.Encoding]::UTF8)
    
    Write-Host "Archivo corregido exitosamente!"
    Write-Host "Nuevas lineas totales: $($newLines.Count)"
    Write-Host ""
    Write-Host "Verificando correccion..."
    $verifyLines = [System.IO.File]::ReadAllLines($filePath, [System.Text.Encoding]::UTF8)
    Write-Host "  20303: $($verifyLines[20302])"
    Write-Host "  20304: $($verifyLines[20303])"
    Write-Host "  20305: $($verifyLines[20304])"
    Write-Host "  20306: $($verifyLines[20305])"
    Write-Host "  20307: $($verifyLines[20306])"
    Write-Host "  20308: $($verifyLines[20307])"
    Write-Host "  20309: $($verifyLines[20308])"
    Write-Host ""
    Write-Host "CORRECCION COMPLETADA!"
} else {
    Write-Host "El archivo no necesita correccion o ya fue corregido."
    Write-Host "Estructura actual:"
    for ($i = 20300; $i -lt 20310 -and $i -lt $lines.Count; $i++) {
        Write-Host "  $($i+1): $($lines[$i])"
    }
}

Write-Host ""
Write-Host "==================================="
