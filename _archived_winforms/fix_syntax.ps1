# Script para corregir el error de sintaxis en MainForm.cs
$filePath = "MainForm.cs"
$lines = [System.IO.File]::ReadAllLines($filePath)

Write-Host "Total lines in file: $($lines.Count)"
Write-Host "Line 20303: $($lines[20302])"
Write-Host "Line 20304: $($lines[20303])"

# Verificar si necesitamos agregar el bloque finally
if ($lines[20302] -match '^\s+}\s*$' -and $lines[20303] -match '^\s+}\)\s*;\s*$') {
    Write-Host "ERROR DETECTADO: Falta el bloque finally"
    
    # Insertar el bloque finally después de la línea 20302
    $newLines = @()
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $newLines += $lines[$i]
        
        if ($i -eq 20302) {
            # Agregar el bloque finally
            $newLines += "                                finally"
            $newLines += "                                {"
            $newLines += "                                    // Asegurar que el semaphore siempre se libere"
            $newLines += "                                    semaphore.Release();"
            $newLines += "                                }"
        }
    }
    
    # Guardar el archivo corregido
    [System.IO.File]::WriteAllLines($filePath, $newLines)
    Write-Host "Archivo corregido. Nuevas líneas: $($newLines.Count)"
} else {
    Write-Host "El archivo parece estar correcto o tiene una estructura diferente"
}
