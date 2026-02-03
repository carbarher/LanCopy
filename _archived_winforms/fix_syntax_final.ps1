# Script para corregir el error de sintaxis eliminando el bloque finally problemático
$filePath = "MainForm.cs"
$backupPath = "MainForm.cs.backup_remove_finally"

Write-Host "==================================="
Write-Host "CORRECTOR DE SINTAXIS - ELIMINAR FINALLY"
Write-Host "==================================="
Write-Host ""

# Leer el archivo
$lines = [System.IO.File]::ReadAllLines($filePath, [System.Text.Encoding]::UTF8)
Write-Host "Total de lineas: $($lines.Count)"
Write-Host ""

# Mostrar contexto
Write-Host "Contexto actual (20300-20310):"
for ($i = 20299; $i -le 20309; $i++) {
    Write-Host "  $($i+1): $($lines[$i])"
}
Write-Host ""

# Crear backup
Write-Host "Creando backup..."
[System.IO.File]::Copy($filePath, $backupPath, $true)
Write-Host "Backup guardado: $backupPath"
Write-Host ""

# Eliminar las líneas 20304-20308 (índices 20303-20307) que contienen el bloque finally problemático
Write-Host "Eliminando bloque finally problemático (lineas 20304-20308)..."
$newLines = New-Object System.Collections.ArrayList

for ($i = 0; $i -lt $lines.Count; $i++) {
    # Saltar las líneas 20303-20307 (índices del bloque finally)
    if ($i -ge 20303 -and $i -le 20307) {
        continue
    }
    [void]$newLines.Add($lines[$i])
}

# Guardar el archivo
[System.IO.File]::WriteAllLines($filePath, $newLines.ToArray(), [System.Text.Encoding]::UTF8)

Write-Host "Archivo modificado!"
Write-Host "Nuevas lineas totales: $($newLines.Count)"
Write-Host ""

# Verificar
$verifyLines = [System.IO.File]::ReadAllLines($filePath, [System.Text.Encoding]::UTF8)
Write-Host "Contexto despues de eliminar (20300-20310):"
for ($i = 20299; $i -le 20305; $i++) {
    if ($i -lt $verifyLines.Count) {
        Write-Host "  $($i+1): $($verifyLines[$i])"
    }
}
Write-Host ""

Write-Host "Compilando para verificar..."
Write-Host ""
& dotnet build SlskDown.csproj --no-incremental

Write-Host ""
Write-Host "==================================="
Write-Host "Codigo de salida: $LASTEXITCODE"
if ($LASTEXITCODE -eq 0) {
    Write-Host "COMPILACION EXITOSA!"
} else {
    Write-Host "COMPILACION FALLIDA"
}
Write-Host "==================================="
